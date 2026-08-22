namespace Notification.Application.Notifications;

public sealed record RecipientInput(string Email, string? Ref);
public sealed record NotificationContentInput(string Mode, string? Subject = null, string? TextBody = null,
    string? TemplateCode = null, IReadOnlyDictionary<string, string>? Data = null);
public sealed record AcceptNotificationCommand(string? SenderKey, NotificationContentInput Content, RecipientInput Recipient);
public sealed record ResolvedNotificationContent(Guid? TemplateId, string Subject, string? TextBody, string? HtmlBody);
public sealed record AcceptedNotification(Guid Id, Guid DeliveryId, string Email, string? Ref);
public sealed record AcceptNotificationResult(int Accepted, IReadOnlyList<AcceptedNotification> Notifications);
public sealed class NotificationOperationException(string code, IReadOnlyList<string>? names = null) : Exception(code)
{
    public string Code { get; } = code;
    public IReadOnlyList<string>? Names { get; } = names;
}
