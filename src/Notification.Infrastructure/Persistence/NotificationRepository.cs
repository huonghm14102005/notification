using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Abstractions.Security;
using Notification.Application.Notifications;
using Notification.Domain.Callbacks;
using Notification.Domain.Devices;
using Notification.Domain.Identity;
using Notification.Domain.Notifications;
using Notification.Domain.Senders;

namespace Notification.Infrastructure.Persistence;

public sealed class NotificationRepository(NotificationDbContext db, ISecretCipher cipher) : INotificationRepository
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

    public async Task<ManualRetryResult> RetryAsync(Guid tenantId, Guid adminId, Guid notificationId,
        DateTimeOffset now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var source = await db.Notifications.FromSqlInterpolated(
            $"SELECT * FROM notifications WHERE tenant_id = {tenantId} AND id = {notificationId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (source is null) throw new NotificationOperationException("NOT_FOUND");
        var existing = await db.NotificationManualActions.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.SourceNotificationId == notificationId && x.Action == NotificationManualActionType.Retry)
            .Select(x => new { x.ResultNotificationId, x.CreatedAt }).SingleOrDefaultAsync(ct);
        if (existing?.ResultNotificationId is Guid resultId)
        {
            await tx.CommitAsync(ct);
            return new(false, resultId, notificationId, NotificationStatus.Accepted, existing.CreatedAt);
        }
        if (source.Status is not (NotificationStatus.Failed or NotificationStatus.PartiallyDelivered))
            throw new NotificationOperationException("INVALID_STATE");
        var failed = await db.Deliveries.FromSqlInterpolated(
            $"SELECT * FROM deliveries WHERE tenant_id = {tenantId} AND notification_id = {notificationId} FOR UPDATE")
            .Where(x => x.Status == DeliveryStatus.Failed).ToListAsync(ct);
        if (failed.Count == 0) throw new NotificationOperationException("INVALID_STATE");
        var senderIds = failed.Select(x => x.SenderId).OfType<Guid>().Distinct().ToArray();
        var activeSenders = await db.Senders.CountAsync(x => x.TenantId == tenantId && senderIds.Contains(x.Id) && x.Status == SenderStatus.Active, ct);
        if (failed.Any(x => x.SenderId is null) || activeSenders != senderIds.Length)
            throw new NotificationOperationException("SENDER_UNAVAILABLE");
        var result = new OutboundNotification(Guid.NewGuid(), tenantId, source.ApiKeyId, source.TemplateId,
            source.SubjectEncrypted.ToArray(), source.TextBodyEncrypted?.ToArray(), source.HtmlBodyEncrypted?.ToArray(), now);
        db.Notifications.Add(result);
        foreach (var item in failed)
            db.Deliveries.Add(new Delivery(Guid.NewGuid(), tenantId, result.Id, item.SenderId!.Value, item.Target, item.TargetRef, now));
        db.NotificationManualActions.Add(new(Guid.NewGuid(), tenantId, adminId, notificationId, result.Id,
            NotificationManualActionType.Retry, now));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(true, result.Id, notificationId, result.Status, now);
    }

    public async Task CancelAsync(Guid tenantId, Guid adminId, Guid notificationId, DateTimeOffset now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (!await db.Notifications.AsNoTracking().AnyAsync(x => x.TenantId == tenantId && x.Id == notificationId, ct))
            throw new NotificationOperationException("NOT_FOUND");
        var existing = await db.NotificationManualActions.AnyAsync(x => x.TenantId == tenantId
            && x.SourceNotificationId == notificationId && x.Action == NotificationManualActionType.Cancel, ct);
        if (existing) { await tx.CommitAsync(ct); return; }
        var deliveries = await db.Deliveries.FromSqlInterpolated(
            $"SELECT * FROM deliveries WHERE tenant_id = {tenantId} AND notification_id = {notificationId} FOR UPDATE")
            .ToListAsync(ct);
        var notification = await db.Notifications.FromSqlInterpolated(
            $"SELECT * FROM notifications WHERE tenant_id = {tenantId} AND id = {notificationId} FOR UPDATE")
            .SingleAsync(ct);
        if (notification.Status != NotificationStatus.Accepted) throw new NotificationOperationException("INVALID_STATE");
        if (deliveries.Count == 0 || deliveries.Any(x => x.Status != DeliveryStatus.Pending || x.AttemptCount != 0))
            throw new NotificationOperationException("INVALID_STATE");
        foreach (var delivery in deliveries) delivery.Cancel(now);
        notification.SetAggregate(NotificationStatus.Cancelled, null, now);
        db.NotificationManualActions.Add(new(Guid.NewGuid(), tenantId, adminId, notificationId, null,
            NotificationManualActionType.Cancel, now));
        await AddCompletionEventAsync(notification, deliveries, now, ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    private async Task AddCompletionEventAsync(OutboundNotification notification, IReadOnlyCollection<Delivery> deliveries,
        DateTimeOffset occurredAt, CancellationToken ct)
    {
        var device = await db.ApiKeys.Where(x => x.Id == notification.ApiKeyId && x.TenantId == notification.TenantId)
            .Select(x => x.Device).SingleAsync(ct);
        if (device.Status != DeviceStatus.Active || device.CallbackUrl is null || device.CallbackSecretEncrypted is null) return;
        var id = Guid.NewGuid(); var publicId = $"evt_{id:N}";
        var payload = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            eventId = publicId,
            type = "notification.completed",
            occurredAt,
            notificationId = notification.Id,
            status = NotificationStatus.Cancelled,
            deliveries = deliveries.Select(x => new
            {
                deliveryId = x.Id,
                channel = x.Channel,
                targetRef = x.TargetRef,
                status = DeliveryStatus.Cancelled,
                attemptCount = 0,
                errorCode = (string?)null
            })
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        db.StatusEvents.Add(new(id, publicId, notification.TenantId, device.Id, notification.Id,
            cipher.Encrypt(payload, notification.TenantId, id), occurredAt));
    }

    public async Task<(Guid ApiKeyId, Guid SourceDeviceId)> EnsureAdminDispatchContextAsync(Guid tenantId, Guid adminId, DateTimeOffset now, CancellationToken ct)
    {
        var existingKey = await db.ApiKeys
            .Include(x => x.Device)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.RevokedAt == null, ct);

        if (existingKey is not null)
        {
            return (existingKey.Id, existingKey.DeviceId);
        }

        var device = new Device(Guid.NewGuid(), tenantId, adminId, "Web Admin Console", DeviceRole.Both, now);
        var key = new ApiKey(Guid.NewGuid(), tenantId, adminId, device.Id, "Admin Playground", "admin_key", [0], now);
        db.Devices.Add(device);
        db.ApiKeys.Add(key);
        await db.SaveChangesAsync(ct);
        return (key.Id, device.Id);
    }
}
