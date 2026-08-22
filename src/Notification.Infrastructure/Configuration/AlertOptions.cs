using Microsoft.Extensions.Options;

namespace Notification.Infrastructure.Configuration;

public sealed class AlertOptions { public int WindowSeconds { get; set; } = 900; public int PollIntervalMs { get; set; } = 5000; public int ClaimLimit { get; set; } = 20; public int StuckAfterSeconds { get; set; } = 120; }
public sealed class AlertOptionsValidator : IValidateOptions<AlertOptions>
{
    public ValidateOptionsResult Validate(string? name, AlertOptions x) => x.WindowSeconds is < 60 or > 86400 || x.PollIntervalMs is < 100 or > 60000 || x.ClaimLimit is < 1 or > 100 || x.StuckAfterSeconds is < 30 or > 3600 ? ValidateOptionsResult.Fail("Alert options are outside allowed limits.") : ValidateOptionsResult.Success;
}
