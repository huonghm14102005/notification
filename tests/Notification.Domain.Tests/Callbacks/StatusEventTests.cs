using Notification.Domain.Callbacks;

namespace Notification.Domain.Tests.Callbacks;

public sealed class StatusEventTests
{
    [Fact]
    public void RetryAndTerminalTransitionsPreserveAttemptCount()
    {
        var now = new DateTimeOffset(2026, 8, 21, 0, 0, 0, TimeSpan.Zero);
        var item = Create(now); item.MarkSending(now); item.ScheduleRetry(now.AddMinutes(1), now);
        item.MarkSending(now.AddMinutes(1)); item.MarkFailed("CALLBACK_TIMEOUT", now.AddMinutes(1));
        Assert.Equal(2, item.AttemptCount); Assert.Equal(CallbackEventStatus.Failed, item.Status); Assert.Null(item.NextAttemptAt);
    }

    [Fact]
    public void CancelIsIdempotentAndTerminal()
    {
        var now = DateTimeOffset.UtcNow; var item = Create(now);
        item.Cancel("CALLBACK_DISABLED", now); item.Cancel("DEVICE_DISABLED", now.AddSeconds(1));
        Assert.Equal(CallbackEventStatus.Cancelled, item.Status); Assert.Equal("CALLBACK_DISABLED", item.FailureCode);
    }

    private static StatusEvent Create(DateTimeOffset now) => new(Guid.NewGuid(), $"evt_{Guid.NewGuid():N}",
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), [1], now);
}
