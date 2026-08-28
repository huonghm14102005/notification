using System.Diagnostics.Metrics;

namespace Notification.Application.Abstractions.Observability;

public sealed class NotificationMetrics : IDisposable
{
    public const string MeterName = "Notification.Server";

    private readonly Meter _meter = new(MeterName);

    public NotificationMetrics()
    {
        Accepted = _meter.CreateCounter<long>("notifications.accepted");
        Sent = _meter.CreateCounter<long>("deliveries.sent");
        Failed = _meter.CreateCounter<long>("deliveries.failed");
        Recovered = _meter.CreateCounter<long>("deliveries.recovered");
        CallbackAttempts = _meter.CreateCounter<long>("callback.attempts");
        Attempts = _meter.CreateCounter<long>("delivery.attempts");
        QueueDepth = _meter.CreateUpDownCounter<long>("queue.depth");
    }

    public Counter<long> Accepted { get; }

    public Counter<long> Sent { get; }

    public Counter<long> Failed { get; }

    public Counter<long> Recovered { get; }

    public Counter<long> CallbackAttempts { get; }

    public Counter<long> Attempts { get; }

    public UpDownCounter<long> QueueDepth { get; }

    public void Dispose() => _meter.Dispose();
}
