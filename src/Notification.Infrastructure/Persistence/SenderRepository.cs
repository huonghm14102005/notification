using System.Text;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Senders;
using Notification.Domain.Senders;
using Npgsql;

namespace Notification.Infrastructure.Persistence;

public sealed class SenderRepository(NotificationDbContext db) : ISenderRepository
{
    public Task<bool> KeyExistsAsync(Guid tenantId, string key, CancellationToken ct) => db.Senders.AnyAsync(x => x.TenantId == tenantId && x.Key == key, ct);
    public async Task AddAsync(Sender sender, CancellationToken ct) { db.Senders.Add(sender); try { await db.SaveChangesAsync(ct); } catch (DbUpdateException e) when (e.InnerException is PostgresException { ConstraintName: "ux_senders_tenant_key" }) { throw new SenderOperationException("SENDER_KEY_EXISTS"); } }
    public async Task<SenderPage> ListAsync(Guid tenantId, int limit, DateTimeOffset? at, Guid? id, CancellationToken ct)
    {
        var q = db.Senders.AsNoTracking().Where(x => x.TenantId == tenantId); if (at is not null && id is not null) q = q.Where(x => x.CreatedAt < at || (x.CreatedAt == at && x.Id.CompareTo(id.Value) < 0));
        var rows = await q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(limit + 1).ToListAsync(ct); string? next = null;
        if (rows.Count > limit) { rows.RemoveAt(rows.Count - 1); var last = rows[^1]; next = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{last.CreatedAt:O}|{last.Id}")); }
        return new(rows.Select(SenderHandlers.Map).ToList(), next);
    }
    public Task<Sender?> FindAsync(Guid tenantId, Guid id, CancellationToken ct) => db.Senders.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
