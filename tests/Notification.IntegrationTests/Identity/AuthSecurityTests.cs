using Microsoft.Extensions.Options;
using Notification.Infrastructure.Configuration;
using Notification.Infrastructure.Security;

namespace Notification.IntegrationTests.Identity;

public sealed class AuthSecurityTests
{
    [Fact]
    public void RefreshSecretsAreRandomAndOnlyTheirHashesNeedPersisting()
    {
        var generator = new SecureRefreshTokenGenerator();
        var first = generator.Generate();
        var second = generator.Generate();
        Assert.NotEqual(first.Raw, second.Raw);
        Assert.Equal(32, first.Hash.Length);
        Assert.Equal(first.Hash, generator.Hash(first.Raw));
        Assert.DoesNotContain(first.Raw, Convert.ToHexString(first.Hash), StringComparison.Ordinal);
    }

    [Fact]
    public void WeakSecretAndInvalidTtlFailValidation()
    {
        var result = new AuthOptionsValidator().Validate(null, new AuthOptions { Secret = "short", AccessExpiresIn = 100, RefreshExpiresIn = 50 });
        Assert.True(result.Failed);
        Assert.DoesNotContain("short", string.Join(' ', result.Failures ?? []), StringComparison.Ordinal);
    }
}
