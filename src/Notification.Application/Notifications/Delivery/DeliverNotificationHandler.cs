using Notification.Application.Abstractions.Email;
using Notification.Application.Abstractions.Observability;
using Notification.Application.Abstractions.Security;
using Notification.Application.Abstractions.Time;

namespace Notification.Application.Notifications.Delivery;

public sealed class DeliverNotificationHandler(IDeliveryRepository repository, IEmailSender emailSender, ISecretCipher cipher,
    IClock clock, NotificationMetrics metrics)
{
    public async Task<DeliveryOutcome> HandleAsync(Guid notificationId, int attemptNo, CancellationToken ct)
    {
        var item = await repository.LoadClaimedAsync(notificationId, attemptNo, ct);
        if (item is null || item.Status != "sending") return new("skipped");
        var started = clock.UtcNow;
        if (item.Sender is null || item.Sender.Status != "active") return await Fail(item, "SENDER_UNAVAILABLE", "Sender is unavailable.", started, ct);
        string subject; string body;
        try { subject = cipher.Decrypt(item.SubjectEncrypted, item.TenantId, item.Id); body = cipher.Decrypt(item.BodyEncrypted, item.TenantId, item.Id); }
        catch { return await Fail(item, "CONTENT_DECRYPTION_FAILED", "Content could not be decrypted.", started, ct); }
        try
        {
            var providerId = await emailSender.SendAsync(item.Sender, item.RecipientEmail, subject, body, ct);
            var finished = clock.UtcNow;
            if (!await repository.CompleteSuccessAsync(item, providerId, started, finished, ct)) return new("skipped");
            metrics.Attempts.Add(1, new KeyValuePair<string, object?>("result", "success")); metrics.Sent.Add(1); return new("sent");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (EmailSendException ex) { var code = Map(ex.Reason, ex.Timeout); return await Fail(item, code, $"Email delivery failed: {ex.Reason}.", started, ct); }
        catch { return await Fail(item, "UNEXPECTED_ERROR", "Email delivery failed.", started, ct); }
    }

    private async Task<DeliveryOutcome> Fail(DeliveryWorkItem item, string code, string message, DateTimeOffset started, CancellationToken ct)
    {
        if (await repository.CompleteFailureAsync(item, code, message, started, clock.UtcNow, ct))
        { metrics.Attempts.Add(1, new KeyValuePair<string, object?>("result", "permanent_failure")); metrics.Failed.Add(1); }
        return new("failed", code);
    }
    private static string Map(string reason, bool timeout) => timeout ? "SMTP_TIMEOUT" : reason switch
    { "authentication" => "SMTP_AUTHENTICATION", "tls" or "tls_handshake" or "tls_not_supported" => "SMTP_TLS", "dns" => "SMTP_DNS", "connection" => "SMTP_CONNECTION", "recipient_rejected" => "RECIPIENT_REJECTED", _ => "SMTP_PROVIDER" };
}
