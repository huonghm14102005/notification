using Notification.Application.Abstractions.Time;

namespace Notification.Application.Notifications;

public sealed record ManualRetryResult(bool Created, Guid Id, Guid SourceNotificationId, string Status, DateTimeOffset CreatedAt);

public sealed class ManualNotificationHandlers(INotificationRepository repository, IClock clock)
{
    public async Task<ManualRetryResult> RetryAsync(Guid tenantId, Guid adminId, Guid id, CancellationToken ct)
    {
        try { return await repository.RetryAsync(tenantId, adminId, id, clock.UtcNow, ct); }
        catch (NotificationOperationException) { throw; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { throw new NotificationOperationException("SERVICE_UNAVAILABLE"); }
    }

    public async Task CancelAsync(Guid tenantId, Guid adminId, Guid id, CancellationToken ct)
    {
        try { await repository.CancelAsync(tenantId, adminId, id, clock.UtcNow, ct); }
        catch (NotificationOperationException) { throw; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { throw new NotificationOperationException("SERVICE_UNAVAILABLE"); }
    }

    public async Task<bool> DeleteAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        try { return await repository.DeleteAsync(tenantId, id, ct); }
        catch (NotificationOperationException) { throw; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { throw new NotificationOperationException("SERVICE_UNAVAILABLE"); }
    }
}
