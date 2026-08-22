using Microsoft.EntityFrameworkCore;
using Notification.Application.Alerts;
using Notification.Domain.Alerts;

namespace Notification.Infrastructure.Persistence;

public sealed class FailureAlertRepository(NotificationDbContext db) : IFailureAlertRepository
{
    public async Task<IReadOnlyList<ClaimedFailureAlert>> ClaimAsync(DateTimeOffset now, int limit, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var alerts = await db.FailureAlerts.FromSqlInterpolated($@"SELECT * FROM failure_alerts WHERE status='pending'
            AND window_end <= {now} ORDER BY window_end,created_at,id LIMIT {limit} FOR UPDATE SKIP LOCKED").ToListAsync(ct);
        var result = new List<ClaimedFailureAlert>();
        foreach (var alert in alerts)
        {
            alert.Claim(now);
            var groups = await db.FailureIncidents.AsNoTracking().Where(x => x.TenantId == alert.TenantId && x.WindowStart == alert.WindowStart)
                .OrderByDescending(x => x.OccurrenceCount).ThenBy(x => x.ErrorCode).Take(10).Select(x => new FailureGroup(x.Channel, x.ErrorCode, x.OccurrenceCount)).ToListAsync(ct);
            var total = await db.FailureIncidents.Where(x => x.TenantId == alert.TenantId && x.WindowStart == alert.WindowStart).SumAsync(x => x.OccurrenceCount, ct);
            var recipients = await db.Admins.AsNoTracking().Where(x => x.TenantId == alert.TenantId && x.DeletedAt == null).Select(x => x.Email.ToLower()).Distinct().ToListAsync(ct);
            result.Add(new(alert.Id, alert.TenantId, alert.WindowStart, alert.WindowEnd, total, groups, recipients));
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result;
    }

    public async Task<bool> CompleteAsync(Guid id, int recipients, int successes, string? code, DateTimeOffset now, CancellationToken ct)
    { var alert = await db.FailureAlerts.SingleOrDefaultAsync(x => x.Id == id && x.Status == FailureAlertStatus.Sending, ct); if (alert is null) return false; alert.Complete(recipients, successes, code, now); await db.SaveChangesAsync(ct); return true; }

    public async Task<int> RecoverAsync(DateTimeOffset staleBefore, DateTimeOffset now, int limit, CancellationToken ct)
    { var rows = await db.FailureAlerts.Where(x => x.Status == FailureAlertStatus.Sending && x.UpdatedAt <= staleBefore).OrderBy(x => x.UpdatedAt).Take(limit).ToListAsync(ct); foreach (var x in rows) x.Recover(now); await db.SaveChangesAsync(ct); return rows.Count; }
}
