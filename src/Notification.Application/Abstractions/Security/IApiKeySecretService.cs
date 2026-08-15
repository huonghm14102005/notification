namespace Notification.Application.Abstractions.Security;

public interface IApiKeySecretService
{
    ApiKeySecret Generate();
    string GetPrefix(string rawKey);
    byte[] Hash(string rawKey);
    bool FixedTimeEquals(byte[] left, byte[] right);
}

public sealed record ApiKeySecret(string Raw, string Prefix, byte[] Hash);
