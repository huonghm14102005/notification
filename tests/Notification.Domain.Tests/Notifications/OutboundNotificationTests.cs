using Notification.Domain.Notifications;

namespace Notification.Domain.Tests.Notifications;

public sealed class OutboundNotificationTests
{
    [Fact]
    public void ScheduleRetryReturnsSendingDeliveryToPending()
    {
        var now = new DateTimeOffset(2026, 8, 21, 0, 0, 0, TimeSpan.Zero);
        var delivery = Create(now);
        delivery.MarkSending(now);

        delivery.ScheduleRetry(now.AddMinutes(1), now.AddSeconds(1));

        Assert.Equal(DeliveryStatus.Pending, delivery.Status);
        Assert.Equal(1, delivery.AttemptCount);
        Assert.Equal(now.AddMinutes(1), delivery.NextAttemptAt);
        Assert.Null(delivery.FailureCode);
    }

    [Fact]
    public void ScheduledRetryCanBeClaimedForNextAttempt()
    {
        var now = new DateTimeOffset(2026, 8, 21, 0, 0, 0, TimeSpan.Zero);
        var delivery = Create(now);
        delivery.MarkSending(now);
        delivery.ScheduleRetry(now.AddMinutes(1), now.AddSeconds(1));

        delivery.MarkSending(now.AddMinutes(1));

        Assert.Equal(DeliveryStatus.Sending, delivery.Status);
        Assert.Equal(2, delivery.AttemptCount);
    }

    private static Delivery Create(DateTimeOffset now) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "recipient@example.test", null, now);
}
