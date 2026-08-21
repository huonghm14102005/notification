using Notification.Domain.Identity;

namespace Notification.Domain.Devices;

public sealed class Device
{
    private Device() { }

    public Device(Guid id, Guid tenantId, Guid ownerAdminId, string name, string role, DateTimeOffset now, string? normalizedLegacyName = null)
    {
        if (role is not DeviceRole.Source and not DeviceRole.Both) throw new ArgumentOutOfRangeException(nameof(role));
        Id = id; TenantId = tenantId; OwnerAdminId = ownerAdminId; Name = name; Role = role;
        Status = DeviceStatus.Active; CreatedAt = now; UpdatedAt = now; NormalizedLegacyName = normalizedLegacyName;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OwnerAdminId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? NormalizedLegacyName { get; private set; }
    public string Role { get; private set; } = DeviceRole.Source;
    public string Status { get; private set; } = DeviceStatus.Active;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DisabledAt { get; private set; }
    public string? CallbackUrl { get; private set; }
    public byte[]? CallbackSecretEncrypted { get; private set; }
    public DateTimeOffset? CallbackConfiguredAt { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
    public Admin OwnerAdmin { get; private set; } = null!;

    public void Rename(string name, DateTimeOffset now) { Name = name; UpdatedAt = now; }
    public void Disable(DateTimeOffset now)
    {
        if (Status == DeviceStatus.Disabled) return;
        Status = DeviceStatus.Disabled; DisabledAt = now; UpdatedAt = now;
    }
    public void ConfigureCallback(string url, byte[] secretEncrypted, DateTimeOffset now)
    {
        if (Status != DeviceStatus.Active) throw new InvalidOperationException();
        CallbackUrl = url; CallbackSecretEncrypted = secretEncrypted; CallbackConfiguredAt = now; UpdatedAt = now;
    }
    public void ClearCallback(DateTimeOffset now)
    {
        CallbackUrl = null; CallbackSecretEncrypted = null; CallbackConfiguredAt = null; UpdatedAt = now;
    }
}
