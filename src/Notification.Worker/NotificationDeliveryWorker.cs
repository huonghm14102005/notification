using Microsoft.Extensions.Options;
using Notification.Application.Abstractions.Observability;
using Notification.Application.Notifications.Delivery;
using Notification.Infrastructure.Configuration;

namespace Notification.Worker;

public sealed class NotificationDeliveryWorker(IServiceScopeFactory scopes, IOptions<DeliveryWorkerOptions> options,
    NotificationMetrics metrics, ILogger<NotificationDeliveryWorker> logger) : BackgroundService
{
    private const int RecoveryBatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromMilliseconds(options.Value.PollIntervalMs);
        var sweepInterval = TimeSpan.FromSeconds(options.Value.SweepIntervalSeconds);
        var nextSweepAt = DateTimeOffset.UtcNow + sweepInterval;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                if (now >= nextSweepAt)
                {
                    await RecoverStuckAsync(now, stoppingToken);
                    nextSweepAt = now + sweepInterval;
                }
                IReadOnlyList<ClaimedNotification> claimed;
                using (var scope = scopes.CreateScope())
                    claimed = await scope.ServiceProvider.GetRequiredService<IDeliveryRepository>()
                        .ClaimDueAsync(DateTimeOffset.UtcNow, options.Value.Concurrency, stoppingToken);
                await Task.WhenAll(claimed.Select(x => DeliverAsync(x, stoppingToken)));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Delivery polling cycle failed"); }
            try { await Task.Delay(delay, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task RecoverStuckAsync(DateTimeOffset now, CancellationToken ct)
    {
        IReadOnlyList<RecoveredNotification> recovered;
        using (var scope = scopes.CreateScope())
            recovered = await scope.ServiceProvider.GetRequiredService<IDeliveryRepository>().RecoverStuckAsync(
                now, now.AddSeconds(-options.Value.StuckAfterSeconds), RecoveryBatchSize, ct);

        foreach (var item in recovered)
        {
            if (item.Invalid)
            {
                logger.LogError("Skipped invalid stuck delivery for tenant {TenantId}, notification {NotificationId}, sender {SenderId}, attempt {AttemptNo}",
                    item.TenantId, item.Id, item.SenderId, item.AttemptNo);
                continue;
            }

            metrics.Attempts.Add(1, new KeyValuePair<string, object?>("result", "transient_failure"));
            metrics.Recovered.Add(1);
            if (item.Terminal) metrics.Failed.Add(1);
            logger.LogWarning("Recovered stuck delivery for tenant {TenantId}, notification {NotificationId}, sender {SenderId}, attempt {AttemptNo}, terminal {Terminal}",
                item.TenantId, item.Id, item.SenderId, item.AttemptNo, item.Terminal);
        }
    }

    private async Task DeliverAsync(ClaimedNotification item, CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var outcome = await scope.ServiceProvider.GetRequiredService<DeliverNotificationHandler>().HandleAsync(item.Id, item.AttemptNo, ct);
            if (outcome.Status == "retrying")
                logger.LogWarning("Delivery will retry for tenant {TenantId}, notification {NotificationId}, sender {SenderId}, attempt {AttemptNo}, error {ErrorCode}",
                    item.TenantId, item.Id, item.SenderId, item.AttemptNo, outcome.ErrorCode);
            else
                logger.LogInformation("Delivery completed with {DeliveryStatus} for tenant {TenantId}, notification {NotificationId}, sender {SenderId}, attempt {AttemptNo}",
                    outcome.Status, item.TenantId, item.Id, item.SenderId, item.AttemptNo);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogError(exception, "Delivery handler failed for tenant {TenantId}, notification {NotificationId}, sender {SenderId}, attempt {AttemptNo}",
                item.TenantId, item.Id, item.SenderId, item.AttemptNo);
        }
    }
}
