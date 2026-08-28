namespace Notification.Domain.Identity;

public sealed class Admin
{
    private Admin() { }

    public Admin(Guid id, Guid tenantId, string email, string passwordHash, DateTimeOffset now,
        string role = AdminRole.Owner, string? displayName = null)
    {
        Id = id; TenantId = tenantId; Email = email; PasswordHash = passwordHash;
        Role = role; DisplayName = displayName ?? email.Split('@')[0]; Status = AdminStatus.Active;
        CreatedAt = now; UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string Role { get; private set; } = AdminRole.Owner;
    public string DisplayName { get; private set; } = string.Empty;
    public string Status { get; private set; } = AdminStatus.Active;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public DateTimeOffset? DisabledAt { get; private set; }
    public Tenant Tenant { get; private set; } = null!;

    public void Disable(DateTimeOffset now)
    {
        if (Status == AdminStatus.Disabled) return;
        Status = AdminStatus.Disabled; DisabledAt = now; UpdatedAt = now;
    }
}

public static class AdminStatus
{
    public const string Active = "active";
    public const string Disabled = "disabled";
}
