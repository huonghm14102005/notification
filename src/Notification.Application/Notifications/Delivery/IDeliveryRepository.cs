namespace Notification.Application.Notifications.Delivery;

public interface IDeliveryRepository
{
    Task<IReadOnlyList<ClaimedNotification>> ClaimDueAsync(DateTimeOffset now, int limit, CancellationToken ct);
    Task<IReadOnlyList<RecoveredNotification>> RecoverStuckAsync(DateTimeOffset now, DateTimeOffset staleBefore, int limit,
        CancellationToken ct);
    Task<DeliveryWorkItem?> LoadClaimedAsync(Guid notificationId, int attemptNo, CancellationToken ct);
    Task<bool> CompleteSuccessAsync(DeliveryWorkItem item, string? providerMessageId, DateTimeOffset startedAt, DateTimeOffset finishedAt, CancellationToken ct);
    Task<bool> CompleteTransientFailureAsync(DeliveryWorkItem item, string errorCode, string errorMessage, DateTimeOffset? nextAttemptAt,
        DateTimeOffset startedAt, DateTimeOffset finishedAt, CancellationToken ct);
    Task<bool> CompletePermanentFailureAsync(DeliveryWorkItem item, string errorCode, string errorMessage,
        DateTimeOffset startedAt, DateTimeOffset finishedAt, CancellationToken ct);
}
