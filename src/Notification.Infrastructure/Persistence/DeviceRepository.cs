using System.Text;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Devices;
using Notification.Domain.Devices;
using Notification.Domain.Identity;

namespace Notification.Infrastructure.Persistence;

public sealed class DeviceRepository(NotificationDbContext db) : IDeviceRepository
{
    public async Task AddAsync(Device device, CancellationToken ct) { db.Devices.Add(device); await db.SaveChangesAsync(ct); }

    public async Task<DeviceItem> GetOrCreateLegacyAsync(Guid tenantId, Guid actorId, string producerName, DateTimeOffset now, CancellationToken ct)
    {
        var normalized = producerName.Trim().ToLowerInvariant(); var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO devices (id, tenant_id, owner_admin_id, name, normalized_legacy_name, role, status, created_at, updated_at)
VALUES ({id}, {tenantId}, {actorId}, {producerName.Trim()}, {normalized}, 'source', 'active', {now}, {now})
ON CONFLICT (tenant_id, normalized_legacy_name) WHERE normalized_legacy_name IS NOT NULL DO NOTHING", ct);
        return await db.Devices.AsNoTracking().Where(x => x.TenantId == tenantId && x.NormalizedLegacyName == normalized).Select(Map()).SingleAsync(ct);
    }

    public Task<DeviceItem?> GetAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, CancellationToken ct) => Query(tenantId, actorId, tenantScope)
        .Where(x => x.Id == deviceId).Select(Map()).SingleOrDefaultAsync(ct);

    public async Task<DevicePage> ListAsync(Guid tenantId, Guid actorId, bool tenantScope, string? status, int limit, DateTimeOffset? cursorCreatedAt, Guid? cursorId, CancellationToken ct)
    {
        var query = Query(tenantId, actorId, tenantScope);
        if (status is not null) query = query.Where(x => x.Status == status);
        if (cursorCreatedAt is not null && cursorId is not null) query = query.Where(x => x.CreatedAt < cursorCreatedAt || (x.CreatedAt == cursorCreatedAt && x.Id.CompareTo(cursorId.Value) < 0));
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(limit + 1).Select(Map()).ToListAsync(ct);
        return Page(rows, limit);
    }

    public async Task<DeviceItem?> RenameAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, string name, DateTimeOffset now, CancellationToken ct)
    {
        var device = await Query(tenantId, actorId, tenantScope, tracking: true).SingleOrDefaultAsync(x => x.Id == deviceId, ct);
        if (device is null) return null; device.Rename(name, now); await db.SaveChangesAsync(ct); return ToItem(device);
    }

    public async Task<bool> DisableAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var device = await Query(tenantId, actorId, tenantScope, tracking: true).SingleOrDefaultAsync(x => x.Id == deviceId, ct);
        if (device is null) return false; device.Disable(now);
        await db.StatusEvents.Where(x => x.TenantId == tenantId && x.DeviceId == deviceId && x.Status == "pending")
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "cancelled").SetProperty(x => x.FailureCode, "DEVICE_DISABLED")
                .SetProperty(x => x.NextAttemptAt, (DateTimeOffset?)null).SetProperty(x => x.UpdatedAt, now), ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return true;
    }

    public async Task<bool> ConfigureCallbackAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId,
        string url, byte[] secretEncrypted, DateTimeOffset now, CancellationToken ct)
    {
        var device = await Query(tenantId, actorId, tenantScope, tracking: true)
            .SingleOrDefaultAsync(x => x.Id == deviceId && x.Status == DeviceStatus.Active, ct);
        if (device is null) return false; device.ConfigureCallback(url, secretEncrypted, now); await db.SaveChangesAsync(ct); return true;
    }

    public async Task<bool> ClearCallbackAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId,
        DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var device = await Query(tenantId, actorId, tenantScope, tracking: true).SingleOrDefaultAsync(x => x.Id == deviceId, ct);
        if (device is null) return false; device.ClearCallback(now);
        await db.StatusEvents.Where(x => x.TenantId == tenantId && x.DeviceId == deviceId && x.Status == "pending")
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "cancelled").SetProperty(x => x.FailureCode, "CALLBACK_DISABLED")
                .SetProperty(x => x.NextAttemptAt, (DateTimeOffset?)null).SetProperty(x => x.UpdatedAt, now), ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return true;
    }

    public async Task<DeviceKeyCreateResult> TryAddKeyAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, ApiKey apiKey, int deviceLimit, int tenantLimit, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({tenantId.ToString()}, 0))", ct);
        var device = await Query(tenantId, actorId, tenantScope).SingleOrDefaultAsync(x => x.Id == deviceId, ct);
        if (device is null) return DeviceKeyCreateResult.NotFound;
        if (device.Status == DeviceStatus.Disabled) return DeviceKeyCreateResult.DeviceDisabled;
        if (await db.ApiKeys.CountAsync(x => x.TenantId == tenantId && x.DeviceId == deviceId && x.Status == ApiKeyStatus.Active, ct) >= deviceLimit) return DeviceKeyCreateResult.DeviceLimitReached;
        if (await db.ApiKeys.CountAsync(x => x.TenantId == tenantId && x.Status == ApiKeyStatus.Active, ct) >= tenantLimit) return DeviceKeyCreateResult.TenantLimitReached;
        db.ApiKeys.Add(apiKey); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return DeviceKeyCreateResult.Success;
    }

    public async Task<DeviceApiKeyPage?> ListKeysAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, int limit, DateTimeOffset? cursorCreatedAt, Guid? cursorId, CancellationToken ct)
    {
        if (!await Query(tenantId, actorId, tenantScope).AnyAsync(x => x.Id == deviceId, ct)) return null;
        var query = db.ApiKeys.AsNoTracking().Where(x => x.TenantId == tenantId && x.DeviceId == deviceId);
        if (cursorCreatedAt is not null && cursorId is not null) query = query.Where(x => x.CreatedAt < cursorCreatedAt || (x.CreatedAt == cursorCreatedAt && x.Id.CompareTo(cursorId.Value) < 0));
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(limit + 1)
            .Select(x => new DeviceApiKeyItem(x.Id, x.DeviceId, x.KeyPrefix, x.Status, x.LastUsedAt, x.CreatedAt, x.RevokedAt)).ToListAsync(ct);
        string? next = null; if (rows.Count > limit) { rows.RemoveAt(rows.Count - 1); next = Cursor(rows[^1].CreatedAt, rows[^1].Id); }
        return new(rows, next);
    }

    public async Task<bool> RevokeKeyAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, Guid keyId, DateTimeOffset now, CancellationToken ct)
    {
        if (!await Query(tenantId, actorId, tenantScope).AnyAsync(x => x.Id == deviceId, ct) || !await db.ApiKeys.AnyAsync(x => x.TenantId == tenantId && x.DeviceId == deviceId && x.Id == keyId, ct)) return false;
        await db.ApiKeys.Where(x => x.TenantId == tenantId && x.DeviceId == deviceId && x.Id == keyId && x.Status == ApiKeyStatus.Active)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, ApiKeyStatus.Revoked).SetProperty(x => x.RevokedAt, now), ct); return true;
    }

    private IQueryable<Device> Query(Guid tenantId, Guid actorId, bool tenantScope, bool tracking = false)
    {
        var query = tracking ? db.Devices.AsQueryable() : db.Devices.AsNoTracking();
        query = query.Where(x => x.TenantId == tenantId); return tenantScope ? query : query.Where(x => x.OwnerAdminId == actorId);
    }
    private static System.Linq.Expressions.Expression<Func<Device, DeviceItem>> Map() => x => new(x.Id, x.Name, x.Role, x.Status, x.OwnerAdminId, x.CreatedAt, x.UpdatedAt, x.DisabledAt, x.CallbackUrl != null);
    private static DeviceItem ToItem(Device x) => new(x.Id, x.Name, x.Role, x.Status, x.OwnerAdminId, x.CreatedAt, x.UpdatedAt, x.DisabledAt, x.CallbackUrl != null);
    private static string Cursor(DateTimeOffset createdAt, Guid id) => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{createdAt:O}|{id}"));
    private static DevicePage Page(List<DeviceItem> rows, int limit) { string? next = null; if (rows.Count > limit) { rows.RemoveAt(rows.Count - 1); next = Cursor(rows[^1].CreatedAt, rows[^1].Id); } return new(rows, next); }
}
