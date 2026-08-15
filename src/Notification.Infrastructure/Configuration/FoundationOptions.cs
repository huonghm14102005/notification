namespace Notification.Infrastructure.Configuration;

public sealed class FoundationOptions
{
    public const string SectionName = "Foundation";

    public string DatabaseUrl { get; set; } = string.Empty;

    public string RedisUrl { get; set; } = string.Empty;

    public int HealthCheckTimeoutSeconds { get; set; } = 3;

    public int WorkerHealthIntervalSeconds { get; set; } = 10;

    public string WorkerHealthFile { get; set; } = "/tmp/notification-worker-health";
}
