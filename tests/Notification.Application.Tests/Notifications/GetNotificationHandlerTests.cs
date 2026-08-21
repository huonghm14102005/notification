using Notification.Application.Abstractions.Security;
using Notification.Application.Notifications;
using Notification.Domain.Notifications;

namespace Notification.Application.Tests.Notifications;

public sealed class GetNotificationHandlerTests
{
    [Fact]
    public async Task AdminReceivesDecryptedContentAndPrivateMetadata()
    {
        var model = Model(); var handler = new GetNotificationHandler(new Repository(model), new Cipher());
        var result = await handler.HandleAsync(new(model.TenantId, model.Id, new(NotificationCallerType.Admin, null)), CancellationToken.None);
        Assert.NotNull(result); Assert.Equal("Subject", result.Subject); Assert.Equal("Body", result.Body);
        Assert.Equal("student-1", result.RecipientRef); Assert.Equal("sender", result.SenderKey);
        Assert.Equal("provider", result.DeliveryAttempts[0].ProviderMessageId);
    }

    [Fact]
    public async Task OwningApiKeyReceivesMetadataWithoutContentOrRef()
    {
        var model = Model(); var cipher = new Cipher(); var handler = new GetNotificationHandler(new Repository(model), cipher);
        var result = await handler.HandleAsync(new(model.TenantId, model.Id, new(NotificationCallerType.ApiKey, model.ApiKeyId)), CancellationToken.None);
        Assert.NotNull(result); Assert.Null(result.Subject); Assert.Null(result.Body); Assert.Null(result.RecipientRef);
        Assert.Null(result.SenderKey); Assert.Null(result.DeliveryAttempts[0].ProviderMessageId); Assert.Equal(0, cipher.Calls);
    }

    [Fact]
    public async Task DifferentApiKeyCannotReadNotification()
    {
        var model = Model(); var handler = new GetNotificationHandler(new Repository(model), new Cipher());
        var result = await handler.HandleAsync(new(model.TenantId, model.Id, new(NotificationCallerType.ApiKey, Guid.NewGuid())), CancellationToken.None);
        Assert.Null(result);
    }

    private static NotificationWithAttempts Model() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "DRL", "sender",
        "sent", "student@example.test", "student-1", "Subject"u8.ToArray(), "Body"u8.ToArray(),
        DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null,
        [new(1, "success", DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow, null, null, "provider")]);

    private sealed class Repository(NotificationWithAttempts value) : INotificationRepository
    {
        public Task AddAsync(OutboundNotification notification, Notification.Domain.Notifications.Delivery delivery, CancellationToken ct) => throw new NotSupportedException();
        public Task<NotificationWithAttempts?> GetWithAttemptsAsync(Guid tenantId, Guid notificationId, CancellationToken ct) =>
            Task.FromResult<NotificationWithAttempts?>(value.TenantId == tenantId && value.Id == notificationId ? value : null);
    }
    private sealed class Cipher : ISecretCipher
    {
        public int Calls { get; private set; }
        public byte[] Encrypt(string plaintext, Guid tenantId, Guid recordId) => throw new NotSupportedException();
        public string Decrypt(byte[] envelope, Guid tenantId, Guid recordId) { Calls++; return System.Text.Encoding.UTF8.GetString(envelope); }
    }
}
