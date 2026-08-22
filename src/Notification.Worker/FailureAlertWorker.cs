using System.Text;
using Microsoft.Extensions.Options;
using Notification.Application.Abstractions.Email;
using Notification.Application.Alerts;
using Notification.Application.Senders;
using Notification.Infrastructure.Configuration;

namespace Notification.Worker;

public sealed class FailureAlertWorker(IServiceScopeFactory scopes, IOptions<AlertOptions> options, ILogger<FailureAlertWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var delay = TimeSpan.FromMilliseconds(options.Value.PollIntervalMs);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope(); var repo = scope.ServiceProvider.GetRequiredService<IFailureAlertRepository>(); var now = DateTimeOffset.UtcNow;
                await repo.RecoverAsync(now.AddSeconds(-options.Value.StuckAfterSeconds), now, 100, ct);
                var rows = await repo.ClaimAsync(now, options.Value.ClaimLimit, ct);
                foreach (var row in rows) await SendAsync(scope.ServiceProvider, row, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Failure alert polling cycle failed"); }
            try { await Task.Delay(delay, ct); } catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }

    private async Task SendAsync(IServiceProvider services, ClaimedFailureAlert item, CancellationToken ct)
    {
        var repo = services.GetRequiredService<IFailureAlertRepository>(); var successes = 0; string? code = null;
        ResolvedSender? sender;
        try { sender = await services.GetRequiredService<ISenderResolver>().ResolveAsync(item.TenantId, null, ct); }
        catch { sender = null; }
        if (sender is null) code = "ALERT_SENDER_UNAVAILABLE";
        else if (item.Recipients.Count == 0) code = "ALERT_RECIPIENT_MISSING";
        else
        {
            var subject = $"[Notification] {item.TotalCount} delivery failures"; var body = BuildBody(item);
            foreach (var recipient in item.Recipients)
            {
                try { await services.GetRequiredService<IEmailSender>().SendAsync(sender, recipient, subject, body, ct); successes++; }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch { code = "ALERT_SEND_FAILED"; }
            }
        }
        await repo.CompleteAsync(item.Id, item.Recipients.Count, successes, code, DateTimeOffset.UtcNow, ct);
        if (code is not null) logger.LogError("Failure alert {AlertId} for tenant {TenantId} completed with {ErrorCode}", item.Id, item.TenantId, code);
        else logger.LogInformation("Failure alert {AlertId} delivered for tenant {TenantId} to {RecipientCount} recipients", item.Id, item.TenantId, item.Recipients.Count);
    }

    private static string BuildBody(ClaimedFailureAlert x)
    {
        var b = new StringBuilder().AppendLine($"Window: {x.WindowStart:O} — {x.WindowEnd:O}").AppendLine($"Total: {x.TotalCount}");
        foreach (var g in x.Groups) b.AppendLine($"{g.Channel} / {g.ErrorCode}: {g.Count}");
        return b.AppendLine($"Lookup: GET /v1/notifications?status=failed&from={Uri.EscapeDataString(x.WindowStart.ToString("O"))}&to={Uri.EscapeDataString(x.WindowEnd.ToString("O"))}").ToString();
    }
}
