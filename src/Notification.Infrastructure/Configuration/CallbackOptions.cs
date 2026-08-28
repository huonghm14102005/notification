using Microsoft.Extensions.Options;

namespace Notification.Infrastructure.Configuration;

public sealed class CallbackOptions
{
    public int TimeoutMs { get; set; } = 10000;
    public int PollIntervalMs { get; set; } = 2000;
    public int Concurrency { get; set; } = 5;
    public int StuckAfterSeconds { get; set; } = 120;
    public bool AllowInsecureHttp { get; set; }
    public bool AllowPrivateNetwork { get; set; }
    public string EnvironmentName { get; set; } = "Production";
}

public sealed class CallbackOptionsValidator : IValidateOptions<CallbackOptions>
{
    public ValidateOptionsResult Validate(string? name, CallbackOptions options)
    {
        var errors = new List<string>();
        if (options.TimeoutMs is < 1000 or > 30000) errors.Add("CALLBACK_TIMEOUT_MS must be between 1000 and 30000.");
        if (options.PollIntervalMs is < 250 or > 60000) errors.Add("CALLBACK_POLL_INTERVAL_MS must be between 250 and 60000.");
        if (options.Concurrency is < 1 or > 50) errors.Add("CALLBACK_CONCURRENCY must be between 1 and 50.");
        if (options.StuckAfterSeconds is < 2 or > 86400 || options.StuckAfterSeconds * 1000 <= options.TimeoutMs)
            errors.Add("CALLBACK_STUCK_AFTER_SECONDS must be greater than callback timeout and at most 86400.");
        if (options.AllowInsecureHttp && options.EnvironmentName is not "Development" and not "Test")
            errors.Add("CALLBACK_ALLOW_INSECURE_HTTP is only allowed in Development or Test.");
        if (options.AllowPrivateNetwork && options.EnvironmentName is not "Development" and not "Test")
            errors.Add("CALLBACK_ALLOW_PRIVATE_NETWORK is only allowed in Development or Test.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
