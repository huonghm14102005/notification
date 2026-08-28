namespace Notification.Application.Callbacks;

public sealed record ClaimedCallback(Guid EventId, Guid TenantId, Guid DeviceId, int AttemptNo);
public sealed record CallbackWorkItem(Guid EventId, string PublicId, Guid TenantId, Guid DeviceId, int AttemptNo,
    string Url, byte[] SecretEncrypted, byte[] PayloadEncrypted, string Status);
public sealed record CallbackSendResult(bool Success, bool Transient, int? HttpStatusCode, string? ErrorCode);

public interface ICallbackSender
{
    Task<CallbackSendResult> SendAsync(string url, string secret, string eventId, string rawJson,
        DateTimeOffset timestamp, CancellationToken cancellationToken);
}

public interface ICallbackRepository
{
    Task<IReadOnlyList<ClaimedCallback>> ClaimDueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);
    Task<CallbackWorkItem?> LoadClaimedAsync(Guid eventId, int attemptNo, CancellationToken cancellationToken);
    Task<bool> CancelClaimedAsync(Guid eventId, int attemptNo, DateTimeOffset cancelledAt,
        CancellationToken cancellationToken);
    Task<bool> CompleteAsync(CallbackWorkItem item, CallbackSendResult result, DateTimeOffset startedAt,
        DateTimeOffset finishedAt, DateTimeOffset? nextAttemptAt, CancellationToken cancellationToken);
    Task<IReadOnlyList<ClaimedCallback>> RecoverStuckAsync(DateTimeOffset now, DateTimeOffset staleBefore, int limit,
        CancellationToken cancellationToken);
}
