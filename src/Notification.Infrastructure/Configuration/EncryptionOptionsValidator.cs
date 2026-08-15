using Microsoft.Extensions.Options;
namespace Notification.Infrastructure.Configuration;

public sealed class EncryptionOptionsValidator : IValidateOptions<EncryptionOptions>
{
    public ValidateOptionsResult Validate(string? name, EncryptionOptions options)
    {
        try { return Convert.FromBase64String(options.Key).Length == 32 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail("ENCRYPTION_KEY must decode to exactly 32 bytes."); }
        catch (FormatException) { return ValidateOptionsResult.Fail("ENCRYPTION_KEY must be valid base64 encoding exactly 32 bytes."); }
    }
}
