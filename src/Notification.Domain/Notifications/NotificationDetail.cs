namespace Notification.Domain.Notifications;

/// <summary>
/// Dữ liệu chi tiết của một thông báo cho tra cứu.
/// - Admin thấy nội dung plaintext + ref
/// - API key chỉ thấy metadata, không nội dung
/// </summary>
public sealed record NotificationDetail(
    Guid Id,
    Guid TenantId,
    string ProducerName,
    string? SenderKey,
    string Status,
    string RecipientEmail,
    string? RecipientRef,
    string? Subject,
    string? Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt,
    DateTimeOffset UpdatedAt,
    string? FailureReason,
    IReadOnlyList<DeliveryAttemptDetail> DeliveryAttempts
);
