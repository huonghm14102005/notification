using Notification.Domain.Notifications;

namespace Notification.Domain.Tests.Notifications;

public sealed class OutboundNotificationTests
{
    [Fact]
    public void ScheduleRetryReturnsSendingNotificationToAccepted()
    {
        var now = new DateTimeOffset(2026, 8, 21, 0, 0, 0, TimeSpan.Zero);
        var notification = Create(now);
        notification.MarkSending(now);

        notification.ScheduleRetry(now.AddMinutes(1), now.AddSeconds(1));

        Assert.Equal(NotificationStatus.Accepted, notification.Status);
        Assert.Equal(1, notification.AttemptCount);
        Assert.Equal(now.AddMinutes(1), notification.NextAttemptAt);
        Assert.Null(notification.FailureReason);
    }

    [Fact]
    public void ScheduledRetryCanBeClaimedForNextAttempt()
    {
        var now = new DateTimeOffset(2026, 8, 21, 0, 0, 0, TimeSpan.Zero);
        var notification = Create(now);
        notification.MarkSending(now);
        notification.ScheduleRetry(now.AddMinutes(1), now.AddSeconds(1));

        notification.MarkSending(now.AddMinutes(1));

        Assert.Equal(NotificationStatus.Sending, notification.Status);
        Assert.Equal(2, notification.AttemptCount);
    }

    private static OutboundNotification Create(DateTimeOffset now) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "recipient@example.test", null, [1], [2], now);
}
