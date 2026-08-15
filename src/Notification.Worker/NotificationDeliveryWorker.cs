using Microsoft.Extensions.Options;
using Notification.Application.Notifications.Delivery;
using Notification.Infrastructure.Configuration;

namespace Notification.Worker;

public sealed class NotificationDeliveryWorker(IServiceScopeFactory scopes, IOptions<DeliveryWorkerOptions> options,
    ILogger<NotificationDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromMilliseconds(options.Value.PollIntervalMs);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
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

    private async Task DeliverAsync(ClaimedNotification item, CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var outcome = await scope.ServiceProvider.GetRequiredService<DeliverNotificationHandler>().HandleAsync(item.Id, item.AttemptNo, ct);
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
