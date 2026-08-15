using Microsoft.Extensions.Options;
using Notification.Infrastructure.Configuration;

namespace Notification.IntegrationTests.Foundation;

public sealed class FoundationOptionsTests
{
    [Fact]
    public void InvalidSettingsOnlyReportSettingNames()
    {
        var options = new FoundationOptions
        {
            DatabaseUrl = "password=super-secret",
            RedisUrl = "redis://localhost:6379",
        };
        var result = new FoundationOptionsValidator().Validate(null, options);
        var message = string.Join(' ', result.Failures ?? []);
        Assert.True(result.Failed);
        Assert.Contains("DATABASE_URL", message, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", message, StringComparison.Ordinal);
    }
}
