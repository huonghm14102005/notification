using System.Text.Json.Serialization;

namespace Notification.Api.Contracts.Notifications;

/// <summary>
/// Response cho GET /v1/notifications/:id - Chi tiết một thông báo kèm danh sách attempts.
/// </summary>
public sealed record GetNotificationResponse(
    string Id,
    string TenantId,
    string ProducerName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SenderKey,
    string Status,
    string RecipientEmail,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RecipientRef,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Subject,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Body,
    string CreatedAt,
    string? SentAt,
    string UpdatedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? FailureReason,
    IReadOnlyList<DeliveryAttemptResponse> DeliveryAttempts
);

/// <summary>
/// Chi tiết một lần gửi trong danh sách attempts.
/// </summary>
public sealed record DeliveryAttemptResponse(
    int AttemptNo,
    string Result,
    string StartedAt,
    string FinishedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ErrorCode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ErrorMessage,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ProviderMessageId
);
