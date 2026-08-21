namespace Notification.Application.Notifications;

public sealed record RecipientInput(string Email, string? Ref);
public sealed record AcceptNotificationCommand(string? SenderKey, string Subject, string Body, RecipientInput Recipient);
public sealed record AcceptedNotification(Guid Id, Guid DeliveryId, string Email, string? Ref);
public sealed record AcceptNotificationResult(int Accepted, IReadOnlyList<AcceptedNotification> Notifications);
public sealed class NotificationOperationException(string code) : Exception(code) { public string Code { get; } = code; }
