namespace Notification.Infrastructure.Configuration;

public sealed class DeliveryWorkerOptions
{
    public int PollIntervalMs { get; set; } = 2000;
    public int Concurrency { get; set; } = 5;
}
