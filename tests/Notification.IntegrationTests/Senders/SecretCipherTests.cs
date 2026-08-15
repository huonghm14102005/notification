using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Notification.Infrastructure.Configuration;
using Notification.Infrastructure.Security;

namespace Notification.IntegrationTests.Senders;

public sealed class SecretCipherTests
{
    private static readonly EncryptionOptions Options = new() { Key = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=" };
    [Fact]
    public void CipherUsesRandomNonceAndBindsTenantAndRecordAsAad()
    {
        var cipher = new AesGcmSecretCipher(Microsoft.Extensions.Options.Options.Create(Options)); var tenant = Guid.NewGuid(); var id = Guid.NewGuid();
        var first = cipher.Encrypt("app-password", tenant, id); var second = cipher.Encrypt("app-password", tenant, id);
        Assert.NotEqual(first, second); Assert.Equal("app-password", cipher.Decrypt(first, tenant, id));
        Assert.ThrowsAny<CryptographicException>(() => cipher.Decrypt(first, Guid.NewGuid(), id));
        Assert.ThrowsAny<CryptographicException>(() => cipher.Decrypt(first, tenant, Guid.NewGuid()));
        first[^1] ^= 1; Assert.ThrowsAny<CryptographicException>(() => cipher.Decrypt(first, tenant, id));
    }
    [Fact]
    public void InvalidKeyConfigurationFailsWithoutLeakingKey()
    {
        var result = new EncryptionOptionsValidator().Validate(null, new EncryptionOptions { Key = "secret" });
        Assert.True(result.Failed); Assert.DoesNotContain("secret", string.Join(' ', result.Failures ?? []), StringComparison.Ordinal);
    }
}
