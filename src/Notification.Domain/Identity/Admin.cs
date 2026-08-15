namespace Notification.Domain.Identity;

public sealed class Admin
{
    private Admin() { }

    public Admin(Guid id, Guid tenantId, string email, string passwordHash, DateTimeOffset now)
    {
        Id = id; TenantId = tenantId; Email = email; PasswordHash = passwordHash;
        Role = AdminRole.Owner; CreatedAt = now; UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string Role { get; private set; } = AdminRole.Owner;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
}
