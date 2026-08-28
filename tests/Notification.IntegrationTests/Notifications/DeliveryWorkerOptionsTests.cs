using Notification.Infrastructure.Configuration;

namespace Notification.IntegrationTests.Notifications;

public sealed class DeliveryWorkerOptionsTests
{
    [Fact]
    public void AcceptsDefaults()
    {
        Assert.True(new DeliveryWorkerOptionsValidator().Validate(null, new()).Succeeded);
    }

    [Theory]
    [InlineData(4, 600, 30000)]
    [InlineData(3601, 600, 30000)]
    [InlineData(300, 179, 30000)]
    [InlineData(300, 86401, 30000)]
    [InlineData(300, 180, 180000)]
    public void RejectsInvalidRecoveryOptions(int sweepSeconds, int stuckSeconds, int smtpTimeoutMs)
    {
        var options = new DeliveryWorkerOptions
        {
            SweepIntervalSeconds = sweepSeconds,
            StuckAfterSeconds = stuckSeconds,
            SmtpTimeoutMs = smtpTimeoutMs,
        };

        Assert.False(new DeliveryWorkerOptionsValidator().Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData(5, 180)]
    [InlineData(3600, 86400)]
    public void AcceptsRecoveryOptionBounds(int sweepSeconds, int stuckSeconds)
    {
        var options = new DeliveryWorkerOptions
        {
            SweepIntervalSeconds = sweepSeconds,
            StuckAfterSeconds = stuckSeconds,
            SmtpTimeoutMs = 120000,
        };

        Assert.True(new DeliveryWorkerOptionsValidator().Validate(null, options).Succeeded);
    }
}
