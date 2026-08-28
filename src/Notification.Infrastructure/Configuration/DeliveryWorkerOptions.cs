namespace Notification.Infrastructure.Configuration;

public sealed class DeliveryWorkerOptions
{
    public int PollIntervalMs { get; set; } = 2000;
    public int Concurrency { get; set; } = 5;
    public int SweepIntervalSeconds { get; set; } = 300;
    public int StuckAfterSeconds { get; set; } = 600;
    public int SmtpTimeoutMs { get; set; } = 30000;
}
