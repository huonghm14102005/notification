using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Abstractions.Security;
using Notification.Application.Notifications.Delivery;
using Notification.Application.Senders;
using Notification.Domain.Callbacks;
using Notification.Domain.Devices;
using Notification.Domain.Notifications;

namespace Notification.Infrastructure.Persistence;

public sealed class DeliveryRepository(NotificationDbContext db, ISecretCipher cipher) : IDeliveryRepository
{
    private const int MaxAttempts = 4;

    public async Task<IReadOnlyList<ClaimedNotification>> ClaimDueAsync(DateTimeOffset now, int limit, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var due = await db.Deliveries.FromSqlInterpolated($@"SELECT * FROM deliveries WHERE status = 'pending'
            AND next_attempt_at <= {now} ORDER BY next_attempt_at, created_at, id LIMIT {limit} FOR UPDATE SKIP LOCKED").ToListAsync(ct);
        foreach (var delivery in due)
        {
            delivery.MarkSending(now);
            var notification = await db.Notifications.SingleAsync(x => x.Id == delivery.NotificationId && x.TenantId == delivery.TenantId, ct);
            if (notification.Status == NotificationStatus.Accepted)
                notification.SetAggregate(NotificationStatus.Processing, null, now);
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return due.Select(x => new ClaimedNotification(x.Id, x.NotificationId, x.TenantId, x.SenderId!.Value, x.AttemptCount)).ToArray();
    }

    public async Task<IReadOnlyList<RecoveredNotification>> RecoverStuckAsync(DateTimeOffset now, DateTimeOffset staleBefore, int limit, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var stuck = await db.Deliveries.FromSqlInterpolated($@"SELECT * FROM deliveries WHERE status = 'sending'
            AND updated_at <= {staleBefore} ORDER BY updated_at, created_at, id LIMIT {limit} FOR UPDATE SKIP LOCKED").ToListAsync(ct);
        var recovered = new List<RecoveredNotification>();
        foreach (var delivery in stuck)
        {
            if (delivery.AttemptCount is < 1 or > MaxAttempts || delivery.SenderId is null)
            { recovered.Add(new(delivery.Id, delivery.NotificationId, delivery.TenantId, delivery.SenderId ?? Guid.Empty, delivery.AttemptCount, false, true)); continue; }
            db.DeliveryAttempts.Add(new(Guid.NewGuid(), delivery.TenantId, delivery.Id, delivery.SenderId.Value,
                delivery.AttemptCount, DeliveryResult.TransientFailure, null, "WORKER_INTERRUPTED",
                "Delivery worker did not complete the attempt.", delivery.UpdatedAt, now));
            var terminal = delivery.AttemptCount == MaxAttempts;
            if (terminal) delivery.MarkFailed("WORKER_INTERRUPTED", now); else delivery.ScheduleRetry(now, now);
            await AggregateAsync(delivery.NotificationId, delivery.TenantId, now, ct);
            recovered.Add(new(delivery.Id, delivery.NotificationId, delivery.TenantId, delivery.SenderId.Value, delivery.AttemptCount, terminal));
        }
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return recovered; }
        catch (DbUpdateException) { await tx.RollbackAsync(ct); db.ChangeTracker.Clear(); return []; }
    }

    public Task<DeliveryWorkItem?> LoadClaimedAsync(Guid deliveryId, int attemptNo, CancellationToken ct) => db.Deliveries
        .AsNoTracking().Where(x => x.Id == deliveryId && x.Status == DeliveryStatus.Sending && x.AttemptCount == attemptNo)
        .Select(x => new DeliveryWorkItem(x.Id, x.NotificationId, x.TenantId, x.SenderId!.Value, x.AttemptCount, x.Status,
            x.Target, x.Notification.SubjectEncrypted, x.Notification.TextBodyEncrypted, x.Notification.HtmlBodyEncrypted,
            x.Sender == null ? null : new ResolvedSender(x.Sender.Id, x.Sender.TenantId, x.Sender.Key, x.Sender.Channel,
                x.Sender.Host, x.Sender.Port, x.Sender.Secure, x.Sender.Username, x.Sender.PasswordEncrypted,
                x.Sender.FromEmail, x.Sender.FromName, x.Sender.Status))).SingleOrDefaultAsync(ct);

    public Task<bool> CompleteSuccessAsync(DeliveryWorkItem item, string? providerMessageId, DateTimeOffset startedAt, DateTimeOffset finishedAt, CancellationToken ct) =>
        CompleteAsync(item, DeliveryResult.Success, providerMessageId, null, null, null, startedAt, finishedAt, ct);
    public Task<bool> CompleteTransientFailureAsync(DeliveryWorkItem item, string errorCode, string errorMessage, DateTimeOffset? nextAttemptAt, DateTimeOffset startedAt, DateTimeOffset finishedAt, CancellationToken ct) =>
        CompleteAsync(item, DeliveryResult.TransientFailure, null, errorCode, errorMessage, nextAttemptAt, startedAt, finishedAt, ct);
    public Task<bool> CompletePermanentFailureAsync(DeliveryWorkItem item, string errorCode, string errorMessage, DateTimeOffset startedAt, DateTimeOffset finishedAt, CancellationToken ct) =>
        CompleteAsync(item, DeliveryResult.PermanentFailure, null, errorCode, errorMessage, null, startedAt, finishedAt, ct);

    private async Task<bool> CompleteAsync(DeliveryWorkItem item, string result, string? providerId, string? errorCode,
        string? errorMessage, DateTimeOffset? next, DateTimeOffset started, DateTimeOffset finished, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var delivery = await db.Deliveries.SingleOrDefaultAsync(x => x.Id == item.Id && x.TenantId == item.TenantId &&
            x.Status == DeliveryStatus.Sending && x.AttemptCount == item.AttemptNo, ct);
        if (delivery is null) { await tx.RollbackAsync(ct); return false; }
        db.DeliveryAttempts.Add(new(Guid.NewGuid(), item.TenantId, item.Id, item.SenderId, item.AttemptNo, result,
            providerId, errorCode, errorMessage, started, finished));
        if (result == DeliveryResult.Success) delivery.MarkDelivered(finished);
        else if (next.HasValue) delivery.ScheduleRetry(next.Value, finished);
        else delivery.MarkFailed(errorCode!, finished);
        await AggregateAsync(item.NotificationId, item.TenantId, finished, ct);
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true; }
        catch (DbUpdateException) { await tx.RollbackAsync(ct); db.ChangeTracker.Clear(); return false; }
    }

    private async Task AggregateAsync(Guid notificationId, Guid tenantId, DateTimeOffset now, CancellationToken ct)
    {
        var notification = await db.Notifications.Include(x => x.Deliveries)
            .SingleAsync(x => x.Id == notificationId && x.TenantId == tenantId, ct);
        var states = notification.Deliveries.Select(x => x.Status).ToArray();
        var aggregate = DeliveryAggregate.Calculate(states);
        if (notification.Status != aggregate)
            notification.SetAggregate(aggregate, aggregate == NotificationStatus.Failed ? notification.Deliveries.FirstOrDefault()?.FailureCode : null, now);
        if (aggregate is NotificationStatus.Delivered or NotificationStatus.PartiallyDelivered or NotificationStatus.Failed or NotificationStatus.Cancelled)
            await AddCompletionEventAsync(notification, now, ct);
    }

    private async Task AddCompletionEventAsync(OutboundNotification notification, DateTimeOffset occurredAt, CancellationToken ct)
    {
        if (await db.StatusEvents.AnyAsync(x => x.NotificationId == notification.Id && x.EventType == "notification.completed", ct)) return;
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
            status = notification.Status,
            deliveries = notification.Deliveries.Select(x => new
            {
                deliveryId = x.Id,
                channel = x.Channel,
                targetRef = x.TargetRef,
                status = x.Status,
                attemptCount = x.AttemptCount,
                errorCode = x.FailureCode
            })
        },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        db.StatusEvents.Add(new(id, publicId, notification.TenantId, device.Id, notification.Id,
            cipher.Encrypt(payload, notification.TenantId, id), occurredAt));
    }
}
