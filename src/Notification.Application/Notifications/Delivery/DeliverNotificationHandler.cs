using Notification.Application.Abstractions.Channels;
using Notification.Application.Abstractions.Email;
using Notification.Application.Abstractions.Observability;
using Notification.Application.Abstractions.Security;
using Notification.Application.Abstractions.Time;
using Notification.Application.Devices;

namespace Notification.Application.Notifications.Delivery;

public sealed class DeliverNotificationHandler(
    IDeliveryRepository repository,
    IEmailSender emailSender,
    ITelegramSender? telegramSender,
    IDiscordSender? discordSender,
    IPushSender? pushSender,
    IDeviceRepository? deviceRepository,
    ISecretCipher cipher,
    IClock clock,
    NotificationMetrics metrics)
{
    public DeliverNotificationHandler(IDeliveryRepository repository, IEmailSender emailSender, ISecretCipher cipher,
        IClock clock, NotificationMetrics metrics)
        : this(repository, emailSender, null, null, null, null, cipher, clock, metrics)
    {
    }

    public DeliverNotificationHandler(IDeliveryRepository repository, IEmailSender emailSender,
        ITelegramSender? telegramSender, IDiscordSender? discordSender, ISecretCipher cipher,
        IClock clock, NotificationMetrics metrics)
        : this(repository, emailSender, telegramSender, discordSender, null, null, cipher, clock, metrics)
    {
    }

    private const int MaxAttempts = 4;
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(25)];

    public async Task<DeliveryOutcome> HandleAsync(Guid deliveryId, int attemptNo, CancellationToken ct)
    {
        var item = await repository.LoadClaimedAsync(deliveryId, attemptNo, ct);
        if (item is null || item.Status != "sending") return new("skipped");
        var started = clock.UtcNow;

        var channel = item.Channel?.ToLowerInvariant() ?? "email";

        if (channel == "email")
        {
            if (item.Sender is null || item.Sender.Status != "active")
                return await FailPermanent(item, "SENDER_UNAVAILABLE", "Sender is unavailable.", started, ct);
        }

        string subject; string? textBody; string? htmlBody;
        try
        {
            subject = cipher.Decrypt(item.SubjectEncrypted, item.TenantId, item.NotificationId);
            textBody = item.TextBodyEncrypted is null ? null : cipher.Decrypt(item.TextBodyEncrypted, item.TenantId, item.NotificationId);
            htmlBody = item.HtmlBodyEncrypted is null ? null : cipher.Decrypt(item.HtmlBodyEncrypted, item.TenantId, item.NotificationId);
        }
        catch { return await FailPermanent(item, "CONTENT_DECRYPTION_FAILED", "Content could not be decrypted.", started, ct); }

        try
        {
            string? providerId;
            if (channel == "email")
            {
                providerId = await emailSender.SendAsync(item.Sender!, item.Target, subject, textBody, htmlBody, ct);
            }
            else if (channel == "telegram")
            {
                if (telegramSender is null) throw new NotSupportedException("Telegram sender is not registered.");
                providerId = await telegramSender.SendAsync(item.Sender, item.Target, subject, textBody, htmlBody, ct);
            }
            else if (channel == "discord")
            {
                if (discordSender is null) throw new NotSupportedException("Discord sender is not registered.");
                providerId = await discordSender.SendAsync(item.Sender, item.Target, subject, textBody, htmlBody, ct);
            }
            else if (channel == "push")
            {
                if (pushSender is null || deviceRepository is null)
                    throw new NotSupportedException("Push sender or device repository is not registered.");

                if (!Guid.TryParse(item.Target, out var targetDeviceId))
                    return await FailPermanent(item, "DEVICE_NOT_FOUND", "Invalid target device ID.", started, ct);

                var pushEndpoint = await deviceRepository.FindActivePushEndpointAsync(item.TenantId, targetDeviceId, ct);
                if (pushEndpoint is null)
                    return await FailPermanent(item, "PUSH_ENDPOINT_NOT_FOUND", "Active push endpoint not found for device.", started, ct);

                var token = cipher.Decrypt(pushEndpoint.TokenEncrypted, item.TenantId, targetDeviceId);
                providerId = await pushSender.SendAsync(pushEndpoint.Platform, token, subject, textBody, null, ct);
            }
            else
            {
                return await FailPermanent(item, "CHANNEL_NOT_SUPPORTED", $"Channel '{channel}' is not supported.", started, ct);
            }

            var finished = clock.UtcNow;
            if (!await repository.CompleteSuccessAsync(item, providerId, started, finished, ct)) return new("skipped");
            metrics.Attempts.Add(1, new KeyValuePair<string, object?>("result", "success")); metrics.Sent.Add(1); return new("delivered");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (EmailSendException ex) when (ex.IsTransient) { return await FailTransient(item, ex.Code, "Email delivery failed temporarily.", started, ct); }
        catch (EmailSendException ex) { return await FailPermanent(item, ex.Code, "Email delivery failed.", started, ct); }
        catch (ChannelSendException ex) when (ex.IsTransient) { return await FailTransient(item, ex.Code, ex.Message, started, ct); }
        catch (ChannelSendException ex)
        {
            if (channel == "push" && ex.Code == "PUSH_TOKEN_INVALID" && deviceRepository is not null && Guid.TryParse(item.Target, out var targetDeviceId))
            {
                await deviceRepository.DisablePushEndpointAsync(item.TenantId, targetDeviceId, clock.UtcNow, ct);
            }
            return await FailPermanent(item, ex.Code, ex.Message, started, ct);
        }
        catch (Exception ex) { return await FailPermanent(item, "UNEXPECTED_ERROR", ex.Message, started, ct); }
    }

    private async Task<DeliveryOutcome> FailTransient(DeliveryWorkItem item, string code, string message, DateTimeOffset started, CancellationToken ct)
    {
        var finished = clock.UtcNow;
        if (item.AttemptNo >= MaxAttempts)
        {
            if (await repository.CompleteTransientFailureAsync(item, code, message, null, started, finished, ct))
            {
                metrics.Attempts.Add(1, new KeyValuePair<string, object?>("result", "transient_failure"));
                metrics.Failed.Add(1);
            }
            return new("failed", code);
        }

        var nextAttemptAt = finished + RetryDelays[item.AttemptNo - 1];
        if (await repository.CompleteTransientFailureAsync(item, code, message, nextAttemptAt, started, finished, ct))
            metrics.Attempts.Add(1, new KeyValuePair<string, object?>("result", "transient_failure"));
        return new("retrying", code);
    }

    private async Task<DeliveryOutcome> FailPermanent(DeliveryWorkItem item, string code, string message, DateTimeOffset started, CancellationToken ct)
    {
        if (await repository.CompletePermanentFailureAsync(item, code, message, started, clock.UtcNow, ct))
        {
            metrics.Attempts.Add(1, new KeyValuePair<string, object?>("result", "permanent_failure"));
            metrics.Failed.Add(1);
        }
        return new("failed", code);
    }
}
