using System.Text;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Identity.Abstractions;
using Notification.Application.Identity.ApiKeys;
using Notification.Application.Identity.RegisterTenant;
using Notification.Domain.Identity;
using Npgsql;

namespace Notification.Infrastructure.Persistence;

public sealed class IdentityRepository(NotificationDbContext dbContext) : IIdentityRepository
{
    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct) => dbContext.Tenants.AnyAsync(x => x.Slug == slug && x.DeletedAt == null, ct);
    public Task<bool> EmailExistsAsync(string email, CancellationToken ct) => dbContext.Admins.AnyAsync(x => x.Email == email && x.DeletedAt == null, ct);
    public async Task AddRegistrationAsync(Tenant tenant, Admin admin, CancellationToken ct)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        dbContext.Tenants.Add(tenant);
        dbContext.Admins.Add(admin);
        try
        {
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres)
        {
            throw postgres.ConstraintName switch
            {
                "ux_tenants_slug_active" => new RegistrationConflictException("TENANT_SLUG_EXISTS"),
                "ux_admins_email_active" => new RegistrationConflictException("ADMIN_EMAIL_EXISTS"),
                _ => exception,
            };
        }
    }

    public Task<Admin?> FindActiveAdminByEmailAsync(string email, CancellationToken ct) =>
        dbContext.Admins.Include(x => x.Tenant).SingleOrDefaultAsync(x => x.Email == email && x.DeletedAt == null && x.Tenant.DeletedAt == null, ct);

    public async Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken ct)
    {
        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<RefreshRotationResult?> RotateRefreshTokenAsync(byte[] currentHash, RefreshToken replacement, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        var current = await dbContext.RefreshTokens.AsNoTracking().Include(x => x.Admin).ThenInclude(x => x.Tenant)
            .SingleOrDefaultAsync(x => x.TokenHash == currentHash, ct);
        if (current is null || current.RevokedAt is not null || current.ExpiresAt <= now || current.Admin.DeletedAt is not null || current.Admin.Tenant.DeletedAt is not null) return null;
        dbContext.RefreshTokens.Add(new RefreshToken(replacement.Id, current.AdminId, current.FamilyId, replacement.TokenHash, replacement.CreatedAt, replacement.ExpiresAt));
        await dbContext.SaveChangesAsync(ct);
        var rows = await dbContext.RefreshTokens.Where(x => x.Id == current.Id && x.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.RevokedAt, now).SetProperty(x => x.ReplacedById, replacement.Id), ct);
        if (rows != 1)
        {
            await transaction.RollbackAsync(ct);
            return null;
        }
        await transaction.CommitAsync(ct);
        return new(current.AdminId, current.Admin.TenantId, current.Admin.Role);
    }

    public async Task<LogoutResult> RevokeRefreshTokenAsync(byte[] tokenHash, Guid adminId, DateTimeOffset now, CancellationToken ct)
    {
        var token = await dbContext.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == tokenHash, ct);
        if (token is null || token.AdminId != adminId) return LogoutResult.Invalid;
        if (token.RevokedAt is not null) return LogoutResult.Success;
        await dbContext.RefreshTokens.Where(x => x.Id == token.Id && x.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.RevokedAt, now), ct);
        return LogoutResult.Success;
    }

    public async Task<bool> TryAddApiKeyAsync(ApiKey apiKey, int activeLimit, CancellationToken ct)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({apiKey.TenantId.ToString()}, 0))", ct);
        if (await dbContext.ApiKeys.CountAsync(x => x.TenantId == apiKey.TenantId && x.Status == ApiKeyStatus.Active, ct) >= activeLimit) return false;
        dbContext.ApiKeys.Add(apiKey); await dbContext.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return true;
    }

    public async Task<ApiKeyPage> ListApiKeysAsync(Guid tenantId, int limit, DateTimeOffset? cursorCreatedAt, Guid? cursorId, CancellationToken ct)
    {
        var query = dbContext.ApiKeys.AsNoTracking().Where(x => x.TenantId == tenantId);
        if (cursorCreatedAt is not null && cursorId is not null)
            query = query.Where(x => x.CreatedAt < cursorCreatedAt || (x.CreatedAt == cursorCreatedAt && x.Id.CompareTo(cursorId.Value) < 0));
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(limit + 1)
            .Select(x => new ApiKeyItem(x.Id, x.ProducerName, x.KeyPrefix, x.Status, x.LastUsedAt, x.CreatedAt, x.RevokedAt)).ToListAsync(ct);
        string? next = null;
        if (rows.Count > limit)
        {
            rows.RemoveAt(rows.Count - 1); var last = rows[^1];
            next = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{last.CreatedAt:O}|{last.Id}"));
        }
        return new(rows, next);
    }

    public async Task<bool> RevokeApiKeyAsync(Guid tenantId, Guid id, DateTimeOffset now, CancellationToken ct)
    {
        var exists = await dbContext.ApiKeys.AnyAsync(x => x.TenantId == tenantId && x.Id == id, ct); if (!exists) return false;
        await dbContext.ApiKeys.Where(x => x.TenantId == tenantId && x.Id == id && x.Status == ApiKeyStatus.Active)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, ApiKeyStatus.Revoked).SetProperty(x => x.RevokedAt, now), ct);
        return true;
    }

    public Task<ApiKeyIdentity?> FindActiveApiKeyAsync(string prefix, CancellationToken ct) => dbContext.ApiKeys.AsNoTracking()
        .Where(x => x.KeyPrefix == prefix && x.Status == ApiKeyStatus.Active && x.Tenant.DeletedAt == null)
        .Select(x => new ApiKeyIdentity(x.Id, x.TenantId, x.ProducerName, x.KeyHash, x.LastUsedAt)).SingleOrDefaultAsync(ct);

    public Task TouchApiKeyAsync(Guid id, DateTimeOffset now, TimeSpan interval, CancellationToken ct) => dbContext.ApiKeys
        .Where(x => x.Id == id && (x.LastUsedAt == null || x.LastUsedAt < now - interval))
        .ExecuteUpdateAsync(update => update.SetProperty(x => x.LastUsedAt, now), ct);
}
