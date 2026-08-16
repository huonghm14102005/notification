using Notification.Domain.Notifications;

namespace Notification.Application.Notifications;

public interface INotificationRepository
{
    Task AddAsync(OutboundNotification notification, CancellationToken ct);

    /// <summary>
    /// Lấy thông báo cùng danh sách delivery attempts, sắp xếp tăng theo attemptNo.
    /// Trả null nếu notification không tồn tại hoặc thuộc tenant khác.
    /// </summary>
    Task<NotificationWithAttempts?> GetWithAttemptsAsync(Guid tenantId, Guid notificationId, CancellationToken ct);
}
