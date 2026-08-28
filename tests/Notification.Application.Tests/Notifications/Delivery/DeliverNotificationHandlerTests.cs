using Notification.Application.Abstractions.Channels;
using Notification.Application.Abstractions.Email;
using Notification.Application.Abstractions.Observability;
using Notification.Application.Abstractions.Security;
using Notification.Application.Abstractions.Time;
using Notification.Application.Devices;
using Notification.Application.Notifications.Delivery;
using Notification.Application.Senders;
using Notification.Domain.Devices;
using Notification.Domain.Identity;

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
    public async Task TelegramDeliveryDispatchesToTelegramSender()
    {
        var item = Item("telegram") with { Target = "123456789" };
        var repository = new Repository(item); var telegram = new TelegramMock(); using var metrics = new NotificationMetrics();
        var handler = new DeliverNotificationHandler(repository, new Email(), telegram, new DiscordMock(), new Cipher(), new Clock(), metrics);
        var result = await handler.HandleAsync(item.Id, 1, CancellationToken.None);

        Assert.Equal("delivered", result.Status);
        Assert.Equal(1, telegram.Calls);
        Assert.Equal("123456789", telegram.Target);
        Assert.Equal("Subject", telegram.Subject);
        Assert.Equal(1, repository.Successes);
    }

    [Fact]
    public async Task DiscordDeliveryDispatchesToDiscordSender()
    {
        var item = Item("discord") with { Target = "https://discord.com/api/webhooks/123/abc" };
        var repository = new Repository(item); var discord = new DiscordMock(); using var metrics = new NotificationMetrics();
        var handler = new DeliverNotificationHandler(repository, new Email(), new TelegramMock(), discord, new Cipher(), new Clock(), metrics);
        var result = await handler.HandleAsync(item.Id, 1, CancellationToken.None);

        Assert.Equal("delivered", result.Status);
        Assert.Equal(1, discord.Calls);
        Assert.Equal("https://discord.com/api/webhooks/123/abc", discord.Target);
        Assert.Equal(1, repository.Successes);
    }

    [Fact]
    public async Task PushDeliveryDispatchesToPushSender()
    {
        var targetDeviceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var item = Item("push") with { TenantId = tenantId, Target = targetDeviceId.ToString() };
        var repository = new Repository(item);
        var pushSender = new PushMock();
        var pushEndpoint = new DevicePushEndpoint(Guid.NewGuid(), tenantId, targetDeviceId, "fcm", "raw_token_xyz"u8.ToArray(), DateTimeOffset.UtcNow);
        var deviceRepo = new MockDeviceRepo(pushEndpoint);
        using var metrics = new NotificationMetrics();

        var handler = new DeliverNotificationHandler(repository, new Email(), new TelegramMock(), new DiscordMock(), pushSender, deviceRepo, new Cipher(), new Clock(), metrics);
        var result = await handler.HandleAsync(item.Id, 1, CancellationToken.None);

        Assert.Equal("delivered", result.Status);
        Assert.Equal(1, pushSender.Calls);
        Assert.Equal("raw_token_xyz", pushSender.Token);
        Assert.Equal("fcm", pushSender.Platform);
        Assert.Equal("Subject", pushSender.Title);
        Assert.Equal(1, repository.Successes);
    }

    [Fact]
    public async Task PushInvalidTokenDisablesPushEndpoint()
    {
        var targetDeviceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var item = Item("push") with { TenantId = tenantId, Target = targetDeviceId.ToString() };
        var repository = new Repository(item);
        var pushSender = new PushMock(new ChannelSendException("push", "PUSH_TOKEN_INVALID", false));
        var pushEndpoint = new DevicePushEndpoint(Guid.NewGuid(), tenantId, targetDeviceId, "fcm", "bad_token"u8.ToArray(), DateTimeOffset.UtcNow);
        var deviceRepo = new MockDeviceRepo(pushEndpoint);
        using var metrics = new NotificationMetrics();

        var handler = new DeliverNotificationHandler(repository, new Email(), new TelegramMock(), new DiscordMock(), pushSender, deviceRepo, new Cipher(), new Clock(), metrics);
        var result = await handler.HandleAsync(item.Id, 1, CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.Equal("PUSH_TOKEN_INVALID", repository.ErrorCode);
        Assert.True(deviceRepo.DisableCalled);
    }

    [Fact]
    public async Task TelegramTransientFailureSchedulesRetry()
    {
        var item = Item("telegram");
        var repository = new Repository(item);
        var telegram = new TelegramMock(new ChannelSendException("telegram", "TELEGRAM_RATE_LIMITED", true));
        using var metrics = new NotificationMetrics(); var clock = new Clock();
        var handler = new DeliverNotificationHandler(repository, new Email(), telegram, new DiscordMock(), new Cipher(), clock, metrics);

        var result = await handler.HandleAsync(item.Id, 1, CancellationToken.None);

        Assert.Equal("retrying", result.Status);
        Assert.Equal("TELEGRAM_RATE_LIMITED", repository.ErrorCode);
        Assert.Equal(1, repository.TransientFailures);
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

    private static DeliveryWorkItem Item(string channel = "email")
    {
        var id = Guid.NewGuid(); var tenant = Guid.NewGuid(); var senderId = Guid.NewGuid();
        return new(id, Guid.NewGuid(), tenant, senderId, 1, "sending", channel, "student@example.test", "Subject"u8.ToArray(), "Body"u8.ToArray(), null,
            new(senderId, tenant, "smtp", channel, "host", 465, true, "user", [], "from@example.test", "From"));
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

    private sealed class TelegramMock(Exception? error = null) : ITelegramSender
    {
        public int Calls { get; private set; }
        public string? Target { get; private set; }
        public string? Subject { get; private set; }
        public Task<string?> SendAsync(ResolvedSender? sender, string target, string subject, string? textBody, string? htmlBody, CancellationToken ct)
        {
            Calls++; Target = target; Subject = subject;
            if (error is not null) throw error;
            return Task.FromResult<string?>("tg_12345");
        }
    }

    private sealed class DiscordMock(Exception? error = null) : IDiscordSender
    {
        public int Calls { get; private set; }
        public string? Target { get; private set; }
        public Task<string?> SendAsync(ResolvedSender? sender, string target, string subject, string? textBody, string? htmlBody, CancellationToken ct)
        {
            Calls++; Target = target;
            if (error is not null) throw error;
            return Task.FromResult<string?>("discord_ok");
        }
    }

    private sealed class PushMock(Exception? error = null) : IPushSender
    {
        public int Calls { get; private set; }
        public string? Platform { get; private set; }
        public string? Token { get; private set; }
        public string? Title { get; private set; }
        public Task<string?> SendAsync(string platform, string token, string title, string? body, IReadOnlyDictionary<string, string>? data, CancellationToken ct)
        {
            Calls++; Platform = platform; Token = token; Title = title;
            if (error is not null) throw error;
            return Task.FromResult<string?>("fcm_msg_12345");
        }
    }

    private sealed class MockDeviceRepo(DevicePushEndpoint? endpoint) : IDeviceRepository
    {
        public bool DisableCalled { get; private set; }
        public Task<DevicePushEndpoint?> FindActivePushEndpointAsync(Guid tenantId, Guid deviceId, CancellationToken cancellationToken) =>
            Task.FromResult(endpoint?.Status == "active" ? endpoint : null);
        public Task<DevicePushEndpoint?> FindPushEndpointAsync(Guid tenantId, Guid deviceId, CancellationToken cancellationToken) =>
            Task.FromResult(endpoint);
        public Task<bool> DisablePushEndpointAsync(Guid tenantId, Guid deviceId, DateTimeOffset now, CancellationToken cancellationToken)
        {
            DisableCalled = true;
            endpoint?.Disable(now);
            return Task.FromResult(true);
        }
        public Task AddAsync(Device device, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DeviceItem> GetOrCreateLegacyAsync(Guid tenantId, Guid actorId, string producerName, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DeviceItem?> GetAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DevicePage> ListAsync(Guid tenantId, Guid actorId, bool tenantScope, string? status, int limit, DateTimeOffset? cursorCreatedAt, Guid? cursorId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DeviceItem?> RenameAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, string name, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> DisableAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ConfigureCallbackAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, string url, byte[] secretEncrypted, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ClearCallbackAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DeviceKeyCreateResult> TryAddKeyAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, ApiKey apiKey, int deviceLimit, int tenantLimit, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DeviceApiKeyPage?> ListKeysAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, int limit, DateTimeOffset? cursorCreatedAt, Guid? cursorId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> RevokeKeyAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, Guid keyId, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SavePushEndpointAsync(DevicePushEndpoint endpoint, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class Cipher : ISecretCipher { public byte[] Encrypt(string plaintext, Guid tenantId, Guid recordId) => throw new NotSupportedException(); public string Decrypt(byte[] envelope, Guid tenantId, Guid recordId) => System.Text.Encoding.UTF8.GetString(envelope); }
    private sealed class Clock : IClock
    {
        private DateTimeOffset _now = new(2026, 8, 15, 0, 0, 0, TimeSpan.Zero);
        public DateTimeOffset LastRead { get; private set; }
        public DateTimeOffset UtcNow => LastRead = _now = _now.AddMilliseconds(1);
    }
}
