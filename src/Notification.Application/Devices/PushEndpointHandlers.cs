using Notification.Application.Abstractions.Security;
using Notification.Application.Abstractions.Time;
using Notification.Domain.Devices;

namespace Notification.Application.Devices;

public sealed class PushEndpointHandlers(IDeviceRepository repository, ISecretCipher cipher, IClock clock)
{
    public async Task<DevicePushEndpointItem> RegisterAsync(Guid tenantId, Guid actorId, bool tenantScope,
        Guid deviceId, string platform, string token, CancellationToken ct)
    {
        var device = await repository.GetAsync(tenantId, actorId, tenantScope, deviceId, ct)
            ?? throw new DeviceOperationException("DEVICE_NOT_FOUND");

        if (device.Status == DeviceStatus.Disabled)
            throw new DeviceOperationException("DEVICE_DISABLED");

        var normalizedPlatform = platform?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalizedPlatform is not PushPlatform.Fcm and not PushPlatform.Apns)
            throw new DeviceOperationException("PLATFORM_NOT_SUPPORTED");

        if (string.IsNullOrWhiteSpace(token))
            throw new DeviceOperationException("TOKEN_REQUIRED");

        var now = clock.UtcNow;
        var tokenEncrypted = cipher.Encrypt(token.Trim(), tenantId, deviceId);

        var existing = await repository.FindPushEndpointAsync(tenantId, deviceId, ct);
        if (existing is null)
        {
            var endpoint = new DevicePushEndpoint(Guid.NewGuid(), tenantId, deviceId, normalizedPlatform, tokenEncrypted, now);
            await repository.SavePushEndpointAsync(endpoint, ct);
            return new DevicePushEndpointItem(deviceId, normalizedPlatform, endpoint.Status, now, now, null, null);
        }

        existing.UpdateToken(normalizedPlatform, tokenEncrypted, now);
        await repository.SavePushEndpointAsync(existing, ct);
        return new DevicePushEndpointItem(deviceId, normalizedPlatform, existing.Status, existing.CreatedAt, now, null, existing.LastDeliveredAt);
    }

    public async Task<DevicePushEndpointItem?> GetAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, CancellationToken ct)
    {
        var device = await repository.GetAsync(tenantId, actorId, tenantScope, deviceId, ct);
        if (device is null) return null;

        var endpoint = await repository.FindPushEndpointAsync(tenantId, deviceId, ct);
        if (endpoint is null) return null;

        return new DevicePushEndpointItem(
            endpoint.DeviceId,
            endpoint.Platform,
            endpoint.Status,
            endpoint.CreatedAt,
            endpoint.UpdatedAt,
            endpoint.DisabledAt,
            endpoint.LastDeliveredAt);
    }

    public async Task<bool> RevokeAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, CancellationToken ct)
    {
        var device = await repository.GetAsync(tenantId, actorId, tenantScope, deviceId, ct);
        if (device is null) return false;

        return await repository.DisablePushEndpointAsync(tenantId, deviceId, clock.UtcNow, ct);
    }
}
