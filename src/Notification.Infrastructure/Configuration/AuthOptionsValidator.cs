using Microsoft.Extensions.Options;

namespace Notification.Infrastructure.Configuration;

public sealed class AuthOptionsValidator : IValidateOptions<AuthOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthOptions options)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(options.Secret) < 32) return ValidateOptionsResult.Fail("JWT_SECRET must contain at least 32 UTF-8 bytes.");
        if (string.IsNullOrWhiteSpace(options.Issuer)) return ValidateOptionsResult.Fail("JWT_ISSUER is required.");
        if (string.IsNullOrWhiteSpace(options.Audience)) return ValidateOptionsResult.Fail("JWT_AUDIENCE is required.");
        if (options.AccessExpiresIn <= 0 || options.RefreshExpiresIn <= 0 || options.AccessExpiresIn > options.RefreshExpiresIn)
            return ValidateOptionsResult.Fail("JWT TTL values must be positive and access TTL must not exceed refresh TTL.");
        return ValidateOptionsResult.Success;
    }
}
