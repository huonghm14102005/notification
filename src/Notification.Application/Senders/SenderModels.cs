namespace Notification.Application.Senders;

public sealed record SenderItem(Guid Id, string Key, string Channel, string Host, int Port, bool Secure, string Username, string FromEmail, string FromName, bool IsDefault, string Status, DateTimeOffset? VerifiedAt, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record SenderPage(IReadOnlyList<SenderItem> Items, string? NextCursor);
public sealed record CreateSenderCommand(string Key, string Host, int Port, bool Secure, string Username, string Password, string FromEmail, string FromName);
public sealed record UpdateSenderCommand(string? Host, int? Port, bool? Secure, string? Username, string? Password, string? FromEmail, string? FromName, bool? IsDefault);
public sealed record ResolvedSender(Guid Id, Guid TenantId, string Key, string Channel, string Host, int Port, bool Secure, string Username, byte[] PasswordEncrypted, string FromEmail, string FromName);
public sealed class SenderOperationException(string code) : Exception(code) { public string Code { get; } = code; }
