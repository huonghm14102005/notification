using Notification.Application.Abstractions.Email;
using Notification.Application.Abstractions.Observability;
using Notification.Application.Abstractions.Security;
using Notification.Application.Abstractions.Time;
using Notification.Application.Notifications.Delivery;
using Notification.Application.Senders;

namespace Notification.Application.Tests.Notifications.Delivery;

public sealed class DeliverNotificationHandlerTests
{
    [Fact]
    public async Task SuccessSendsDecryptedContentAndCompletesOnce()
    {
        var repository = new Repository(Item()); var email = new Email(); using var metrics = new NotificationMetrics();
        var handler = new DeliverNotificationHandler(repository, email, new Cipher(), new Clock(), metrics);
        var result = await handler.HandleAsync(repository.Item!.Id, 1, CancellationToken.None);
        Assert.Equal("delivered", result.Status); Assert.Equal("Subject", email.Subject); Assert.Equal("Body", email.Body); Assert.Equal(1, repository.Successes);
        repository.Item = null; var repeated = await handler.HandleAsync(Guid.NewGuid(), 1, CancellationToken.None);
        Assert.Equal("skipped", repeated.Status); Assert.Equal(1, email.Calls);
    }

    [Fact]
    public async Task ProviderFailureIsRecordedWithoutThrowing()
    {
        var repository = new Repository(Item()); using var metrics = new NotificationMetrics();
        var handler = new DeliverNotificationHandler(repository, new Email(new EmailSendException("SMTP_AUTHENTICATION", false)), new Cipher(), new Clock(), metrics);
        var result = await handler.HandleAsync(repository.Item!.Id, 1, CancellationToken.None);
        Assert.Equal("failed", result.Status); Assert.Equal("SMTP_AUTHENTICATION", repository.ErrorCode); Assert.Equal(1, repository.Failures);
    }

