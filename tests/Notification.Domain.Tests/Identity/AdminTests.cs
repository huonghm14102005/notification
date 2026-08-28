using Notification.Domain.Identity;

namespace Notification.Domain.Tests.Identity;

public sealed class AdminTests
{
    [Fact]
    public void MemberUsesExplicitDisplayNameAndCanBeDisabledIdempotently()
    {
        var createdAt = DateTimeOffset.Parse("2026-08-22T00:00:00Z");
        var disabledAt = createdAt.AddMinutes(1);
        var user = new Admin(Guid.NewGuid(), Guid.NewGuid(), "member@example.com", "hash", createdAt, AdminRole.Member, "Member");

        user.Disable(disabledAt);
        user.Disable(disabledAt.AddMinutes(1));

        Assert.Equal(AdminRole.Member, user.Role);
        Assert.Equal("Member", user.DisplayName);
        Assert.Equal(AdminStatus.Disabled, user.Status);
        Assert.Equal(disabledAt, user.DisabledAt);
    }

    [Fact]
    public void DisplayNameDefaultsToEmailLocalPart()
    {
        var user = new Admin(Guid.NewGuid(), Guid.NewGuid(), "operator@example.com", "hash", DateTimeOffset.UtcNow);
        Assert.Equal("operator", user.DisplayName);
    }
}
