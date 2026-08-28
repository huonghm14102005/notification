using Notification.Application.Notifications;

namespace Notification.Application.Tests.Notifications;

public sealed class ListNotificationsHandlerTests
{
    [Fact]
    public async Task ApiKeyCannotUseAdminFiltersBeforeRepositoryRuns()
    {
        var repository = new Repository(); var handler = new ListNotificationsHandler(repository);
        var error = await Assert.ThrowsAsync<NotificationOperationException>(() => handler.HandleAsync(Guid.NewGuid(),
            new(NotificationCallerType.ApiKey, Guid.NewGuid()), new(null, null, null, null, Guid.NewGuid(), null),
            50, null, CancellationToken.None));
        Assert.Equal("FILTER_NOT_ALLOWED", error.Code); Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task ValidCursorIsDecodedAndForwarded()
    {
        var repository = new Repository(); var handler = new ListNotificationsHandler(repository);
        var at = new DateTimeOffset(2026, 8, 22, 3, 0, 0, TimeSpan.Zero); var id = Guid.NewGuid();
        await handler.HandleAsync(Guid.NewGuid(), new(NotificationCallerType.Admin, null),
            new(null, null, null, null, null, null), 25, NotificationListCursor.Encode(at, id), CancellationToken.None);
        Assert.Equal(at, repository.Query!.CursorCreatedAt); Assert.Equal(id, repository.Query.CursorId);
        Assert.Equal(25, repository.Query.Limit);
    }

    [Theory]
    [InlineData("not-base64")]
    [InlineData("MXxiYWQ=")]
    public async Task InvalidCursorIsRejected(string cursor)
    {
        var repository = new Repository(); var handler = new ListNotificationsHandler(repository);
        var error = await Assert.ThrowsAsync<NotificationOperationException>(() => handler.HandleAsync(Guid.NewGuid(),
            new(NotificationCallerType.Admin, null), new(null, null, null, null, null, null), 50, cursor,
            CancellationToken.None));
        Assert.Equal("INVALID_CURSOR", error.Code); Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task DatabaseFailureMapsToServiceUnavailable()
    {
        var handler = new ListNotificationsHandler(new Repository(new InvalidOperationException()));
        var error = await Assert.ThrowsAsync<NotificationOperationException>(() => handler.HandleAsync(Guid.NewGuid(),
            new(NotificationCallerType.Admin, null), new(null, null, null, null, null, null), 50, null,
            CancellationToken.None));
        Assert.Equal("SERVICE_UNAVAILABLE", error.Code);
    }

    private sealed class Repository(Exception? error = null) : INotificationRepository
    {
        public int Calls { get; private set; }
        public NotificationListQuery? Query { get; private set; }
        public Task<NotificationListPage> ListAsync(NotificationListQuery query, CancellationToken ct)
        {
            Calls++; Query = query; if (error is not null) throw error;
            return Task.FromResult(new NotificationListPage([], null));
        }
        public Task AddAsync(Notification.Domain.Notifications.OutboundNotification notification,
            Notification.Domain.Notifications.Delivery delivery, CancellationToken ct) => throw new NotSupportedException();
        public Task<NotificationWithAttempts?> GetWithAttemptsAsync(Guid tenantId, Guid notificationId,
            CancellationToken ct) => throw new NotSupportedException();
    }
}
