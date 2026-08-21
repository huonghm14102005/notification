using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Notification.Application.Abstractions.Security;
using Notification.Application.Notifications.Delivery;
using Notification.Domain.Callbacks;
using Notification.Domain.Devices;
using Notification.Application.Senders;
using Notification.Domain.Notifications;

namespace Notification.Infrastructure.Persistence;

public sealed class DeliveryRepository(NotificationDbContext db, ISecretCipher cipher) : IDeliveryRepository
{
    private const int MaxDeliveryAttempts = 4;

    public async Task<IReadOnlyList<ClaimedNotification>> ClaimDueAsync(DateTimeOffset now, int limit, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var due = await db.Notifications.FromSqlInterpolated($@"SELECT * FROM notifications
            WHERE status = 'accepted' AND next_attempt_at <= {now}
            ORDER BY next_attempt_at, created_at, id LIMIT {limit} FOR UPDATE SKIP LOCKED").ToListAsync(ct);
        foreach (var item in due) item.MarkSending(now);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return due.Select(x => new ClaimedNotification(x.Id, x.TenantId, x.SenderId, x.AttemptCount)).ToArray();
    }

    public async Task<IReadOnlyList<RecoveredNotification>> RecoverStuckAsync(DateTimeOffset now, DateTimeOffset staleBefore,
        int limit, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var stuck = await db.Notifications.FromSqlInterpolated($@"SELECT * FROM notifications
            WHERE status = 'sending' AND updated_at <= {staleBefore}
            ORDER BY updated_at, created_at, id LIMIT {limit} FOR UPDATE SKIP LOCKED").ToListAsync(ct);
        var recovered = new List<RecoveredNotification>(stuck.Count);
        foreach (var notification in stuck)
        {
            if (notification.AttemptCount is < 1 or > MaxDeliveryAttempts)
            {
                recovered.Add(new(notification.Id, notification.TenantId, notification.SenderId,
                    notification.AttemptCount, false, true));
                continue;
            }

            db.DeliveryAttempts.Add(new(Guid.NewGuid(), notification.TenantId, notification.Id, notification.SenderId,
                notification.AttemptCount, DeliveryResult.TransientFailure, null, "WORKER_INTERRUPTED",
                "Delivery worker did not complete the attempt.", notification.UpdatedAt, now));
            var terminal = notification.AttemptCount == MaxDeliveryAttempts;
            if (terminal) { notification.MarkFailed("WORKER_INTERRUPTED", now); await AddCompletionEventAsync(notification, now, ct); }
            else notification.ScheduleRetry(now, now);
            recovered.Add(new(notification.Id, notification.TenantId, notification.SenderId,
                notification.AttemptCount, terminal));
        }

        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return recovered;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return [];
        }
    }

    public Task<DeliveryWorkItem?> LoadClaimedAsync(Guid notificationId, int attemptNo, CancellationToken ct) => db.Notifications
        .AsNoTracking().Where(x => x.Id == notificationId && x.Status == NotificationStatus.Sending && x.AttemptCount == attemptNo)
        .Select(x => new DeliveryWorkItem(x.Id, x.TenantId, x.SenderId, x.AttemptCount, x.Status, x.RecipientEmail,
            x.SubjectEncrypted, x.BodyEncrypted, x.Sender == null ? null : new ResolvedSender(x.Sender.Id, x.Sender.TenantId,
                x.Sender.Key, x.Sender.Channel, x.Sender.Host, x.Sender.Port, x.Sender.Secure, x.Sender.Username,
                x.Sender.PasswordEncrypted, x.Sender.FromEmail, x.Sender.FromName, x.Sender.Status))).SingleOrDefaultAsync(ct);

    public Task<bool> CompleteSuccessAsync(DeliveryWorkItem item, string? providerMessageId, DateTimeOffset startedAt, DateTimeOffset finishedAt, CancellationToken ct) =>
        CompleteAsync(item, DeliveryResult.Success, providerMessageId, null, null, null, startedAt, finishedAt, ct);

    public Task<bool> CompleteTransientFailureAsync(DeliveryWorkItem item, string errorCode, string errorMessage,
        DateTimeOffset? nextAttemptAt, DateTimeOffset startedAt, DateTimeOffset finishedAt, CancellationToken ct) =>
        CompleteAsync(item, DeliveryResult.TransientFailure, null, errorCode, errorMessage, nextAttemptAt, startedAt, finishedAt, ct);

    public Task<bool> CompletePermanentFailureAsync(DeliveryWorkItem item, string errorCode, string errorMessage,
        DateTimeOffset startedAt, DateTimeOffset finishedAt, CancellationToken ct) =>
        CompleteAsync(item, DeliveryResult.PermanentFailure, null, errorCode, errorMessage, null, startedAt, finishedAt, ct);

    private async Task<bool> CompleteAsync(DeliveryWorkItem item, string result, string? providerId, string? errorCode,
        string? errorMessage, DateTimeOffset? nextAttemptAt, DateTimeOffset startedAt, DateTimeOffset finishedAt, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var notification = await db.Notifications.SingleOrDefaultAsync(x => x.Id == item.Id && x.TenantId == item.TenantId
            && x.Status == NotificationStatus.Sending && x.AttemptCount == item.AttemptNo, ct);
        if (notification is null) { await transaction.RollbackAsync(ct); return false; }
        db.DeliveryAttempts.Add(new(Guid.NewGuid(), item.TenantId, item.Id, item.SenderId, item.AttemptNo, result,
            providerId, errorCode, errorMessage, startedAt, finishedAt));
        if (result == DeliveryResult.Success) notification.MarkSent(finishedAt);
        else if (nextAttemptAt.HasValue) notification.ScheduleRetry(nextAttemptAt.Value, finishedAt);
        else notification.MarkFailed(errorCode!, finishedAt);
        if (result == DeliveryResult.Success || !nextAttemptAt.HasValue)
            await AddCompletionEventAsync(notification, finishedAt, ct);
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return true; }
        catch (DbUpdateException) { await transaction.RollbackAsync(ct); db.ChangeTracker.Clear(); return false; }
    }

    private async Task AddCompletionEventAsync(OutboundNotification notification, DateTimeOffset occurredAt, CancellationToken ct)
    {
        var device = await db.ApiKeys.Where(x => x.Id == notification.ApiKeyId && x.TenantId == notification.TenantId)
            .Select(x => x.Device).SingleAsync(ct);
        if (device.Status != DeviceStatus.Active || device.CallbackUrl is null || device.CallbackSecretEncrypted is null) return;
        var id = Guid.NewGuid(); var publicId = $"evt_{id:N}";
        var delivered = notification.Status == NotificationStatus.Sent;
        var payload = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            eventId = publicId,
            type = "notification.completed",
            occurredAt,
            notificationId = notification.Id,
            status = delivered ? "delivered" : "failed",
            deliveries = new[] { new { channel = "email", status = delivered ? "delivered" : "failed", attemptCount = notification.AttemptCount, errorCode = delivered ? null : notification.FailureReason } },
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        db.StatusEvents.Add(new(id, publicId, notification.TenantId, device.Id, notification.Id,
            cipher.Encrypt(payload, notification.TenantId, id), occurredAt));
    }
}
