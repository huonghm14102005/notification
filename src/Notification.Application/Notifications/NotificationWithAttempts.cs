namespace Notification.Application.Notifications;

/// <summary>
/// Thông báo kèm danh sách delivery attempts để truy vấn trong HIST-001.
/// </summary>
public sealed record NotificationWithAttempts(Guid Id, Guid TenantId, Guid ApiKeyId, string ProducerName, string SenderKey,
    string Status, string RecipientEmail, string? RecipientRef, byte[] SubjectEncrypted, byte[] BodyEncrypted,
    DateTimeOffset CreatedAt, DateTimeOffset? SentAt, DateTimeOffset UpdatedAt, string? FailureReason,
    IReadOnlyList<Notification.Domain.Notifications.DeliveryAttemptDetail> Attempts);
