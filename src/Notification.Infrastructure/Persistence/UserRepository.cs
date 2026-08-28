using System.Text;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Identity.Users;
using Notification.Domain.Devices;
using Notification.Domain.Identity;
using Npgsql;

namespace Notification.Infrastructure.Persistence;

public sealed class UserRepository(NotificationDbContext db) : IUserRepository
{
    public async Task<CreateUserResult> TryAddAsync(Admin user, CancellationToken ct)
    {
        db.Admins.Add(user);
        try { await db.SaveChangesAsync(ct); return CreateUserResult.Success; }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { db.Entry(user).State = EntityState.Detached; return CreateUserResult.EmailExists; }
    }

    public Task<UserItem?> GetAsync(Guid tenantId, Guid userId, CancellationToken ct) => Query(tenantId).Where(x => x.Id == userId).Select(Map()).SingleOrDefaultAsync(ct);

    public async Task<UserPage> ListAsync(Guid tenantId, string? status, int limit, DateTimeOffset? cursorCreatedAt, Guid? cursorId, CancellationToken ct)
    {
        var query = Query(tenantId); if (status is not null) query = query.Where(x => x.Status == status);
        if (cursorCreatedAt is not null && cursorId is not null) query = query.Where(x => x.CreatedAt < cursorCreatedAt || (x.CreatedAt == cursorCreatedAt && x.Id.CompareTo(cursorId.Value) < 0));
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(limit + 1).Select(Map()).ToListAsync(ct);
        string? next = null; if (rows.Count > limit) { rows.RemoveAt(rows.Count - 1); next = Cursor(rows[^1].CreatedAt, rows[^1].Id); }
        return new(rows, next);
    }

    public Task<bool> IsActiveAsync(Guid tenantId, Guid userId, CancellationToken ct) => db.Admins.AnyAsync(x => x.TenantId == tenantId && x.Id == userId && x.Status == AdminStatus.Active && x.DeletedAt == null, ct);

    public async Task<DisableUserResult> DisableAsync(Guid tenantId, Guid actorId, Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        if (actorId == userId) return DisableUserResult.CannotDisableSelf;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = await db.Admins.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == userId && x.Role == AdminRole.Member && x.DeletedAt == null, ct);
        if (user is null) return DisableUserResult.NotFound;
        user.Disable(now);
        await db.RefreshTokens.Where(x => x.AdminId == userId && x.RevokedAt == null).ExecuteUpdateAsync(x => x.SetProperty(t => t.RevokedAt, now), ct);
        await db.Devices.Where(x => x.TenantId == tenantId && x.OwnerAdminId == userId && x.Status == DeviceStatus.Active)
            .ExecuteUpdateAsync(x => x.SetProperty(d => d.Status, DeviceStatus.Disabled).SetProperty(d => d.DisabledAt, now).SetProperty(d => d.UpdatedAt, now), ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return DisableUserResult.Success;
    }

    private IQueryable<Admin> Query(Guid tenantId) => db.Admins.AsNoTracking().Where(x => x.TenantId == tenantId && x.DeletedAt == null);
    private System.Linq.Expressions.Expression<Func<Admin, UserItem>> Map() => x => new(x.Id, x.Email, x.DisplayName, x.Role, x.Status,
        db.Devices.Count(d => d.OwnerAdminId == x.Id), db.Devices.Count(d => d.OwnerAdminId == x.Id && d.Status == DeviceStatus.Active), x.CreatedAt, x.UpdatedAt, x.DisabledAt);
    private static string Cursor(DateTimeOffset createdAt, Guid id) => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{createdAt:O}|{id}"));
}
