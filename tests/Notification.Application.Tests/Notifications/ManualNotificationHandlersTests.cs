using Notification.Application.Abstractions.Time;
using Notification.Application.Notifications;
using Notification.Domain.Notifications;

namespace Notification.Application.Tests.Notifications;

public sealed class ManualNotificationHandlersTests
{
    [Fact]
    public async Task RetryForwardsTrustedIdentityAndClock()
    {
        var repository = new StubRepository(); var now = DateTimeOffset.Parse("2026-08-22T10:00:00Z");
        var handler = new ManualNotificationHandlers(repository, new Clock(now)); var tenant = Guid.NewGuid();
        var admin = Guid.NewGuid(); var source = Guid.NewGuid();
        var result = await handler.RetryAsync(tenant, admin, source, CancellationToken.None);
        Assert.True(result.Created); Assert.Equal((tenant, admin, source, now), repository.RetryCall);
    }

    [Fact]
    public async Task CancelMapsUnexpectedDatabaseFailure()
    {
        var handler = new ManualNotificationHandlers(new StubRepository { Fail = true }, new Clock(DateTimeOffset.UtcNow));
        var error = await Assert.ThrowsAsync<NotificationOperationException>(() =>
            handler.CancelAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));
        Assert.Equal("SERVICE_UNAVAILABLE", error.Code);
    }

    [Fact]
    public async Task DeleteForwardsTenantAndId()
    {
        var repo = new StubRepository();
        var handler = new ManualNotificationHandlers(repo, new Clock(DateTimeOffset.UtcNow));
        var tenantId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var deleted = await handler.DeleteAsync(tenantId, notificationId, CancellationToken.None);
        Assert.True(deleted);
        Assert.Equal((tenantId, notificationId), repo.DeleteCall);
    }

    private sealed class Clock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow => now; }
    private sealed class StubRepository : INotificationRepository
    {
        public bool Fail { get; init; }
        public (Guid, Guid, Guid, DateTimeOffset) RetryCall { get; private set; }
        public (Guid, Guid) DeleteCall { get; private set; }
        public Task<ManualRetryResult> RetryAsync(Guid tenantId, Guid adminId, Guid notificationId, DateTimeOffset now, CancellationToken ct)
        { RetryCall = (tenantId, adminId, notificationId, now); return Task.FromResult(new ManualRetryResult(true, Guid.NewGuid(), notificationId, NotificationStatus.Accepted, now)); }
        public Task CancelAsync(Guid tenantId, Guid adminId, Guid notificationId, DateTimeOffset now, CancellationToken ct)
        { if (Fail) throw new InvalidOperationException(); return Task.CompletedTask; }
        public Task<bool> DeleteAsync(Guid tenantId, Guid notificationId, CancellationToken ct)
        {
            if (Fail) throw new InvalidOperationException();
            DeleteCall = (tenantId, notificationId);
            return Task.FromResult(true);
        }
        public Task AddAsync(OutboundNotification notification, Notification.Domain.Notifications.Delivery delivery, CancellationToken ct) => throw new NotSupportedException();
        public Task<NotificationWithAttempts?> GetWithAttemptsAsync(Guid tenantId, Guid notificationId, CancellationToken ct) => throw new NotSupportedException();
    }
}
