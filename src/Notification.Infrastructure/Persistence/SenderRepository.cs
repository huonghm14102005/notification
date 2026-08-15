using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

    public async Task SaveUpdateAsync(Guid tenantId, Sender sender, bool? isDefault, DateTimeOffset now, CancellationToken ct)
    {
        if (isDefault is null) { await db.SaveChangesAsync(ct); return; }

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                if (attempt > 1) db.Entry(sender).State = EntityState.Modified;
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                await using (var command = db.Database.GetDbConnection().CreateCommand())
                {
                    command.Transaction = transaction.GetDbTransaction();
                    command.CommandText = "SELECT 1 FROM tenants WHERE id = @tenant_id FOR UPDATE";
                    command.Parameters.Add(new NpgsqlParameter<Guid>("tenant_id", tenantId));
                    if (await command.ExecuteScalarAsync(ct) is null) throw new SenderOperationException("NOT_FOUND");
                }

                if (isDefault.Value)
                {
                    await db.Senders
                        .Where(x => x.TenantId == tenantId && x.Id != sender.Id && x.IsDefault)
                        .ExecuteUpdateAsync(update => update
                            .SetProperty(x => x.IsDefault, false)
                            .SetProperty(x => x.UpdatedAt, now), ct);
                }

                sender.SetDefault(isDefault.Value, now);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return;
            }
            catch (Exception exception) when (attempt < 3 && IsRetryable(exception))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), ct);
            }
        }
    }

    public Task<ResolvedSender?> ResolveAsync(Guid tenantId, string? key, CancellationToken ct) => db.Senders
        .AsNoTracking()
        .Where(x => x.TenantId == tenantId && x.Status == SenderStatus.Active && (key == null ? x.IsDefault : x.Key == key))
        .Select(x => new ResolvedSender(x.Id, x.TenantId, x.Key, x.Channel, x.Host, x.Port, x.Secure, x.Username, x.PasswordEncrypted, x.FromEmail, x.FromName))
        .SingleOrDefaultAsync(ct);

    public Task<ResolvedSender?> FindResolvedByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => db.Senders
        .AsNoTracking()
        .Where(x => x.TenantId == tenantId && x.Id == id)
        .Select(x => new ResolvedSender(x.Id, x.TenantId, x.Key, x.Channel, x.Host, x.Port, x.Secure, x.Username, x.PasswordEncrypted, x.FromEmail, x.FromName, x.Status))
        .SingleOrDefaultAsync(ct);

    public async Task<bool> MarkVerifiedAsync(ResolvedSender snapshot, DateTimeOffset now, CancellationToken ct) => await db.Senders
        .Where(x => x.TenantId == snapshot.TenantId && x.Id == snapshot.Id && x.Status == SenderStatus.Active
            && x.Host == snapshot.Host && x.Port == snapshot.Port && x.Secure == snapshot.Secure
            && x.Username == snapshot.Username && x.PasswordEncrypted == snapshot.PasswordEncrypted)
        .ExecuteUpdateAsync(update => update.SetProperty(x => x.VerifiedAt, now).SetProperty(x => x.UpdatedAt, now), ct) == 1;

    private static bool IsRetryable(Exception exception) => exception switch
    {
        PostgresException { SqlState: "40001" or "40P01" } => true,
        DbUpdateException { InnerException: PostgresException { SqlState: "40001" or "40P01" } } => true,
        _ => false,
    };
}
