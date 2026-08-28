using Notification.Domain.Devices;

namespace Notification.Domain.Tests.Devices;

public sealed class DevicePushEndpointTests
{
    [Fact]
    public void ValidCreationSucceedsWithActiveStatus()
    {
        var now = DateTimeOffset.UtcNow;
        var endpoint = new DevicePushEndpoint(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "fcm", [1, 2, 3], now);

        Assert.Equal("fcm", endpoint.Platform);
        Assert.Equal(PushEndpointStatus.Active, endpoint.Status);
        Assert.Equal(now, endpoint.CreatedAt);
        Assert.Null(endpoint.DisabledAt);
    }

    [Theory]
    [InlineData("fcm")]
    [InlineData("apns")]
    public void SupportedPlatformsAreAccepted(string platform)
    {
        var endpoint = new DevicePushEndpoint(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), platform, [1, 2, 3], DateTimeOffset.UtcNow);
        Assert.Equal(platform, endpoint.Platform);
    }

    [Fact]
    public void UnsupportedPlatformThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DevicePushEndpoint(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "sms", [1, 2, 3], DateTimeOffset.UtcNow));
    }

    [Fact]
    public void EmptyTokenThrowsException()
    {
        Assert.Throws<ArgumentException>(() =>
            new DevicePushEndpoint(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "fcm", [], DateTimeOffset.UtcNow));
    }

    [Fact]
    public void UpdateTokenRefreshesPlatformAndStatus()
    {
        var now = DateTimeOffset.UtcNow;
        var endpoint = new DevicePushEndpoint(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "fcm", [1, 2, 3], now);
        endpoint.Disable(now);
        Assert.Equal(PushEndpointStatus.Disabled, endpoint.Status);

        var later = now.AddMinutes(5);
        endpoint.UpdateToken("apns", [4, 5, 6], later);

        Assert.Equal("apns", endpoint.Platform);
        Assert.Equal(PushEndpointStatus.Active, endpoint.Status);
        Assert.Null(endpoint.DisabledAt);
        Assert.Equal(later, endpoint.UpdatedAt);
    }

    [Fact]
    public void DisableMarksStatusDisabled()
    {
        var now = DateTimeOffset.UtcNow;
        var endpoint = new DevicePushEndpoint(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "fcm", [1, 2, 3], now);

        endpoint.Disable(now.AddMinutes(1));

        Assert.Equal(PushEndpointStatus.Disabled, endpoint.Status);
        Assert.NotNull(endpoint.DisabledAt);
    }
}
