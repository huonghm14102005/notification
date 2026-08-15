namespace Notification.Domain.Identity;

public sealed class RefreshToken
{
    private RefreshToken() { }

    public RefreshToken(Guid id, Guid adminId, Guid familyId, byte[] tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        Id = id;
        AdminId = adminId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }
    public Guid AdminId { get; private set; }
    public Guid FamilyId { get; private set; }
    public byte[] TokenHash { get; private set; } = [];
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? ReplacedById { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Admin Admin { get; private set; } = null!;
}
