using Microsoft.Extensions.Options;
using Notification.Infrastructure.Configuration;
using Notification.Infrastructure.Security;

namespace Notification.IntegrationTests.Identity;

public sealed class ApiKeySecurityTests
{
    [Fact]
    public void GeneratedKeyHasContractFormatAndHmacIsStable()
    {
        var service = new ApiKeySecretService(Options.Create(new ApiKeyOptions { Salt = "test-salt-at-least-16-bytes" }));
        var first = service.Generate(); var second = service.Generate();
        Assert.Matches("^notify_[0-9a-f]{64}$", first.Raw);
        Assert.Equal(first.Raw[..19], first.Prefix);
        Assert.NotEqual(first.Raw, second.Raw);
        Assert.True(service.FixedTimeEquals(first.Hash, service.Hash(first.Raw)));
        Assert.False(service.FixedTimeEquals(first.Hash, service.Hash(second.Raw)));
    }

    [Fact]
    public void WeakSaltFailsWithoutLeakingItsValue()
    {
        var result = new ApiKeyOptionsValidator().Validate(null, new ApiKeyOptions { Salt = "secret" });
        Assert.True(result.Failed);
        Assert.DoesNotContain("secret", string.Join(' ', result.Failures ?? []), StringComparison.Ordinal);
    }
}
