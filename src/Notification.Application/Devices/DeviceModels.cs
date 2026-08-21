namespace Notification.Application.Devices;

public sealed record DeviceItem(Guid Id, string Name, string Role, string Status, Guid OwnerUserId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? DisabledAt, bool CallbackConfigured);
public sealed record ConfiguredDeviceCallback(Guid DeviceId, string Url, string Secret, DateTimeOffset ConfiguredAt);
public sealed record DevicePage(IReadOnlyList<DeviceItem> Items, string? NextCursor);
public sealed record DeviceApiKeyItem(Guid Id, Guid DeviceId, string KeyPrefix, string Status, DateTimeOffset? LastUsedAt, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt);
public sealed record CreatedDeviceApiKey(Guid Id, Guid DeviceId, string KeyPrefix, string RawKey, string Status, DateTimeOffset CreatedAt);
public sealed record DeviceApiKeyPage(IReadOnlyList<DeviceApiKeyItem> Items, string? NextCursor);
public sealed class DeviceOperationException(string code) : Exception(code) { public string Code { get; } = code; }
