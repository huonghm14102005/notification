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
    public async Task<AcceptNotificationResult> HandleAsync(Guid tenantId, Guid? apiKeyId, Guid? sourceDeviceId, Guid? adminId,
        AcceptNotificationCommand command, CancellationToken ct)
    {
        var now = clock.UtcNow;
        Guid effectiveApiKeyId;
        Guid effectiveDeviceId;

        if (apiKeyId.HasValue && sourceDeviceId.HasValue)
        {
            effectiveApiKeyId = apiKeyId.Value;
            effectiveDeviceId = sourceDeviceId.Value;
        }
        else
        {
            (effectiveApiKeyId, effectiveDeviceId) = await repository.EnsureAdminDispatchContextAsync(
                tenantId, adminId ?? Guid.NewGuid(), now, ct);
        }

        ResolvedSender sender;
        try { sender = await senderResolver.ResolveAsync(tenantId, command.SenderKey, ct); }
        catch (SenderOperationException) { throw new NotificationOperationException("SENDER_NOT_FOUND"); }

        ResolvedNotificationContent content;
        try { content = await ResolveContentAsync(tenantId, effectiveDeviceId, command.Content, ct); }
        catch (NotificationOperationException) { throw; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { throw new NotificationOperationException("SERVICE_UNAVAILABLE"); }
        var id = Guid.NewGuid();
        var notification = new OutboundNotification(id, tenantId, effectiveApiKeyId,
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

    public Task<AcceptNotificationResult> HandleAsync(Guid tenantId, Guid apiKeyId, Guid sourceDeviceId,
        AcceptNotificationCommand command, CancellationToken ct) =>
        HandleAsync(tenantId, apiKeyId, sourceDeviceId, null, command, ct);

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
