using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Notification.Application.Abstractions.Security;
using Notification.Infrastructure.Configuration;

namespace Notification.Infrastructure.Security;

public sealed class ApiKeySecretService(IOptions<ApiKeyOptions> options) : IApiKeySecretService
{
    public ApiKeySecret Generate()
    {
        var raw = "notify_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        return new(raw, GetPrefix(raw), Hash(raw));
    }
    public string GetPrefix(string rawKey) => rawKey.Length >= 19 ? rawKey[..19] : string.Empty;
    public byte[] Hash(string rawKey) => HMACSHA256.HashData(System.Text.Encoding.UTF8.GetBytes(options.Value.Salt), System.Text.Encoding.UTF8.GetBytes(rawKey));
    public bool FixedTimeEquals(byte[] left, byte[] right) => CryptographicOperations.FixedTimeEquals(left, right);
}
