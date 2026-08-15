using Microsoft.EntityFrameworkCore;
using Notification.Application.Notifications.Delivery;
using Notification.Application.Senders;
using Notification.Domain.Notifications;

namespace Notification.Infrastructure.Persistence;

public sealed class DeliveryRepository(NotificationDbContext db) : IDeliveryRepository
{
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

    public Task<DeliveryWorkItem?> LoadClaimedAsync(Guid notificationId, int attemptNo, CancellationToken ct) => db.Notifications
        .AsNoTracking().Where(x => x.Id == notificationId && x.Status == NotificationStatus.Sending && x.AttemptCount == attemptNo)
        .Select(x => new DeliveryWorkItem(x.Id, x.TenantId, x.SenderId, x.AttemptCount, x.Status, x.RecipientEmail,
            x.SubjectEncrypted, x.BodyEncrypted, x.Sender == null ? null : new ResolvedSender(x.Sender.Id, x.Sender.TenantId,
                x.Sender.Key, x.Sender.Channel, x.Sender.Host, x.Sender.Port, x.Sender.Secure, x.Sender.Username,
                x.Sender.PasswordEncrypted, x.Sender.FromEmail, x.Sender.FromName, x.Sender.Status))).SingleOrDefaultAsync(ct);

    public Task<bool> CompleteSuccessAsync(DeliveryWorkItem item, string? providerMessageId, DateTimeOffset startedAt, DateTimeOffset finishedAt, CancellationToken ct) =>
        CompleteAsync(item, DeliveryResult.Success, providerMessageId, null, null, startedAt, finishedAt, ct);

    public Task<bool> CompleteFailureAsync(DeliveryWorkItem item, string errorCode, string errorMessage, DateTimeOffset startedAt, DateTimeOffset finishedAt, CancellationToken ct) =>
        CompleteAsync(item, DeliveryResult.PermanentFailure, null, errorCode, errorMessage, startedAt, finishedAt, ct);

    private async Task<bool> CompleteAsync(DeliveryWorkItem item, string result, string? providerId, string? errorCode,
        string? errorMessage, DateTimeOffset startedAt, DateTimeOffset finishedAt, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var notification = await db.Notifications.SingleOrDefaultAsync(x => x.Id == item.Id && x.TenantId == item.TenantId
            && x.Status == NotificationStatus.Sending && x.AttemptCount == item.AttemptNo, ct);
        if (notification is null) { await transaction.RollbackAsync(ct); return false; }
        db.DeliveryAttempts.Add(new(Guid.NewGuid(), item.TenantId, item.Id, item.SenderId, item.AttemptNo, result,
            providerId, errorCode, errorMessage, startedAt, finishedAt));
        if (result == DeliveryResult.Success) notification.MarkSent(finishedAt); else notification.MarkFailed(errorCode!, finishedAt);
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return true; }
        catch (DbUpdateException) { await transaction.RollbackAsync(ct); db.ChangeTracker.Clear(); return false; }
    }
}
