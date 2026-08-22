using Microsoft.EntityFrameworkCore;
using Notification.Application.Notifications;
using Notification.Domain.Notifications;

namespace Notification.Infrastructure.Persistence;

public sealed class NotificationRepository(NotificationDbContext db) : INotificationRepository
{
    public async Task AddAsync(OutboundNotification notification, Delivery delivery, CancellationToken ct)
    {
        db.Notifications.Add(notification); db.Deliveries.Add(delivery); await db.SaveChangesAsync(ct);
    }

    public async Task<NotificationWithAttempts?> GetWithAttemptsAsync(Guid tenantId, Guid notificationId, CancellationToken ct)
    {
        var notification = await db.Notifications.AsNoTracking()
            .Where(n => n.TenantId == tenantId && n.Id == notificationId)
            .Select(n => new
            {
                n.Id,
                n.TenantId,
                n.ApiKeyId,
                n.ApiKey.ProducerName,
                SenderKey = n.Deliveries.Select(d => d.Sender!.Key).First(),
                n.Status,
                RecipientEmail = n.Deliveries.Select(d => d.Target).First(),
                RecipientRef = n.Deliveries.Select(d => d.TargetRef).First(),
                n.SubjectEncrypted,
                n.TextBodyEncrypted,
                n.HtmlBodyEncrypted,
                n.CreatedAt,
                SentAt = n.CompletedAt,
                n.UpdatedAt,
                n.FailureReason
            }).SingleOrDefaultAsync(ct);

        if (notification is null) return null;

        var attempts = await db.DeliveryAttempts.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.Delivery.NotificationId == notificationId)
            .OrderBy(a => a.AttemptNo)
            .Take(4)
            .Select(a => new DeliveryAttemptDetail(a.AttemptNo, a.Result, a.StartedAt, a.FinishedAt, a.ErrorCode,
                a.ErrorMessage, a.ProviderMessageId))
            .ToListAsync(ct);

        return new(notification.Id, notification.TenantId, notification.ApiKeyId, notification.ProducerName,
            notification.SenderKey, notification.Status, notification.RecipientEmail, notification.RecipientRef,
            notification.SubjectEncrypted, notification.TextBodyEncrypted, notification.HtmlBodyEncrypted,
            notification.CreatedAt, notification.SentAt,
            notification.UpdatedAt, notification.FailureReason, attempts);
    }
}
