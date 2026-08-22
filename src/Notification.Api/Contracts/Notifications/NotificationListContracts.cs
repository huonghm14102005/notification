using System.Text.Json.Serialization;

namespace Notification.Api.Contracts.Notifications;

public sealed record NotificationDeliveryListResponse(Guid Id, string Channel, string Target,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TargetRef,
    string Status, int AttemptCount,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ErrorCode);
public sealed record NotificationListItemResponse(Guid Id,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? SourceDeviceId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? ApiKeyId,
    string ProducerName, string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? CompletedAt,
    IReadOnlyList<NotificationDeliveryListResponse> Deliveries);
public sealed record NotificationListResponse(IReadOnlyList<NotificationListItemResponse> Items, string? NextCursor);
