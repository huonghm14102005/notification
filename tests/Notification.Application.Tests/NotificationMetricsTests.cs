using System.Diagnostics.Metrics;
using Notification.Application.Abstractions.Observability;

namespace Notification.Application.Tests;

public sealed class NotificationMetricsTests
{
    [Fact]
    public void ExposesTheFiveStableInstrumentsWithoutHighCardinalityTags()
    {
        var observed = new HashSet<string>(StringComparer.Ordinal);
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == NotificationMetrics.MeterName)
            {
                observed.Add(instrument.Name);
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            Assert.DoesNotContain(tags.ToArray(), tag =>
                tag.Key is "email" or "apiKey" or "notificationId" or "correlationId" or "tenantId");
        });
        listener.Start();

        using var metrics = new NotificationMetrics();
        metrics.Accepted.Add(1);
        metrics.Sent.Add(1);
        metrics.Failed.Add(1);
        metrics.Attempts.Add(1);
        metrics.QueueDepth.Add(1);

        Assert.Equal(
            ["deliveries.failed", "deliveries.sent", "delivery.attempts", "notifications.accepted", "queue.depth"],
            observed.Order(StringComparer.Ordinal).ToArray());
    }
}
