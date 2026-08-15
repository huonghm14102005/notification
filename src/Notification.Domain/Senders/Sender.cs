using Notification.Domain.Identity;

namespace Notification.Domain.Senders;

public sealed class Sender
{
    private Sender() { }
    public Sender(Guid id, Guid tenantId, string key, string host, int port, bool secure, string username, byte[] passwordEncrypted, string fromEmail, string fromName, DateTimeOffset now)
    {
        Id = id; TenantId = tenantId; Key = key; Channel = "email"; Host = host; Port = port; Secure = secure;
        Username = username; PasswordEncrypted = passwordEncrypted; FromEmail = fromEmail; FromName = fromName;
        Status = SenderStatus.Active; CreatedAt = now; UpdatedAt = now;
    }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Channel { get; private set; } = "email";
    public string Host { get; private set; } = string.Empty;
    public int Port { get; private set; }
    public bool Secure { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public byte[] PasswordEncrypted { get; private set; } = [];
    public string FromEmail { get; private set; } = string.Empty;
    public string FromName { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }
    public string Status { get; private set; } = SenderStatus.Active;
    public DateTimeOffset? VerifiedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Tenant Tenant { get; private set; } = null!;

    public void Update(string? host, int? port, bool? secure, string? username, byte[]? password, string? fromEmail, string? fromName, DateTimeOffset now)
    {
        if (host is not null) Host = host; if (port is not null) Port = port.Value; if (secure is not null) Secure = secure.Value;
        if (username is not null) Username = username; if (password is not null) PasswordEncrypted = password;
        if (fromEmail is not null) FromEmail = fromEmail; if (fromName is not null) FromName = fromName;
        if (host is not null || port is not null || secure is not null || username is not null || password is not null) VerifiedAt = null;
        UpdatedAt = now;
    }
    public void Disable(DateTimeOffset now) { Status = SenderStatus.Disabled; IsDefault = false; UpdatedAt = now; }
}
