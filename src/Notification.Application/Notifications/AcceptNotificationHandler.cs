using Notification.Application.Abstractions.Observability;
using Notification.Application.Abstractions.Security;
using Notification.Application.Abstractions.Time;
using Notification.Application.Senders;
using Notification.Domain.Notifications;

namespace Notification.Application.Notifications;

public sealed class AcceptNotificationHandler(INotificationRepository repository, ISenderResolver senderResolver,
    ISecretCipher cipher, IClock clock, NotificationMetrics metrics)
{
    public async Task<AcceptNotificationResult> HandleAsync(Guid tenantId, Guid apiKeyId, AcceptNotificationCommand command, CancellationToken ct)
    {
        ResolvedSender sender;
        try { sender = await senderResolver.ResolveAsync(tenantId, command.SenderKey, ct); }
        catch (SenderOperationException) { throw new NotificationOperationException("SENDER_NOT_FOUND"); }

        var id = Guid.NewGuid(); var now = clock.UtcNow;
        var notification = new OutboundNotification(id, tenantId, apiKeyId,
            cipher.Encrypt(command.Subject, tenantId, id), cipher.Encrypt(command.Body, tenantId, id), now);
        var delivery = new Notification.Domain.Notifications.Delivery(Guid.NewGuid(), tenantId, id, sender.Id,
            command.Recipient.Email, command.Recipient.Ref, now);
        try { await repository.AddAsync(notification, delivery, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { throw new NotificationOperationException("SERVICE_UNAVAILABLE"); }
        metrics.Accepted.Add(1);
        return new(1, [new(id, delivery.Id, command.Recipient.Email, command.Recipient.Ref)]);
    }
}
