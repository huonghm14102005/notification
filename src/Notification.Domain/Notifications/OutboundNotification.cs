using Notification.Domain.Identity;
using Notification.Domain.Senders;
using Notification.Domain.Templates;

namespace Notification.Domain.Notifications;

public sealed class OutboundNotification
{
    private OutboundNotification() { }

    public OutboundNotification(Guid id, Guid tenantId, Guid apiKeyId, Guid senderId, string recipientEmail,
        string? recipientRef, byte[] subjectEncrypted, byte[] bodyEncrypted, DateTimeOffset now)
    {
        Id = id; TenantId = tenantId; ApiKeyId = apiKeyId; SenderId = senderId; RecipientEmail = recipientEmail;
        RecipientRef = recipientRef; SubjectEncrypted = subjectEncrypted; BodyEncrypted = bodyEncrypted;
        Status = NotificationStatus.Accepted; AttemptCount = 0; NextAttemptAt = now; CreatedAt = now; UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ApiKeyId { get; private set; }
    public Guid SenderId { get; private set; }
    public Guid? TemplateId { get; private set; }
    public string RecipientEmail { get; private set; } = string.Empty;
    public string? RecipientRef { get; private set; }
    public byte[] SubjectEncrypted { get; private set; } = [];
    public byte[] BodyEncrypted { get; private set; } = [];
    public string Status { get; private set; } = NotificationStatus.Accepted;
    public int AttemptCount { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
    public ApiKey ApiKey { get; private set; } = null!;
    public Sender Sender { get; private set; } = null!;
    public ContentTemplate? Template { get; private set; }

    public void MarkSending(DateTimeOffset now) { if (Status != NotificationStatus.Accepted) throw new InvalidOperationException(); Status = NotificationStatus.Sending; AttemptCount++; UpdatedAt = now; }
    public void MarkSent(DateTimeOffset now) { if (Status != NotificationStatus.Sending) throw new InvalidOperationException(); Status = NotificationStatus.Sent; SentAt = now; NextAttemptAt = null; FailureReason = null; UpdatedAt = now; }
    public void ScheduleRetry(DateTimeOffset nextAttemptAt, DateTimeOffset now) { if (Status != NotificationStatus.Sending) throw new InvalidOperationException(); Status = NotificationStatus.Accepted; NextAttemptAt = nextAttemptAt; FailureReason = null; UpdatedAt = now; }
    public void MarkFailed(string reason, DateTimeOffset now) { if (Status != NotificationStatus.Sending) throw new InvalidOperationException(); Status = NotificationStatus.Failed; FailureReason = reason; NextAttemptAt = null; UpdatedAt = now; }
}
