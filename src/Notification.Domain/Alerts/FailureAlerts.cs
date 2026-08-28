using Notification.Domain.Identity;

namespace Notification.Domain.Alerts;

public static class FailureAlertStatus
{
    public const string Pending = "pending"; public const string Sending = "sending";
    public const string Delivered = "delivered"; public const string PartiallyDelivered = "partially_delivered";
    public const string Failed = "failed";
}

public sealed class FailureIncident
{
    private FailureIncident() { }
    public FailureIncident(Guid id, Guid tenantId, DateTimeOffset windowStart, DateTimeOffset windowEnd,
        string component, string channel, string errorCode, string sample, DateTimeOffset now)
    {
        Id = id; TenantId = tenantId; WindowStart = windowStart; WindowEnd = windowEnd; Component = component; Channel = channel;
        ErrorCode = errorCode; SampleMessage = sample; FirstSeenAt = now; LastSeenAt = now; OccurrenceCount = 1; CreatedAt = now; UpdatedAt = now;
    }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public DateTimeOffset WindowStart { get; private set; }
    public DateTimeOffset WindowEnd { get; private set; }
    public string Component { get; private set; } = ""; public string Channel { get; private set; } = "";
    public string ErrorCode { get; private set; } = ""; public string SampleMessage { get; private set; } = "";
    public DateTimeOffset FirstSeenAt { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }
    public int OccurrenceCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
}

public sealed class FailureAlert
{
    private FailureAlert() { }
    public FailureAlert(Guid id, Guid tenantId, DateTimeOffset start, DateTimeOffset end, DateTimeOffset now)
    { Id = id; TenantId = tenantId; WindowStart = start; WindowEnd = end; Status = FailureAlertStatus.Pending; CreatedAt = now; UpdatedAt = now; }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public DateTimeOffset WindowStart { get; private set; }
    public DateTimeOffset WindowEnd { get; private set; }
    public string Status { get; private set; } = ""; public int AttemptCount { get; private set; }
    public int RecipientCount { get; private set; }
    public int SuccessCount { get; private set; }
    public string? FailureCode { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
    public void Claim(DateTimeOffset now) { if (Status != FailureAlertStatus.Pending) throw new InvalidOperationException(); Status = FailureAlertStatus.Sending; AttemptCount = 1; StartedAt = now; UpdatedAt = now; }
    public void Complete(int recipients, int successes, string? code, DateTimeOffset now) { if (Status != FailureAlertStatus.Sending) throw new InvalidOperationException(); RecipientCount = recipients; SuccessCount = successes; FailureCode = code; FinishedAt = now; UpdatedAt = now; Status = successes == recipients && recipients > 0 ? FailureAlertStatus.Delivered : successes > 0 ? FailureAlertStatus.PartiallyDelivered : FailureAlertStatus.Failed; }
    public void Recover(DateTimeOffset now) => Complete(RecipientCount, 0, "ALERT_WORKER_INTERRUPTED", now);
}
