using Notification.Application.Abstractions.Security;
using Notification.Domain.Notifications;

namespace Notification.Application.Notifications;

public sealed class GetNotificationHandler(
    INotificationRepository repository,
    ISecretCipher cipher)
{
    public async Task<NotificationDetail?> HandleAsync(GetNotificationQuery query, CancellationToken ct)
    {
        var notification = await repository.GetWithAttemptsAsync(query.TenantId, query.NotificationId, ct);
        if (notification is null)
            return null;

        // API key chỉ thấy notification do chính nó tạo
        if (query.Caller.Type == NotificationCallerType.ApiKey && notification.ApiKeyId != query.Caller.ApiKeyId)
            return null;

        // Giải mã nội dung nếu caller là Admin
        string? subject = null;
        string? body = null;
        string? recipientRef = null;

        if (query.Caller.Type == NotificationCallerType.Admin)
        {
            subject = cipher.Decrypt(notification.SubjectEncrypted, query.TenantId, notification.Id);
            body = cipher.Decrypt(notification.BodyEncrypted, query.TenantId, notification.Id);
            recipientRef = notification.RecipientRef;
        }

        // Lấy sender key từ sender
        var senderKey = query.Caller.Type == NotificationCallerType.Admin ? notification.SenderKey : null;

        // Chuyển delivery attempts thành DTO
        var attempts = notification.Attempts
            .Select(a => new DeliveryAttemptDetail(
                a.AttemptNo,
                a.Result,
                a.StartedAt,
                a.FinishedAt,
                a.ErrorCode,
                a.ErrorMessage,
                query.Caller.Type == NotificationCallerType.Admin ? a.ProviderMessageId : null))
            .ToList();

        return new NotificationDetail(
            notification.Id,
            notification.TenantId,
            notification.ProducerName,
            senderKey,
            notification.Status,
            notification.RecipientEmail,
            recipientRef,
            subject,
            body,
            notification.CreatedAt,
            notification.SentAt,
            notification.UpdatedAt,
            notification.FailureReason,
            attempts);
    }
}
