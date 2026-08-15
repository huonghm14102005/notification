using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Notification.Application.Abstractions.Security;

namespace Notification.Infrastructure.Security;

public sealed class SecureRefreshTokenGenerator : IRefreshTokenGenerator
{
    public RefreshTokenSecret Generate()
    {
        var raw = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return new(raw, Hash(raw));
    }

    public byte[] Hash(string token) => SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
}
