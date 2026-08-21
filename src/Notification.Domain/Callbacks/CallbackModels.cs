using Notification.Domain.Devices;
using Notification.Domain.Identity;
using Notification.Domain.Notifications;

namespace Notification.Domain.Callbacks;

public static class CallbackEventStatus
{
    public const string Pending = "pending";
    public const string Sending = "sending";
    public const string Delivered = "delivered";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}

public static class CallbackAttemptResult
{
    public const string Success = "success";
    public const string TransientFailure = "transient_failure";
    public const string PermanentFailure = "permanent_failure";
}

public sealed class StatusEvent
{
    private StatusEvent() { }
    public StatusEvent(Guid id, string publicId, Guid tenantId, Guid deviceId, Guid notificationId,
        byte[] payloadEncrypted, DateTimeOffset occurredAt)
    {
        Id = id; PublicId = publicId; TenantId = tenantId; DeviceId = deviceId; NotificationId = notificationId;
        EventType = "notification.completed"; PayloadEncrypted = payloadEncrypted; Status = CallbackEventStatus.Pending;
        NextAttemptAt = occurredAt; OccurredAt = occurredAt; CreatedAt = occurredAt; UpdatedAt = occurredAt;
    }
    public Guid Id { get; private set; }
    public string PublicId { get; private set; } = string.Empty;
    public Guid TenantId { get; private set; }
    public Guid DeviceId { get; private set; }
    public Guid NotificationId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public byte[] PayloadEncrypted { get; private set; } = [];
    public string Status { get; private set; } = CallbackEventStatus.Pending;
    public int AttemptCount { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public string? FailureCode { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
    public Device Device { get; private set; } = null!;
    public OutboundNotification Notification { get; private set; } = null!;

    public void MarkSending(DateTimeOffset now) { if (Status != CallbackEventStatus.Pending) throw new InvalidOperationException(); Status = CallbackEventStatus.Sending; AttemptCount++; UpdatedAt = now; }
    public void MarkDelivered(DateTimeOffset now) { if (Status != CallbackEventStatus.Sending) throw new InvalidOperationException(); Status = CallbackEventStatus.Delivered; NextAttemptAt = null; FailureCode = null; UpdatedAt = now; }
    public void ScheduleRetry(DateTimeOffset next, DateTimeOffset now) { if (Status != CallbackEventStatus.Sending) throw new InvalidOperationException(); Status = CallbackEventStatus.Pending; NextAttemptAt = next; FailureCode = null; UpdatedAt = now; }
    public void MarkFailed(string code, DateTimeOffset now) { if (Status != CallbackEventStatus.Sending) throw new InvalidOperationException(); Status = CallbackEventStatus.Failed; NextAttemptAt = null; FailureCode = code; UpdatedAt = now; }
    public void Cancel(string code, DateTimeOffset now) { if (Status is CallbackEventStatus.Delivered or CallbackEventStatus.Failed or CallbackEventStatus.Cancelled) return; Status = CallbackEventStatus.Cancelled; NextAttemptAt = null; FailureCode = code; UpdatedAt = now; }
}

public sealed class CallbackAttempt
{
    private CallbackAttempt() { }
    public CallbackAttempt(Guid id, Guid tenantId, Guid eventId, int attemptNo, string result, int? httpStatusCode,
        string? errorCode, DateTimeOffset startedAt, DateTimeOffset finishedAt)
    {
        Id = id; TenantId = tenantId; EventId = eventId; AttemptNo = attemptNo; Result = result;
        HttpStatusCode = httpStatusCode; ErrorCode = errorCode; StartedAt = startedAt; FinishedAt = finishedAt;
        CreatedAt = finishedAt;
    }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EventId { get; private set; }
    public int AttemptNo { get; private set; }
    public string Result { get; private set; } = string.Empty;
    public int? HttpStatusCode { get; private set; }
    public string? ErrorCode { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset FinishedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
    public StatusEvent Event { get; private set; } = null!;
}
