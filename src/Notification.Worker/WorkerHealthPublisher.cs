using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Notification.Infrastructure.Configuration;

namespace Notification.Worker;

public sealed class WorkerHealthPublisher(
    HealthCheckService healthCheckService,
    IOptions<FoundationOptions> options,
    ILogger<WorkerHealthPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(options.Value.WorkerHealthIntervalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            var report = await healthCheckService.CheckHealthAsync(
                check => check.Tags.Contains("ready"), stoppingToken);
            if (report.Status == HealthStatus.Unhealthy)
            {
                DeleteHealthFile(options.Value.WorkerHealthFile);
                logger.LogWarning("Worker dependencies are unhealthy");
            }
            else
            {
                await WriteHealthFileAsync(options.Value.WorkerHealthFile, stoppingToken);
                logger.LogInformation("Worker dependencies are healthy");
            }
            await Task.Delay(interval, stoppingToken);
        }
    }

    private static async Task WriteHealthFileAsync(string path, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(path, DateTimeOffset.UtcNow.ToString("O"), cancellationToken);
    }

    private static void DeleteHealthFile(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
