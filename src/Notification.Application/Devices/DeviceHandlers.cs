using System.Globalization;
using System.Text;
using Notification.Application.Abstractions.Security;
using Notification.Application.Abstractions.Callbacks;
using Notification.Application.Abstractions.Time;
using Notification.Domain.Devices;
using Notification.Domain.Identity;

namespace Notification.Application.Devices;

public sealed class DeviceHandlers(IDeviceRepository repository, IApiKeySecretService secrets, ISecretCipher cipher,
    ICallbackSecretGenerator callbackSecrets, ICallbackTargetValidator callbackTargets, IClock clock)
{
    public async Task<DeviceItem> CreateAsync(Guid tenantId, Guid actorId, string name, string role, CancellationToken ct)
    {
        var device = new Device(Guid.NewGuid(), tenantId, actorId, name.Trim(), role, clock.UtcNow);
        await repository.AddAsync(device, ct); return Map(device);
    }

    public async Task<DeviceItem> GetAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid id, CancellationToken ct) =>
        await repository.GetAsync(tenantId, actorId, tenantScope, id, ct) ?? throw new DeviceOperationException("NOT_FOUND");

    public Task<DevicePage> ListAsync(Guid tenantId, Guid actorId, bool tenantScope, string? status, int limit, string? cursor, CancellationToken ct)
    {
        var (createdAt, id) = ParseCursor(cursor); return repository.ListAsync(tenantId, actorId, tenantScope, status, limit, createdAt, id, ct);
    }

    public async Task<DeviceItem> RenameAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid id, string name, CancellationToken ct) =>
        await repository.RenameAsync(tenantId, actorId, tenantScope, id, name.Trim(), clock.UtcNow, ct) ?? throw new DeviceOperationException("NOT_FOUND");

    public async Task DisableAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid id, CancellationToken ct)
    { if (!await repository.DisableAsync(tenantId, actorId, tenantScope, id, clock.UtcNow, ct)) throw new DeviceOperationException("NOT_FOUND"); }

    public async Task<ConfiguredDeviceCallback> ConfigureCallbackAsync(Guid tenantId, Guid actorId, bool tenantScope,
        Guid deviceId, string url, CancellationToken ct)
    {
        string normalizedUrl;
        try { normalizedUrl = await callbackTargets.ValidateAsync(url, ct); }
        catch (CallbackTargetException exception) { throw new DeviceOperationException(exception.Code); }
        var secret = callbackSecrets.Generate(); var now = clock.UtcNow;
        var encrypted = cipher.Encrypt(secret, tenantId, deviceId);
        if (!await repository.ConfigureCallbackAsync(tenantId, actorId, tenantScope, deviceId, normalizedUrl, encrypted, now, ct))
            throw new DeviceOperationException("NOT_FOUND");
        return new(deviceId, normalizedUrl, secret, now);
    }

    public async Task ClearCallbackAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, CancellationToken ct)
    {
        if (!await repository.ClearCallbackAsync(tenantId, actorId, tenantScope, deviceId, clock.UtcNow, ct))
            throw new DeviceOperationException("NOT_FOUND");
    }

    public async Task<CreatedDeviceApiKey> CreateKeyAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, CancellationToken ct)
    {
        var secret = secrets.Generate(); var now = clock.UtcNow;
        var device = await repository.GetAsync(tenantId, actorId, tenantScope, deviceId, ct) ?? throw new DeviceOperationException("NOT_FOUND");
        var key = new ApiKey(Guid.NewGuid(), tenantId, actorId, deviceId, device.Name, secret.Prefix, secret.Hash, now);
        var result = await repository.TryAddKeyAsync(tenantId, actorId, tenantScope, deviceId, key, 10, 50, ct);
        if (result != DeviceKeyCreateResult.Success) throw new DeviceOperationException(result switch
        {
            DeviceKeyCreateResult.NotFound => "NOT_FOUND",
            DeviceKeyCreateResult.DeviceDisabled => "DEVICE_DISABLED",
            DeviceKeyCreateResult.DeviceLimitReached => "DEVICE_API_KEY_LIMIT_REACHED",
            _ => "API_KEY_LIMIT_REACHED",
        });
        return new(key.Id, deviceId, key.KeyPrefix, secret.Raw, key.Status, key.CreatedAt);
    }

    public async Task<DeviceApiKeyPage> ListKeysAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, int limit, string? cursor, CancellationToken ct)
    {
        var (createdAt, id) = ParseCursor(cursor);
        return await repository.ListKeysAsync(tenantId, actorId, tenantScope, deviceId, limit, createdAt, id, ct) ?? throw new DeviceOperationException("NOT_FOUND");
    }

    public async Task RevokeKeyAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, Guid keyId, CancellationToken ct)
    { if (!await repository.RevokeKeyAsync(tenantId, actorId, tenantScope, deviceId, keyId, clock.UtcNow, ct)) throw new DeviceOperationException("NOT_FOUND"); }

    private static (DateTimeOffset?, Guid?) ParseCursor(string? cursor)
    {
        if (cursor is null) return (null, null);
        try { var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|'); return (DateTimeOffset.Parse(parts[0], CultureInfo.InvariantCulture), Guid.Parse(parts[1])); }
        catch (Exception exception) when (exception is FormatException or IndexOutOfRangeException) { throw new DeviceOperationException("VALIDATION_FAILED"); }
    }
    private static DeviceItem Map(Device x) => new(x.Id, x.Name, x.Role, x.Status, x.OwnerAdminId, x.CreatedAt, x.UpdatedAt, x.DisabledAt, x.CallbackUrl != null);
}
