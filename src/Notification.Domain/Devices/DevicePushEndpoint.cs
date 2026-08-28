using Notification.Domain.Identity;

namespace Notification.Domain.Devices;

public static class PushPlatform
{
    public const string Fcm = "fcm";
    public const string Apns = "apns";
}

public static class PushEndpointStatus
{
    public const string Active = "active";
    public const string Disabled = "disabled";
}

public sealed class DevicePushEndpoint
{
    private DevicePushEndpoint() { }

    public DevicePushEndpoint(Guid id, Guid tenantId, Guid deviceId, string platform, byte[] tokenEncrypted, DateTimeOffset now)
    {
        if (platform is not PushPlatform.Fcm and not PushPlatform.Apns)
            throw new ArgumentOutOfRangeException(nameof(platform));
        if (tokenEncrypted is not { Length: > 0 })
            throw new ArgumentException("Token cannot be empty", nameof(tokenEncrypted));

        Id = id;
        TenantId = tenantId;
        DeviceId = deviceId;
        Platform = platform;
        TokenEncrypted = tokenEncrypted;
        Status = PushEndpointStatus.Active;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid DeviceId { get; private set; }
    public string Platform { get; private set; } = string.Empty;
    public byte[] TokenEncrypted { get; private set; } = [];
    public string Status { get; private set; } = PushEndpointStatus.Active;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DisabledAt { get; private set; }
    public DateTimeOffset? LastDeliveredAt { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public Device Device { get; private set; } = null!;

    public void UpdateToken(string platform, byte[] newTokenEncrypted, DateTimeOffset now)
    {
        if (platform is not PushPlatform.Fcm and not PushPlatform.Apns)
            throw new ArgumentOutOfRangeException(nameof(platform));
        if (newTokenEncrypted is not { Length: > 0 })
            throw new ArgumentException("Token cannot be empty", nameof(newTokenEncrypted));

        Platform = platform;
        TokenEncrypted = newTokenEncrypted;
        Status = PushEndpointStatus.Active;
        DisabledAt = null;
        UpdatedAt = now;
    }

    public void MarkDelivered(DateTimeOffset now)
    {
        LastDeliveredAt = now;
        UpdatedAt = now;
    }

    public void Disable(DateTimeOffset now)
    {
        if (Status == PushEndpointStatus.Disabled) return;
        Status = PushEndpointStatus.Disabled;
        DisabledAt = now;
        UpdatedAt = now;
    }
}
