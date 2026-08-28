using Notification.Domain.Identity;

namespace Notification.Domain.Notifications;

public static class NotificationManualActionType
{
    public const string Retry = "retry";
    public const string Cancel = "cancel";
}

public sealed class NotificationManualAction
{
    private NotificationManualAction() { }
    public NotificationManualAction(Guid id, Guid tenantId, Guid adminId, Guid sourceNotificationId,
        Guid? resultNotificationId, string action, DateTimeOffset createdAt)
    {
        Id = id; TenantId = tenantId; AdminId = adminId; SourceNotificationId = sourceNotificationId;
        ResultNotificationId = resultNotificationId; Action = action; CreatedAt = createdAt;
    }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid AdminId { get; private set; }
    public Guid SourceNotificationId { get; private set; }
    public Guid? ResultNotificationId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
    public Admin Admin { get; private set; } = null!;
    public OutboundNotification SourceNotification { get; private set; } = null!;
    public OutboundNotification? ResultNotification { get; private set; }
}
