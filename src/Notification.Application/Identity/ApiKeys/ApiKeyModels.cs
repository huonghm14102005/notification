namespace Notification.Application.Identity.ApiKeys;

public sealed record ApiKeyItem(Guid Id, string ProducerName, string KeyPrefix, string Status, DateTimeOffset? LastUsedAt, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt);
public sealed record CreatedApiKey(Guid Id, string ProducerName, string KeyPrefix, string RawKey, string Status, DateTimeOffset CreatedAt);
public sealed record ApiKeyPage(IReadOnlyList<ApiKeyItem> Items, string? NextCursor);
public sealed record ApiKeyIdentity(Guid Id, Guid TenantId, Guid OwnerUserId, Guid DeviceId, string DeviceRole, string ProducerName, byte[] Hash, DateTimeOffset? LastUsedAt);

public sealed class ApiKeyOperationException(string code) : Exception(code) { public string Code { get; } = code; }
