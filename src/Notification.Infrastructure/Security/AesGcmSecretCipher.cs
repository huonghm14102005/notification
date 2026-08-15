using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Notification.Application.Abstractions.Security;
using Notification.Infrastructure.Configuration;

namespace Notification.Infrastructure.Security;

public sealed class AesGcmSecretCipher(IOptions<EncryptionOptions> options) : ISecretCipher
{
    private const byte Version = 1;
    public byte[] Encrypt(string plaintext, Guid tenantId, Guid recordId)
    {
        var nonce = RandomNumberGenerator.GetBytes(12); var plain = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length]; var tag = new byte[16];
        using var aes = new AesGcm(Convert.FromBase64String(options.Value.Key), 16);
        aes.Encrypt(nonce, plain, cipher, tag, Aad(tenantId, recordId));
        return [Version, .. nonce, .. tag, .. cipher];
    }
    public string Decrypt(byte[] envelope, Guid tenantId, Guid recordId)
    {
        if (envelope.Length < 30 || envelope[0] != Version) throw new CryptographicException("Unsupported encrypted secret envelope.");
        var plain = new byte[envelope.Length - 29];
        using var aes = new AesGcm(Convert.FromBase64String(options.Value.Key), 16);
        aes.Decrypt(envelope.AsSpan(1, 12), envelope.AsSpan(29), envelope.AsSpan(13, 16), plain, Aad(tenantId, recordId));
        return System.Text.Encoding.UTF8.GetString(plain);
    }
    private static byte[] Aad(Guid tenantId, Guid recordId) => System.Text.Encoding.UTF8.GetBytes($"v{Version}:{tenantId:D}:{recordId:D}");
}
