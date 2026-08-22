namespace Notification.Application.Notifications;

public sealed class ListNotificationsHandler(INotificationRepository repository)
{
    public async Task<NotificationListPage> HandleAsync(Guid tenantId, AuthCaller caller, NotificationListFilter filter,
        int limit, string? cursor, CancellationToken ct)
    {
        if (caller.Type == NotificationCallerType.ApiKey && (filter.SourceDeviceId.HasValue || filter.ApiKeyId.HasValue))
            throw new NotificationOperationException("FILTER_NOT_ALLOWED");
        var (createdAt, id) = NotificationListCursor.Decode(cursor);
        try { return await repository.ListAsync(new(tenantId, caller, filter, limit, createdAt, id), ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { throw new NotificationOperationException("SERVICE_UNAVAILABLE"); }
    }
}
