using Microsoft.Extensions.Options;

namespace Notification.Infrastructure.Configuration;

public sealed class DeliveryWorkerOptionsValidator : IValidateOptions<DeliveryWorkerOptions>
{
    public ValidateOptionsResult Validate(string? name, DeliveryWorkerOptions options)
    {
        var errors = new List<string>();
        if (options.PollIntervalMs is < 250 or > 60000) errors.Add("DELIVERY_POLL_INTERVAL_MS must be between 250 and 60000.");
        if (options.Concurrency is < 1 or > 50) errors.Add("WORKER_CONCURRENCY must be between 1 and 50.");
        if (options.SweepIntervalSeconds is < 5 or > 3600) errors.Add("SWEEP_INTERVAL_SECONDS must be between 5 and 3600.");
        if (options.StuckAfterSeconds is < 180 or > 86400) errors.Add("STUCK_AFTER_SECONDS must be between 180 and 86400.");
        if (options.StuckAfterSeconds * 1000 <= options.SmtpTimeoutMs)
            errors.Add("STUCK_AFTER_SECONDS must be greater than SMTP_TIMEOUT_MS.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
