using Microsoft.Extensions.Options;

namespace Notification.Infrastructure.Configuration;

public sealed class FoundationOptionsValidator : IValidateOptions<FoundationOptions>
{
    public ValidateOptionsResult Validate(string? name, FoundationOptions options)
    {
        var failures = new List<string>();
        ValidateUrl(options.DatabaseUrl, "DATABASE_URL", ["postgresql", "postgres"], failures);
        ValidateUrl(options.RedisUrl, "REDIS_URL", ["redis", "rediss"], failures);

        if (options.HealthCheckTimeoutSeconds is < 1 or > 30)
        {
            failures.Add("HEALTH_CHECK_TIMEOUT_SECONDS must be between 1 and 30.");
        }

        if (options.WorkerHealthIntervalSeconds is < 1 or > 300)
        {
            failures.Add("WORKER_HEALTH_INTERVAL_SECONDS must be between 1 and 300.");
        }

        if (string.IsNullOrWhiteSpace(options.WorkerHealthFile))
        {
            failures.Add("WORKER_HEALTH_FILE is required.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateUrl(string value, string settingName, string[] allowedSchemes, List<string> failures)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !allowedSchemes.Any(s => s.Equals(uri.Scheme, StringComparison.OrdinalIgnoreCase)) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            failures.Add($"{settingName} must be a valid {string.Join(" or ", allowedSchemes)} URL.");
        }
    }
}
