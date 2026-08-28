namespace Notification.Application.Alerts;

public sealed record FailureGroup(string Channel, string ErrorCode, int Count);
public sealed record ClaimedFailureAlert(Guid Id, Guid TenantId, DateTimeOffset WindowStart, DateTimeOffset WindowEnd,
    int TotalCount, IReadOnlyList<FailureGroup> Groups, IReadOnlyList<string> Recipients);

public interface IFailureAlertRepository
{
    Task<IReadOnlyList<ClaimedFailureAlert>> ClaimAsync(DateTimeOffset now, int limit, CancellationToken ct);
    Task<bool> CompleteAsync(Guid id, int recipients, int successes, string? errorCode, DateTimeOffset now, CancellationToken ct);
    Task<int> RecoverAsync(DateTimeOffset staleBefore, DateTimeOffset now, int limit, CancellationToken ct);
}
