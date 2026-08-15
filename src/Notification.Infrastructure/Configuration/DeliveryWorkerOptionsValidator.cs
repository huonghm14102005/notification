using Microsoft.Extensions.Options;

namespace Notification.Infrastructure.Configuration;

public sealed class DeliveryWorkerOptionsValidator : IValidateOptions<DeliveryWorkerOptions>
{
    public ValidateOptionsResult Validate(string? name, DeliveryWorkerOptions options)
    {
        var errors = new List<string>();
        if (options.PollIntervalMs is < 250 or > 60000) errors.Add("DELIVERY_POLL_INTERVAL_MS must be between 250 and 60000.");
        if (options.Concurrency is < 1 or > 50) errors.Add("WORKER_CONCURRENCY must be between 1 and 50.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
