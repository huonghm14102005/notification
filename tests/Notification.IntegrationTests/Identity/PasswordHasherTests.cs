using Notification.Infrastructure.Security;

namespace Notification.IntegrationTests.Identity;

public sealed class PasswordHasherTests
{
    [Fact]
    public void HashesAndVerifiesWithoutStoringPlaintext()
    {
        var hasher = new AspNetPasswordHasher();
        var first = hasher.Hash("12345678"); var second = hasher.Hash("12345678");
        Assert.NotEqual("12345678", first); Assert.NotEqual(first, second);
        Assert.True(hasher.Verify(first, "12345678")); Assert.False(hasher.Verify(first, "wrong-password"));
    }
}
