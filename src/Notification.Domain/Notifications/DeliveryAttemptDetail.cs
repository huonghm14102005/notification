namespace Notification.Domain.Notifications;

/// <summary>
/// Chi tiết một lần gửi trong danh sách attempts của thông báo.
/// </summary>
public sealed record DeliveryAttemptDetail(
    int AttemptNo,
    string Result,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    string? ErrorCode,
    string? ErrorMessage,
    string? ProviderMessageId
);
