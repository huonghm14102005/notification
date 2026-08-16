namespace Notification.Application.Notifications;

/// <summary>
/// Truy vấn lấy chi tiết một thông báo kèm delivery attempts.
/// AuthCaller phân biệt quyền hạn: nếu là API key thì chỉ thấy siêu dữ liệu của notification do key tạo.
/// </summary>
public sealed record GetNotificationQuery(
    Guid TenantId,
    Guid NotificationId,
    AuthCaller Caller
);

/// <summary>
/// Thông tin người gọi: có thể là Admin (thấy đầy đủ) hoặc ApiKey (giới hạn quyền hạn).
/// </summary>
public enum NotificationCallerType { Admin, ApiKey }
public sealed record AuthCaller(NotificationCallerType Type, Guid? ApiKeyId);
