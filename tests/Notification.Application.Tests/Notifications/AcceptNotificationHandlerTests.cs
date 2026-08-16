using Notification.Application.Abstractions.Observability;
using Notification.Application.Abstractions.Security;
using Notification.Application.Abstractions.Time;
using Notification.Application.Notifications;
using Notification.Application.Senders;
using Notification.Domain.Notifications;

namespace Notification.Application.Tests.Notifications;

public sealed class AcceptNotificationHandlerTests
{
    [Fact]
    public async Task AcceptedNotificationIsEncryptedAndPersisted()
    {
        var repository = new Repository(); using var metrics = new NotificationMetrics();
        var tenantId = Guid.NewGuid(); var apiKeyId = Guid.NewGuid(); var senderId = Guid.NewGuid();
        var handler = new AcceptNotificationHandler(repository, new Resolver(senderId, tenantId), new Cipher(), new Clock(), metrics);

        var result = await handler.HandleAsync(tenantId, apiKeyId,
            new(null, "Subject", "Body", new("student@example.test", "S1")), CancellationToken.None);

        Assert.Equal(1, result.Accepted); Assert.NotNull(repository.Value); Assert.Equal(NotificationStatus.Accepted, repository.Value.Status);
        Assert.Equal(apiKeyId, repository.Value.ApiKeyId); Assert.Equal(senderId, repository.Value.SenderId);
        Assert.Equal("enc:Subject", System.Text.Encoding.UTF8.GetString(repository.Value.SubjectEncrypted));
        Assert.Equal("enc:Body", System.Text.Encoding.UTF8.GetString(repository.Value.BodyEncrypted));
    }

    [Fact]
    public async Task MissingSenderDoesNotPersist()
    {
        var repository = new Repository(); using var metrics = new NotificationMetrics();
        var handler = new AcceptNotificationHandler(repository, new MissingResolver(), new Cipher(), new Clock(), metrics);
        var error = await Assert.ThrowsAsync<NotificationOperationException>(() => handler.HandleAsync(Guid.NewGuid(), Guid.NewGuid(),
            new(null, "Subject", "Body", new("student@example.test", null)), CancellationToken.None));
        Assert.Equal("SENDER_NOT_FOUND", error.Code); Assert.Null(repository.Value);
    }

    private sealed class Repository : INotificationRepository
    {
        public OutboundNotification? Value { get; private set; }
        public Task AddAsync(OutboundNotification notification, CancellationToken ct)
        {
            Value = notification;
            return Task.CompletedTask;
        }
        public Task<NotificationWithAttempts?> GetWithAttemptsAsync(Guid tenantId, Guid notificationId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
    private sealed class Resolver(Guid senderId, Guid tenantId) : ISenderResolver { public Task<ResolvedSender> ResolveAsync(Guid tid, string? key, CancellationToken ct) => Task.FromResult(new ResolvedSender(senderId, tenantId, "default", "email", "smtp", 465, true, "user", [], "from@example.test", "From")); }
    private sealed class MissingResolver : ISenderResolver { public Task<ResolvedSender> ResolveAsync(Guid tenantId, string? senderKey, CancellationToken ct) => throw new SenderOperationException("SENDER_NOT_FOUND"); }
    private sealed class Cipher : ISecretCipher { public byte[] Encrypt(string plaintext, Guid tenantId, Guid recordId) => System.Text.Encoding.UTF8.GetBytes("enc:" + plaintext); public string Decrypt(byte[] envelope, Guid tenantId, Guid recordId) => throw new NotSupportedException(); }
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => new(2026, 8, 15, 0, 0, 0, TimeSpan.Zero); }
}
