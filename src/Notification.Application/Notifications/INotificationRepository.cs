using Notification.Domain.Notifications;

namespace Notification.Application.Notifications;

public interface INotificationRepository
{
    Task AddAsync(OutboundNotification notification, CancellationToken ct);
}
