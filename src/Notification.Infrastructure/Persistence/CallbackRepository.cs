using Microsoft.EntityFrameworkCore;
using Notification.Application.Callbacks;
using Notification.Domain.Callbacks;
using Notification.Domain.Devices;

namespace Notification.Infrastructure.Persistence;

public sealed class CallbackRepository(NotificationDbContext db) : ICallbackRepository
{
    private const int MaxAttempts = 6;

    public async Task<IReadOnlyList<ClaimedCallback>> ClaimDueAsync(DateTimeOffset now, int limit, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var due = await db.StatusEvents.FromSqlInterpolated($@"SELECT event.* FROM status_events event
            JOIN devices device ON device.id = event.device_id AND device.tenant_id = event.tenant_id
            WHERE event.status = 'pending' AND event.next_attempt_at <= {now} AND device.status = 'active'
              AND device.callback_url IS NOT NULL AND device.callback_secret_encrypted IS NOT NULL
            ORDER BY event.next_attempt_at, event.created_at, event.id LIMIT {limit} FOR UPDATE OF event SKIP LOCKED").ToListAsync(ct);
        foreach (var item in due) item.MarkSending(now);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return due.Select(x => new ClaimedCallback(x.Id, x.TenantId, x.DeviceId, x.AttemptCount)).ToArray();
    }

    public Task<CallbackWorkItem?> LoadClaimedAsync(Guid eventId, int attemptNo, CancellationToken ct) =>
        (from item in db.StatusEvents.AsNoTracking()
         join device in db.Devices.AsNoTracking() on new { item.DeviceId, item.TenantId } equals new { DeviceId = device.Id, device.TenantId }
         where item.Id == eventId && item.Status == CallbackEventStatus.Sending && item.AttemptCount == attemptNo &&
               device.Status == DeviceStatus.Active && device.CallbackUrl != null && device.CallbackSecretEncrypted != null
         select new CallbackWorkItem(item.Id, item.PublicId, item.TenantId, item.DeviceId, item.AttemptCount,
             device.CallbackUrl!, device.CallbackSecretEncrypted!, item.PayloadEncrypted, item.Status)).SingleOrDefaultAsync(ct);

    public async Task<bool> CancelClaimedAsync(Guid eventId, int attemptNo, DateTimeOffset cancelledAt, CancellationToken ct)
    {
        var statusEvent = await db.StatusEvents.SingleOrDefaultAsync(x => x.Id == eventId &&
            x.Status == CallbackEventStatus.Sending && x.AttemptCount == attemptNo, ct);
        if (statusEvent is null) return false;

        statusEvent.Cancel("CALLBACK_DISABLED", cancelledAt);
        try { return await db.SaveChangesAsync(ct) == 1; }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return false; }
    }

    public async Task<bool> CompleteAsync(CallbackWorkItem item, CallbackSendResult result, DateTimeOffset startedAt,
        DateTimeOffset finishedAt, DateTimeOffset? nextAttemptAt, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var statusEvent = await db.StatusEvents.SingleOrDefaultAsync(x => x.Id == item.EventId && x.TenantId == item.TenantId &&
            x.Status == CallbackEventStatus.Sending && x.AttemptCount == item.AttemptNo, ct);
        if (statusEvent is null) return false;
        var resultName = result.Success ? CallbackAttemptResult.Success : result.Transient ? CallbackAttemptResult.TransientFailure : CallbackAttemptResult.PermanentFailure;
        db.CallbackAttempts.Add(new(Guid.NewGuid(), item.TenantId, item.EventId, item.AttemptNo, resultName,
            result.HttpStatusCode, result.ErrorCode, startedAt, finishedAt));
        if (result.Success) statusEvent.MarkDelivered(finishedAt);
        else
        {
            var callbackActive = await db.Devices.AnyAsync(x => x.Id == item.DeviceId && x.TenantId == item.TenantId &&
                x.Status == DeviceStatus.Active && x.CallbackUrl != null && x.CallbackSecretEncrypted != null, ct);
            if (!callbackActive) statusEvent.Cancel("CALLBACK_DISABLED", finishedAt);
            else if (nextAttemptAt.HasValue) statusEvent.ScheduleRetry(nextAttemptAt.Value, finishedAt);
            else statusEvent.MarkFailed(result.ErrorCode ?? "CALLBACK_FAILED", finishedAt);
        }
        return await SaveAsync(transaction, ct);
    }

    public async Task<IReadOnlyList<ClaimedCallback>> RecoverStuckAsync(DateTimeOffset now, DateTimeOffset staleBefore,
        int limit, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var stuck = await db.StatusEvents.FromSqlInterpolated($@"SELECT * FROM status_events WHERE status = 'sending'
            AND updated_at <= {staleBefore} ORDER BY updated_at, created_at, id LIMIT {limit} FOR UPDATE SKIP LOCKED").ToListAsync(ct);
        var recovered = new List<ClaimedCallback>();
        foreach (var item in stuck)
        {
            if (item.AttemptCount is < 1 or > MaxAttempts) continue;
            db.CallbackAttempts.Add(new(Guid.NewGuid(), item.TenantId, item.Id, item.AttemptCount,
                CallbackAttemptResult.TransientFailure, null, "CALLBACK_WORKER_INTERRUPTED", item.UpdatedAt, now));
            if (item.AttemptCount == MaxAttempts) item.MarkFailed("CALLBACK_WORKER_INTERRUPTED", now);
            else item.ScheduleRetry(now, now);
            recovered.Add(new(item.Id, item.TenantId, item.DeviceId, item.AttemptCount));
        }
        return await SaveAsync(transaction, ct) ? recovered : [];
    }

    private async Task<bool> SaveAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return true; }
        catch (DbUpdateException) { await transaction.RollbackAsync(ct); db.ChangeTracker.Clear(); return false; }
    }
}
