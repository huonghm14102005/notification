using Notification.Domain.Identity;
using Notification.Domain.Senders;

namespace Notification.Domain.Notifications;

public static class DeliveryStatus
{
    public const string Pending = "pending";
    public const string Sending = "sending";
    public const string Delivered = "delivered";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}

public static class DeliveryAggregate
{
    public static string Calculate(IEnumerable<string> statuses)
    {
        var values = statuses.ToArray();
        if (values.Length == 0) throw new InvalidOperationException("A notification must have a delivery.");
        if (values.Any(x => x == DeliveryStatus.Sending)) return NotificationStatus.Processing;
        if (values.Any(x => x == DeliveryStatus.Pending)) return NotificationStatus.Accepted;
        if (values.All(x => x == DeliveryStatus.Delivered)) return NotificationStatus.Delivered;
        if (values.Any(x => x == DeliveryStatus.Delivered)) return NotificationStatus.PartiallyDelivered;
        if (values.All(x => x == DeliveryStatus.Cancelled)) return NotificationStatus.Cancelled;
        return NotificationStatus.Failed;
    }
}

public sealed class Delivery
{
    private Delivery() { }
    public Delivery(Guid id, Guid tenantId, Guid notificationId, Guid senderId, string target, string? targetRef,
        DateTimeOffset now)
    {
        Id = id; TenantId = tenantId; NotificationId = notificationId; SenderId = senderId;
        Channel = "email"; Target = target; TargetRef = targetRef; Status = DeliveryStatus.Pending;
        NextAttemptAt = now; CreatedAt = now; UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid NotificationId { get; private set; }
    public Guid? SenderId { get; private set; }
    public string Channel { get; private set; } = string.Empty;
    public string Target { get; private set; } = string.Empty;
    public string? TargetRef { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public int AttemptCount { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public string? FailureCode { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
    public OutboundNotification Notification { get; private set; } = null!;
    public Sender? Sender { get; private set; }

    public void MarkSending(DateTimeOffset now) { if (Status != DeliveryStatus.Pending) throw new InvalidOperationException(); Status = DeliveryStatus.Sending; AttemptCount++; UpdatedAt = now; }
    public void MarkDelivered(DateTimeOffset now) { if (Status != DeliveryStatus.Sending) throw new InvalidOperationException(); Status = DeliveryStatus.Delivered; DeliveredAt = now; NextAttemptAt = null; FailureCode = null; UpdatedAt = now; }
    public void ScheduleRetry(DateTimeOffset next, DateTimeOffset now) { if (Status != DeliveryStatus.Sending) throw new InvalidOperationException(); Status = DeliveryStatus.Pending; NextAttemptAt = next; FailureCode = null; UpdatedAt = now; }
    public void MarkFailed(string code, DateTimeOffset now) { if (Status != DeliveryStatus.Sending) throw new InvalidOperationException(); Status = DeliveryStatus.Failed; FailureCode = code; NextAttemptAt = null; UpdatedAt = now; }
    public void Cancel(DateTimeOffset now) { if (Status != DeliveryStatus.Pending || AttemptCount != 0) throw new InvalidOperationException(); Status = DeliveryStatus.Cancelled; NextAttemptAt = null; FailureCode = null; UpdatedAt = now; }
}
