using Notification.Domain.Senders;

namespace Notification.Application.Senders;

public interface ISenderRepository
{
    Task<bool> KeyExistsAsync(Guid tenantId, string key, CancellationToken ct);
    Task AddAsync(Sender sender, CancellationToken ct);
    Task<SenderPage> ListAsync(Guid tenantId, int limit, DateTimeOffset? cursorCreatedAt, Guid? cursorId, CancellationToken ct);
    Task<Sender?> FindAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    Task SaveUpdateAsync(Guid tenantId, Sender sender, bool? isDefault, DateTimeOffset now, CancellationToken ct);
    Task<ResolvedSender?> ResolveAsync(Guid tenantId, string? key, CancellationToken ct);
    Task<ResolvedSender?> FindResolvedByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> MarkVerifiedAsync(ResolvedSender snapshot, DateTimeOffset now, CancellationToken ct);
}
