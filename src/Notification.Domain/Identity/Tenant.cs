namespace Notification.Domain.Identity;

public sealed class Tenant
{
    private Tenant() { }

    public Tenant(Guid id, string name, string slug, DateTimeOffset now)
    {
        Id = id; Name = name; Slug = slug; CreatedAt = now; UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
}
