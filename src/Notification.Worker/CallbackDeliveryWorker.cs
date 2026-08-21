using Microsoft.Extensions.Options;
using Notification.Application.Abstractions.Observability;
using Notification.Application.Callbacks;
using Notification.Infrastructure.Configuration;

namespace Notification.Worker;

public sealed class CallbackDeliveryWorker(IServiceScopeFactory scopes, IOptions<CallbackOptions> options,
    NotificationMetrics metrics, ILogger<CallbackDeliveryWorker> logger) : BackgroundService
{
    private const int RecoveryBatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var poll = TimeSpan.FromMilliseconds(options.Value.PollIntervalMs);
        var nextRecovery = DateTimeOffset.UtcNow.AddSeconds(options.Value.StuckAfterSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                if (now >= nextRecovery)
                {
                    using var recoveryScope = scopes.CreateScope();
                    var recovered = await recoveryScope.ServiceProvider.GetRequiredService<ICallbackRepository>()
                        .RecoverStuckAsync(now, now.AddSeconds(-options.Value.StuckAfterSeconds), RecoveryBatchSize, stoppingToken);
                    foreach (var item in recovered)
                    {
                        metrics.CallbackAttempts.Add(1, new KeyValuePair<string, object?>("result", "transient_failure"));
                        logger.LogWarning("Recovered stuck callback event {EventId} for tenant {TenantId}, device {DeviceId}, attempt {AttemptNo}", item.EventId, item.TenantId, item.DeviceId, item.AttemptNo);
                    }
                    nextRecovery = now.AddSeconds(options.Value.StuckAfterSeconds);
                }
                IReadOnlyList<ClaimedCallback> claimed;
                using (var scope = scopes.CreateScope())
                    claimed = await scope.ServiceProvider.GetRequiredService<ICallbackRepository>()
                        .ClaimDueAsync(now, options.Value.Concurrency, stoppingToken);
                await Task.WhenAll(claimed.Select(x => DeliverAsync(x, stoppingToken)));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Callback polling cycle failed"); }
            try { await Task.Delay(poll, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task DeliverAsync(ClaimedCallback item, CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var outcome = await scope.ServiceProvider.GetRequiredService<DeliverCallbackHandler>().HandleAsync(item.EventId, item.AttemptNo, ct);
            if (outcome != "skipped") metrics.CallbackAttempts.Add(1, new KeyValuePair<string, object?>("result", outcome));
            if (outcome == "retrying") logger.LogWarning("Callback will retry for event {EventId}, tenant {TenantId}, device {DeviceId}, attempt {AttemptNo}", item.EventId, item.TenantId, item.DeviceId, item.AttemptNo);
            else logger.LogInformation("Callback completed with {Status} for event {EventId}, tenant {TenantId}, device {DeviceId}, attempt {AttemptNo}", outcome, item.EventId, item.TenantId, item.DeviceId, item.AttemptNo);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception exception) { logger.LogError(exception, "Callback handler failed for event {EventId}, tenant {TenantId}, device {DeviceId}, attempt {AttemptNo}", item.EventId, item.TenantId, item.DeviceId, item.AttemptNo); }
    }
}