    [Fact]
    public async Task HtmlAndTextSnapshotsAreSentTogether()
    {
        var item = Item() with { HtmlBodyEncrypted = "<b>Body</b>"u8.ToArray() };
        var repository = new Repository(item); var email = new Email(); using var metrics = new NotificationMetrics();
        var handler = new DeliverNotificationHandler(repository, email, new Cipher(), new Clock(), metrics);
        await handler.HandleAsync(item.Id, 1, CancellationToken.None);
        Assert.Equal("Body", email.Body); Assert.Equal("<b>Body</b>", email.HtmlBody);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 25)]
    public async Task TransientFailureSchedulesExpectedBackoff(int attemptNo, int delayMinutes)
    {
        var item = Item() with { AttemptNo = attemptNo };
        var repository = new Repository(item); using var metrics = new NotificationMetrics(); var clock = new Clock();
        var handler = new DeliverNotificationHandler(repository, new Email(new EmailSendException("SMTP_CONNECTION", true)), new Cipher(), clock, metrics);

        var result = await handler.HandleAsync(item.Id, attemptNo, CancellationToken.None);

        Assert.Equal("retrying", result.Status);
        Assert.Equal("SMTP_CONNECTION", repository.ErrorCode);
        Assert.Equal(clock.LastRead.AddMinutes(delayMinutes), repository.NextAttemptAt);
        Assert.Equal(1, repository.TransientFailures);
        Assert.Equal(0, repository.Failures);
    }

    [Fact]
    public async Task FourthTransientFailureIsTerminalButKeepsTransientAttemptResult()
    {
        var item = Item() with { AttemptNo = 4 };
        var repository = new Repository(item); using var metrics = new NotificationMetrics();
        var handler = new DeliverNotificationHandler(repository, new Email(new EmailSendException("SMTP_TIMEOUT", true)), new Cipher(), new Clock(), metrics);

        var result = await handler.HandleAsync(item.Id, 4, CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.Equal(1, repository.TransientFailures);
        Assert.Null(repository.NextAttemptAt);
        Assert.Equal(0, repository.Failures);
    }

    [Fact]
    public async Task ShutdownCancellationDoesNotCompleteAttempt()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var repository = new Repository(Item()); using var metrics = new NotificationMetrics();
        var handler = new DeliverNotificationHandler(repository, new Email(new OperationCanceledException(cancellation.Token)), new Cipher(), new Clock(), metrics);

        await Assert.ThrowsAsync<OperationCanceledException>(() => handler.HandleAsync(repository.Item!.Id, 1, cancellation.Token));

        Assert.Equal(0, repository.TransientFailures + repository.Failures + repository.Successes);
    }

    [Fact]
    public async Task DisabledSenderNeverOpensSmtp()
    {
        var item = Item() with { Sender = Item().Sender! with { Status = "disabled" } }; var repository = new Repository(item); var email = new Email(); using var metrics = new NotificationMetrics();
        var handler = new DeliverNotificationHandler(repository, email, new Cipher(), new Clock(), metrics);
        var result = await handler.HandleAsync(item.Id, 1, CancellationToken.None);
        Assert.Equal("SENDER_UNAVAILABLE", result.ErrorCode); Assert.Equal(0, email.Calls);
    }

    private static DeliveryWorkItem Item()
    {
        var id = Guid.NewGuid(); var tenant = Guid.NewGuid(); var senderId = Guid.NewGuid();
        return new(id, Guid.NewGuid(), tenant, senderId, 1, "sending", "student@example.test", "Subject"u8.ToArray(), "Body"u8.ToArray(), null,
            new(senderId, tenant, "smtp", "email", "host", 465, true, "user", [], "from@example.test", "From"));
    }
    private sealed class Repository(DeliveryWorkItem item) : IDeliveryRepository
    {
        public DeliveryWorkItem? Item { get; set; } = item; public int Successes { get; private set; }
        public int Failures { get; private set; }
        public int TransientFailures { get; private set; }
        public string? ErrorCode { get; private set; }
        public DateTimeOffset? NextAttemptAt { get; private set; }
        public Task<IReadOnlyList<ClaimedNotification>> ClaimDueAsync(DateTimeOffset now, int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<RecoveredNotification>> RecoverStuckAsync(DateTimeOffset now, DateTimeOffset staleBefore, int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task<DeliveryWorkItem?> LoadClaimedAsync(Guid notificationId, int attemptNo, CancellationToken ct) => Task.FromResult(Item);
        public Task<bool> CompleteSuccessAsync(DeliveryWorkItem item, string? providerMessageId, DateTimeOffset startedAt, DateTimeOffset finishedAt, CancellationToken ct) { Successes++; return Task.FromResult(true); }
        public Task<bool> CompleteTransientFailureAsync(DeliveryWorkItem item, string errorCode, string errorMessage, DateTimeOffset? nextAttemptAt, DateTimeOffset startedAt, DateTimeOffset finishedAt, CancellationToken ct) { TransientFailures++; ErrorCode = errorCode; NextAttemptAt = nextAttemptAt; return Task.FromResult(true); }
        public Task<bool> CompletePermanentFailureAsync(DeliveryWorkItem item, string errorCode, string errorMessage, DateTimeOffset startedAt, DateTimeOffset finishedAt, CancellationToken ct) { Failures++; ErrorCode = errorCode; return Task.FromResult(true); }
    }
    private sealed class Email(Exception? error = null) : IEmailSender
    {
        public int Calls { get; private set; }
        public string? Subject { get; private set; }
        public string? Body { get; private set; }
        public string? HtmlBody { get; private set; }
        public Task SendTestAsync(ResolvedSender sender, string recipientEmail, DateTimeOffset now, CancellationToken ct) => Task.CompletedTask;
        public Task<string?> SendAsync(ResolvedSender sender, string recipientEmail, string subject, string body, CancellationToken ct) { Calls++; Subject = subject; Body = body; if (error is not null) throw error; return Task.FromResult<string?>("provider-id"); }
        public Task<string?> SendAsync(ResolvedSender sender, string recipientEmail, string subject, string? textBody, string? htmlBody, CancellationToken ct)
        { Calls++; Subject = subject; Body = textBody; HtmlBody = htmlBody; if (error is not null) throw error; return Task.FromResult<string?>("provider-id"); }
    }
    private sealed class Cipher : ISecretCipher { public byte[] Encrypt(string plaintext, Guid tenantId, Guid recordId) => throw new NotSupportedException(); public string Decrypt(byte[] envelope, Guid tenantId, Guid recordId) => System.Text.Encoding.UTF8.GetString(envelope); }
    private sealed class Clock : IClock
    {
        private DateTimeOffset _now = new(2026, 8, 15, 0, 0, 0, TimeSpan.Zero);
        public DateTimeOffset LastRead { get; private set; }
        public DateTimeOffset UtcNow => LastRead = _now = _now.AddMilliseconds(1);
    }
}
