using Notification.Domain.Devices;
using Notification.Domain.Identity;

namespace Notification.Application.Devices;

public interface IDeviceRepository
{
    Task AddAsync(Device device, CancellationToken cancellationToken);
    Task<DeviceItem> GetOrCreateLegacyAsync(Guid tenantId, Guid actorId, string producerName, DateTimeOffset now, CancellationToken cancellationToken);
    Task<DeviceItem?> GetAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, CancellationToken cancellationToken);
    Task<DevicePage> ListAsync(Guid tenantId, Guid actorId, bool tenantScope, string? status, int limit, DateTimeOffset? cursorCreatedAt, Guid? cursorId, CancellationToken cancellationToken);
    Task<DeviceItem?> RenameAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, string name, DateTimeOffset now, CancellationToken cancellationToken);
    Task<bool> DisableAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<bool> ConfigureCallbackAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, string url,
        byte[] secretEncrypted, DateTimeOffset now, CancellationToken cancellationToken);
    Task<bool> ClearCallbackAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, DateTimeOffset now,
        CancellationToken cancellationToken);
    Task<DeviceKeyCreateResult> TryAddKeyAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, ApiKey apiKey, int deviceLimit, int tenantLimit, CancellationToken cancellationToken);
    Task<DeviceApiKeyPage?> ListKeysAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, int limit, DateTimeOffset? cursorCreatedAt, Guid? cursorId, CancellationToken cancellationToken);
    Task<bool> RevokeKeyAsync(Guid tenantId, Guid actorId, bool tenantScope, Guid deviceId, Guid keyId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<DevicePushEndpoint?> FindPushEndpointAsync(Guid tenantId, Guid deviceId, CancellationToken cancellationToken);
    Task<DevicePushEndpoint?> FindActivePushEndpointAsync(Guid tenantId, Guid deviceId, CancellationToken cancellationToken);
    Task SavePushEndpointAsync(DevicePushEndpoint endpoint, CancellationToken cancellationToken);
    Task<bool> DisablePushEndpointAsync(Guid tenantId, Guid deviceId, DateTimeOffset now, CancellationToken cancellationToken);
}

public enum DeviceKeyCreateResult { Success, NotFound, DeviceDisabled, DeviceLimitReached, TenantLimitReached }
