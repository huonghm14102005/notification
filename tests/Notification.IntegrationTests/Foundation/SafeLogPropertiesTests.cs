using Notification.Infrastructure.Observability;

namespace Notification.IntegrationTests.Foundation;

public sealed class SafeLogPropertiesTests
{
    [Fact]
    public void RemovesSecretsAndMessageContentFromStructuredContext()
    {
        var values = new Dictionary<string, object?>
        {
            ["correlationId"] = "corr-1",
            ["tenantId"] = "tenant-1",
            ["password"] = "password-value",
            ["token"] = "token-value",
            ["apiKey"] = "key-value",
            ["connectionString"] = "connection-value",
            ["recipient"] = "student@example.test",
            ["subject"] = "private-subject",
            ["body"] = "private-body",
        };

        var safe = SafeLogProperties.Create(values);
        var output = string.Join(' ', safe.Select(item => $"{item.Key}={item.Value}"));
        Assert.Equal(2, safe.Count);
        Assert.DoesNotContain("value", output, StringComparison.Ordinal);
        Assert.DoesNotContain("example.test", output, StringComparison.Ordinal);
    }
}
