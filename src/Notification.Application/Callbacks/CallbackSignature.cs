using System.Security.Cryptography;
using System.Text;

namespace Notification.Application.Callbacks;

public static class CallbackSignature
{
    public static string Create(string secret, string timestamp, string rawJson) =>
        $"v1={Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{rawJson}")))}";
}
