using Notification.Domain.Identity;
using Notification.Domain.Senders;
using Notification.Domain.Templates;

namespace Notification.Domain.Notifications;

public sealed class OutboundNotification
{
    private OutboundNotification() { }

    public OutboundNotification(Guid id, Guid tenantId, Guid apiKeyId, Guid? templateId,
        byte[] subjectEncrypted, byte[]? textBodyEncrypted, byte[]? htmlBodyEncrypted, DateTimeOffset now)
    {
        if (textBodyEncrypted is null && htmlBodyEncrypted is null) throw new ArgumentException("At least one body is required.");
        Id = id; TenantId = tenantId; ApiKeyId = apiKeyId; TemplateId = templateId; SubjectEncrypted = subjectEncrypted;
        TextBodyEncrypted = textBodyEncrypted; HtmlBodyEncrypted = htmlBodyEncrypted;
        Status = NotificationStatus.Accepted; CreatedAt = now; UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ApiKeyId { get; private set; }
    public Guid? TemplateId { get; private set; }
    public byte[] SubjectEncrypted { get; private set; } = [];
    public byte[]? TextBodyEncrypted { get; private set; }
    public byte[]? HtmlBodyEncrypted { get; private set; }
    public string Status { get; private set; } = NotificationStatus.Accepted;
    public string? FailureReason { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
    public ApiKey ApiKey { get; private set; } = null!;
    public ContentTemplate? Template { get; private set; }
    public ICollection<Delivery> Deliveries { get; private set; } = [];

    public void SetAggregate(string status, string? reason, DateTimeOffset now)
    {
        if (Status is NotificationStatus.Delivered or NotificationStatus.PartiallyDelivered or NotificationStatus.Failed or NotificationStatus.Cancelled)
            throw new InvalidOperationException();
        Status = status; FailureReason = reason; UpdatedAt = now;
        if (status is NotificationStatus.Delivered or NotificationStatus.PartiallyDelivered or NotificationStatus.Failed or NotificationStatus.Cancelled)
            CompletedAt = now;
    }
}
