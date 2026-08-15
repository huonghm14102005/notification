using Notification.Application.Notifications;
using Notification.Domain.Notifications;

namespace Notification.Infrastructure.Persistence;

public sealed class NotificationRepository(NotificationDbContext db) : INotificationRepository
{
    public async Task AddAsync(OutboundNotification notification, CancellationToken ct)
    {
        db.Notifications.Add(notification); await db.SaveChangesAsync(ct);
    }
}
