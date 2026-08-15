namespace Notification.Domain.Identity;

public sealed class ApiKey
{
    private ApiKey() { }

    public ApiKey(Guid id, Guid tenantId, Guid createdByAdminId, string producerName, string keyPrefix, byte[] keyHash, DateTimeOffset now)
    {
        Id = id; TenantId = tenantId; CreatedByAdminId = createdByAdminId; ProducerName = producerName;
        KeyPrefix = keyPrefix; KeyHash = keyHash; Status = ApiKeyStatus.Active; CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CreatedByAdminId { get; private set; }
    public string ProducerName { get; private set; } = string.Empty;
    public string KeyPrefix { get; private set; } = string.Empty;
    public byte[] KeyHash { get; private set; } = [];
    public string Status { get; private set; } = ApiKeyStatus.Active;
    public DateTimeOffset? LastUsedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
    public Admin CreatedByAdmin { get; private set; } = null!;
}
