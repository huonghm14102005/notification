using Notification.Domain.Alerts;

namespace Notification.Domain.Tests.Alerts;

public sealed class FailureAlertTests
{
    [Fact]
    public void CompleteCalculatesAggregateResult()
    {
        var now = DateTimeOffset.UtcNow; var alert = new FailureAlert(Guid.NewGuid(), Guid.NewGuid(), now, now.AddMinutes(15), now);
        alert.Claim(now.AddMinutes(16)); alert.Complete(3, 2, "ALERT_SEND_FAILED", now.AddMinutes(16));
        Assert.Equal(FailureAlertStatus.PartiallyDelivered, alert.Status); Assert.Equal(1, alert.AttemptCount); Assert.Equal(2, alert.SuccessCount);
    }

    [Fact]
    public void RecoverDoesNotAllowAnotherClaim()
    {
        var now = DateTimeOffset.UtcNow; var alert = new FailureAlert(Guid.NewGuid(), Guid.NewGuid(), now, now.AddMinutes(15), now);
        alert.Claim(now.AddMinutes(16)); alert.Recover(now.AddMinutes(20));
        Assert.Equal(FailureAlertStatus.Failed, alert.Status); Assert.Equal("ALERT_WORKER_INTERRUPTED", alert.FailureCode);
        Assert.Throws<InvalidOperationException>(() => alert.Claim(now.AddMinutes(21)));
    }
}
