namespace Notification.Application.Senders;

public interface ISenderResolver
{
    Task<ResolvedSender> ResolveAsync(Guid tenantId, string? senderKey, CancellationToken ct);
}

public sealed class SenderResolver(ISenderRepository repository) : ISenderResolver
{
    public async Task<ResolvedSender> ResolveAsync(Guid tenantId, string? senderKey, CancellationToken ct)
    {
        var key = string.IsNullOrWhiteSpace(senderKey) ? null : senderKey.Trim().ToLowerInvariant();
        return await repository.ResolveAsync(tenantId, key, ct)
            ?? throw new SenderOperationException("SENDER_NOT_FOUND");
    }
}
