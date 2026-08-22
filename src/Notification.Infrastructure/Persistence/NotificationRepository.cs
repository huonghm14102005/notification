using Microsoft.EntityFrameworkCore;
using Notification.Application.Notifications;
using Notification.Domain.Notifications;

namespace Notification.Infrastructure.Persistence;

public sealed class NotificationRepository(NotificationDbContext db) : INotificationRepository
{
    public async Task AddAsync(OutboundNotification notification, Delivery delivery, CancellationToken ct)
    {
        db.Notifications.Add(notification); db.Deliveries.Add(delivery); await db.SaveChangesAsync(ct);
    }

    public async Task<NotificationListPage> ListAsync(NotificationListQuery query, CancellationToken ct)
    {
        var values = db.Notifications.AsNoTracking().Where(x => x.TenantId == query.TenantId);
        if (query.Caller.Type == NotificationCallerType.ApiKey)
            values = values.Where(x => x.ApiKeyId == query.Caller.ApiKeyId);
        if (query.Filter.Status is not null) values = values.Where(x => x.Status == query.Filter.Status);
        if (query.Filter.Channel is not null)
            values = values.Where(x => x.Deliveries.Any(d => d.Channel == query.Filter.Channel));
        if (query.Filter.From.HasValue) values = values.Where(x => x.CreatedAt >= query.Filter.From.Value);
        if (query.Filter.To.HasValue) values = values.Where(x => x.CreatedAt < query.Filter.To.Value);
        if (query.Filter.SourceDeviceId.HasValue)
            values = values.Where(x => x.ApiKey.DeviceId == query.Filter.SourceDeviceId.Value);
        if (query.Filter.ApiKeyId.HasValue) values = values.Where(x => x.ApiKeyId == query.Filter.ApiKeyId.Value);
        if (query.CursorCreatedAt.HasValue && query.CursorId.HasValue)
            values = values.Where(x => x.CreatedAt < query.CursorCreatedAt.Value
                || x.CreatedAt == query.CursorCreatedAt.Value && x.Id.CompareTo(query.CursorId.Value) < 0);

        var rows = await values.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Take(query.Limit + 1).Select(x => new
            {
                x.Id,
                SourceDeviceId = x.ApiKey.DeviceId,
                x.ApiKeyId,
                x.ApiKey.ProducerName,
                x.Status,
                x.CreatedAt,
                x.UpdatedAt,
                x.CompletedAt
            }).ToListAsync(ct);
        var hasMore = rows.Count > query.Limit;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        var ids = rows.Select(x => x.Id).ToArray();
        var deliveries = await db.Deliveries.AsNoTracking().Where(x => x.TenantId == query.TenantId && ids.Contains(x.NotificationId))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => new
            {
                x.NotificationId,
                Item = new NotificationDeliveryListItem(x.Id, x.Channel, x.Target, x.TargetRef, x.Status,
                    x.AttemptCount, x.FailureCode)
            }).ToListAsync(ct);
        var grouped = deliveries.GroupBy(x => x.NotificationId).ToDictionary(x => x.Key,
            x => (IReadOnlyList<NotificationDeliveryListItem>)x.Select(v => v.Item).ToArray());
        var items = rows.Select(x => new NotificationListItem(x.Id, x.SourceDeviceId, x.ApiKeyId, x.ProducerName,
            x.Status, x.CreatedAt, x.UpdatedAt, x.CompletedAt,
            grouped.GetValueOrDefault(x.Id, []))).ToArray();
        var next = hasMore && items.Length > 0 ? NotificationListCursor.Encode(items[^1].CreatedAt, items[^1].Id) : null;
        return new(items, next);
    }

    public async Task<NotificationWithAttempts?> GetWithAttemptsAsync(Guid tenantId, Guid notificationId, CancellationToken ct)
    {
        var notification = await db.Notifications.AsNoTracking()
            .Where(n => n.TenantId == tenantId && n.Id == notificationId)
            .Select(n => new
            {
                n.Id,
                n.TenantId,
                n.ApiKeyId,
                n.ApiKey.ProducerName,
                SenderKey = n.Deliveries.Select(d => d.Sender!.Key).First(),
                n.Status,
                RecipientEmail = n.Deliveries.Select(d => d.Target).First(),
                RecipientRef = n.Deliveries.Select(d => d.TargetRef).First(),
                n.SubjectEncrypted,
                n.TextBodyEncrypted,
                n.HtmlBodyEncrypted,
                n.CreatedAt,
                SentAt = n.CompletedAt,
                n.UpdatedAt,
                n.FailureReason
            }).SingleOrDefaultAsync(ct);

        if (notification is null) return null;

        var attempts = await db.DeliveryAttempts.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.Delivery.NotificationId == notificationId)
            .OrderBy(a => a.AttemptNo)
            .Take(4)
            .Select(a => new DeliveryAttemptDetail(a.AttemptNo, a.Result, a.StartedAt, a.FinishedAt, a.ErrorCode,
                a.ErrorMessage, a.ProviderMessageId))
            .ToListAsync(ct);

        return new(notification.Id, notification.TenantId, notification.ApiKeyId, notification.ProducerName,
            notification.SenderKey, notification.Status, notification.RecipientEmail, notification.RecipientRef,
            notification.SubjectEncrypted, notification.TextBodyEncrypted, notification.HtmlBodyEncrypted,
            notification.CreatedAt, notification.SentAt,
            notification.UpdatedAt, notification.FailureReason, attempts);
    }
}
