using Microsoft.Extensions.Options;

namespace Notification.Infrastructure.Configuration;

public sealed class ApiKeyOptionsValidator : IValidateOptions<ApiKeyOptions>
{
    public ValidateOptionsResult Validate(string? name, ApiKeyOptions options) =>
        System.Text.Encoding.UTF8.GetByteCount(options.Salt) >= 16
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("API_KEY_SALT must contain at least 16 UTF-8 bytes.");
}
