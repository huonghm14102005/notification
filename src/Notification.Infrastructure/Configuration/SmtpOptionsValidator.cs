using Microsoft.Extensions.Options;

namespace Notification.Infrastructure.Configuration;

public sealed class SmtpOptionsValidator : IValidateOptions<SmtpOptions>
{
    public ValidateOptionsResult Validate(string? name, SmtpOptions options) => options.TimeoutMs is >= 1000 and <= 120000
        ? ValidateOptionsResult.Success
        : ValidateOptionsResult.Fail("SMTP_TIMEOUT_MS must be between 1000 and 120000.");
}
