using Notification.Domain.Notifications;

namespace Notification.Domain.Tests.Notifications;

public sealed class DeliveryAggregateTests
{
    [Theory]
    [InlineData(NotificationStatus.Accepted, DeliveryStatus.Pending, DeliveryStatus.Failed)]
    [InlineData(NotificationStatus.Processing, DeliveryStatus.Sending, DeliveryStatus.Pending)]
    [InlineData(NotificationStatus.Delivered, DeliveryStatus.Delivered, DeliveryStatus.Delivered)]
    [InlineData(NotificationStatus.PartiallyDelivered, DeliveryStatus.Delivered, DeliveryStatus.Failed)]
    [InlineData(NotificationStatus.Failed, DeliveryStatus.Failed, DeliveryStatus.Failed)]
    [InlineData(NotificationStatus.Cancelled, DeliveryStatus.Cancelled, DeliveryStatus.Cancelled)]
    public void CalculatesNotificationStatus(string expected, params string[] deliveries) =>
        Assert.Equal(expected, DeliveryAggregate.Calculate(deliveries));

    [Fact]
    public void RejectsNotificationWithoutDelivery() =>
        Assert.Throws<InvalidOperationException>(() => DeliveryAggregate.Calculate([]));
}
