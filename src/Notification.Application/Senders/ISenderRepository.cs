using Notification.Domain.Senders;

namespace Notification.Application.Senders;

public interface ISenderRepository
{
    Task<bool> KeyExistsAsync(Guid tenantId, string key, CancellationToken ct);
    Task AddAsync(Sender sender, CancellationToken ct);
    Task<SenderPage> ListAsync(Guid tenantId, int limit, DateTimeOffset? cursorCreatedAt, Guid? cursorId, CancellationToken ct);
    Task<Sender?> FindAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
