using Notification.Application.Abstractions.Security;
using Notification.Application.Abstractions.Time;

namespace Notification.Application.Callbacks;

public sealed class DeliverCallbackHandler(ICallbackRepository repository, ICallbackSender sender, ISecretCipher cipher, IClock clock)
{
    private const int MaxAttempts = 6;
    private static readonly TimeSpan[] Delays = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(25), TimeSpan.FromHours(2), TimeSpan.FromHours(12)];

    public async Task<string> HandleAsync(Guid eventId, int attemptNo, CancellationToken ct)
    {
        var item = await repository.LoadClaimedAsync(eventId, attemptNo, ct);
        if (item is null)
            return await repository.CancelClaimedAsync(eventId, attemptNo, clock.UtcNow, ct) ? "cancelled" : "skipped";
        if (item.Status != "sending") return "skipped";
        var started = clock.UtcNow; CallbackSendResult result;
        try
        {
            var secret = cipher.Decrypt(item.SecretEncrypted, item.TenantId, item.DeviceId);
            var payload = cipher.Decrypt(item.PayloadEncrypted, item.TenantId, item.EventId);
            result = await sender.SendAsync(item.Url, secret, item.PublicId, payload, started, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { result = new(false, false, null, "CALLBACK_UNEXPECTED_ERROR"); }
        var finished = clock.UtcNow;
        DateTimeOffset? next = result.Transient && attemptNo < MaxAttempts ? finished + Delays[attemptNo - 1] : null;
        if (!await repository.CompleteAsync(item, result, started, finished, next, ct)) return "skipped";
        if (result.Success) return "delivered";
        return next.HasValue ? "retrying" : "failed";
    }
}
