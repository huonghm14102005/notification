using System.Globalization;
using System.Text;
using Notification.Application.Abstractions.Security;
using Notification.Application.Abstractions.Time;
using Notification.Domain.Senders;

namespace Notification.Application.Senders;

public sealed class SenderHandlers(ISenderRepository repository, ISecretCipher cipher, IClock clock)
{
    public async Task<SenderItem> CreateAsync(Guid tenantId, CreateSenderCommand command, CancellationToken ct)
    {
        var key = command.Key.Trim().ToLowerInvariant(); if (await repository.KeyExistsAsync(tenantId, key, ct)) throw new SenderOperationException("SENDER_KEY_EXISTS");
        var id = Guid.NewGuid(); var now = clock.UtcNow; var encrypted = cipher.Encrypt(command.Password, tenantId, id);
        var sender = new Sender(id, tenantId, key, command.Host.Trim().ToLowerInvariant(), command.Port, command.Secure, command.Username.Trim(), encrypted, command.FromEmail.Trim().ToLowerInvariant(), command.FromName.Trim(), now);
        await repository.AddAsync(sender, ct); return Map(sender);
    }
    public Task<SenderPage> ListAsync(Guid tenantId, int limit, string? cursor, CancellationToken ct)
    {
        DateTimeOffset? at = null; Guid? id = null;
        if (cursor is not null) try { var p = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|'); at = DateTimeOffset.Parse(p[0], CultureInfo.InvariantCulture); id = Guid.Parse(p[1]); } catch { throw new SenderOperationException("VALIDATION_FAILED"); }
        return repository.ListAsync(tenantId, limit, at, id, ct);
    }
    public async Task<SenderItem> UpdateAsync(Guid tenantId, Guid id, UpdateSenderCommand command, CancellationToken ct)
    {
        var sender = await repository.FindAsync(tenantId, id, ct) ?? throw new SenderOperationException("NOT_FOUND");
        if (sender.Status == SenderStatus.Disabled) throw new SenderOperationException("SENDER_DISABLED");
        var encrypted = command.Password is null ? null : cipher.Encrypt(command.Password, tenantId, id); var now = clock.UtcNow;
        sender.Update(command.Host?.Trim().ToLowerInvariant(), command.Port, command.Secure, command.Username?.Trim(), encrypted, command.FromEmail?.Trim().ToLowerInvariant(), command.FromName?.Trim(), now);
        await repository.SaveUpdateAsync(tenantId, sender, command.IsDefault, now, ct); return Map(sender);
    }
    public async Task DisableAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var sender = await repository.FindAsync(tenantId, id, ct) ?? throw new SenderOperationException("NOT_FOUND");
        if (sender.Status != SenderStatus.Disabled) { sender.Disable(clock.UtcNow); await repository.SaveAsync(ct); }
    }
    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        if (!await repository.DeleteAsync(tenantId, id, ct)) throw new SenderOperationException("NOT_FOUND");
    }
    public static SenderItem Map(Sender x) => new(x.Id, x.Key, x.Channel, x.Host, x.Port, x.Secure, x.Username, x.FromEmail, x.FromName, x.IsDefault, x.Status, x.VerifiedAt, x.CreatedAt, x.UpdatedAt);
}
