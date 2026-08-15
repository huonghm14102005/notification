namespace Notification.Application.Abstractions.Security;

public interface ISecretCipher
{
    byte[] Encrypt(string plaintext, Guid tenantId, Guid recordId);
    string Decrypt(byte[] envelope, Guid tenantId, Guid recordId);
}
