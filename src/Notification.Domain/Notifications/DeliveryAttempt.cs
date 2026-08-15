using Notification.Domain.Identity;
using Notification.Domain.Senders;

namespace Notification.Domain.Notifications;

public static class DeliveryResult
{
    public const string Success = "success";
    public const string TransientFailure = "transient_failure";
    public const string PermanentFailure = "permanent_failure";
}

public sealed class DeliveryAttempt
{
    private DeliveryAttempt() { }
    public DeliveryAttempt(Guid id, Guid tenantId, Guid notificationId, Guid senderId, int attemptNo, string result,
        string? providerMessageId, string? errorCode, string? errorMessage, DateTimeOffset startedAt, DateTimeOffset finishedAt)
    {
        Id = id; TenantId = tenantId; NotificationId = notificationId; SenderId = senderId; AttemptNo = attemptNo;
        Result = result; ProviderMessageId = providerMessageId; ErrorCode = errorCode; ErrorMessage = errorMessage;
        StartedAt = startedAt; FinishedAt = finishedAt; CreatedAt = finishedAt;
    }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid NotificationId { get; private set; }
    public Guid SenderId { get; private set; }
    public int AttemptNo { get; private set; }
    public string Result { get; private set; } = string.Empty;
    public string? ProviderMessageId { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset FinishedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
    public OutboundNotification Notification { get; private set; } = null!;
    public Sender Sender { get; private set; } = null!;
}
