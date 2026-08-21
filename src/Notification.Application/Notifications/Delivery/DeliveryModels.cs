using Notification.Application.Senders;

namespace Notification.Application.Notifications.Delivery;

public sealed record ClaimedNotification(Guid Id, Guid NotificationId, Guid TenantId, Guid SenderId, int AttemptNo);
public sealed record DeliveryWorkItem(Guid Id, Guid NotificationId, Guid TenantId, Guid SenderId, int AttemptNo, string Status,
    string RecipientEmail, byte[] SubjectEncrypted, byte[] BodyEncrypted, ResolvedSender? Sender);
public sealed record DeliveryOutcome(string Status, string? ErrorCode = null);
public sealed record RecoveredNotification(Guid Id, Guid NotificationId, Guid TenantId, Guid SenderId, int AttemptNo, bool Terminal, bool Invalid = false);
