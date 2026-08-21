using Notification.Application.Abstractions.Email;
using Notification.Application.Abstractions.Observability;
using Notification.Application.Abstractions.Security;
using Notification.Application.Abstractions.Time;

namespace Notification.Application.Notifications.Delivery;

public sealed class DeliverNotificationHandler(IDeliveryRepository repository, IEmailSender emailSender, ISecretCipher cipher,
    IClock clock, NotificationMetrics metrics)
{
    private const int MaxAttempts = 4;
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(25)];

    public async Task<DeliveryOutcome> HandleAsync(Guid deliveryId, int attemptNo, CancellationToken ct)
    {
        var item = await repository.LoadClaimedAsync(deliveryId, attemptNo, ct);
        if (item is null || item.Status != "sending") return new("skipped");
        var started = clock.UtcNow;
        if (item.Sender is null || item.Sender.Status != "active") return await FailPermanent(item, "SENDER_UNAVAILABLE", "Sender is unavailable.", started, ct);
        string subject; string body;
        try { subject = cipher.Decrypt(item.SubjectEncrypted, item.TenantId, item.NotificationId); body = cipher.Decrypt(item.BodyEncrypted, item.TenantId, item.NotificationId); }
        catch { return await FailPermanent(item, "CONTENT_DECRYPTION_FAILED", "Content could not be decrypted.", started, ct); }
        try
        {
            var providerId = await emailSender.SendAsync(item.Sender, item.RecipientEmail, subject, body, ct);
            var finished = clock.UtcNow;
            if (!await repository.CompleteSuccessAsync(item, providerId, started, finished, ct)) return new("skipped");
            metrics.Attempts.Add(1, new KeyValuePair<string, object?>("result", "success")); metrics.Sent.Add(1); return new("delivered");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (EmailSendException ex) when (ex.IsTransient) { return await FailTransient(item, ex.Code, "Email delivery failed temporarily.", started, ct); }
        catch (EmailSendException ex) { return await FailPermanent(item, ex.Code, "Email delivery failed.", started, ct); }
        catch { return await FailPermanent(item, "UNEXPECTED_ERROR", "Email delivery failed.", started, ct); }
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
