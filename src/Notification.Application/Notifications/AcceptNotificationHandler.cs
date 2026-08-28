using Notification.Application.Abstractions.Observability;
using Notification.Application.Abstractions.Security;
using Notification.Application.Abstractions.Time;
using Notification.Application.Senders;
using Notification.Application.Templates;
using Notification.Domain.Notifications;

namespace Notification.Application.Notifications;

public sealed class AcceptNotificationHandler(INotificationRepository repository, ISenderResolver senderResolver,
    ITemplateRepository templateRepository, ITemplateRenderer templateRenderer, ISecretCipher cipher, IClock clock,
    NotificationMetrics metrics)
{
    public async Task<AcceptNotificationResult> HandleAsync(Guid tenantId, Guid apiKeyId, Guid sourceDeviceId,
        AcceptNotificationCommand command, CancellationToken ct)
    {
        ResolvedSender sender;
        try { sender = await senderResolver.ResolveAsync(tenantId, command.SenderKey, ct); }
        catch (SenderOperationException) { throw new NotificationOperationException("SENDER_NOT_FOUND"); }

        ResolvedNotificationContent content;
        try { content = await ResolveContentAsync(tenantId, sourceDeviceId, command.Content, ct); }
        catch (NotificationOperationException) { throw; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { throw new NotificationOperationException("SERVICE_UNAVAILABLE"); }
        var id = Guid.NewGuid(); var now = clock.UtcNow;
        var notification = new OutboundNotification(id, tenantId, apiKeyId,
            content.TemplateId, cipher.Encrypt(content.Subject, tenantId, id),
            content.TextBody is null ? null : cipher.Encrypt(content.TextBody, tenantId, id),
            content.HtmlBody is null ? null : cipher.Encrypt(content.HtmlBody, tenantId, id), now);
        var channel = string.IsNullOrWhiteSpace(command.Channel) ? "email" : command.Channel.Trim().ToLowerInvariant();
        var delivery = new Notification.Domain.Notifications.Delivery(Guid.NewGuid(), tenantId, id, sender.Id,
            channel, command.Recipient.Email, command.Recipient.Ref, now);
        try { await repository.AddAsync(notification, delivery, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { throw new NotificationOperationException("SERVICE_UNAVAILABLE"); }
        metrics.Accepted.Add(1);
        return new(1, [new(id, delivery.Id, command.Recipient.Email, command.Recipient.Ref)]);
    }

    private async Task<ResolvedNotificationContent> ResolveContentAsync(Guid tenantId, Guid sourceDeviceId,
        NotificationContentInput content, CancellationToken ct)
    {
        if (content.Mode == "plaintext") return new(null, content.Subject!, content.TextBody!, null);
        var code = content.TemplateCode!.Trim().ToLowerInvariant();
        var template = await templateRepository.FindActiveAsync(tenantId, sourceDeviceId, code, ct)
            ?? throw new NotificationOperationException("TEMPLATE_NOT_FOUND");
        try
        {
            var rendered = templateRenderer.Render(template, content.Data!);
            return new(template.Id, rendered.Subject, rendered.TextBody, rendered.HtmlBody);
        }
        catch (TemplateOperationException exception)
        {
            throw new NotificationOperationException(exception.Code, exception.Names);
        }
    }
}
