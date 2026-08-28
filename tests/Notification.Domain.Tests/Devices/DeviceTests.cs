using Notification.Domain.Devices;

namespace Notification.Domain.Tests.Devices;

public sealed class DeviceTests
{
    [Fact]
    public void NewDeviceIsActiveAndCanBeRenamedAndDisabledOnce()
    {
        var created = new DateTimeOffset(2026, 8, 21, 0, 0, 0, TimeSpan.Zero);
        var device = new Device(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "DRL", DeviceRole.Source, created);

        Assert.Equal(DeviceStatus.Active, device.Status);
        Assert.Null(device.DisabledAt);

        var renamed = created.AddMinutes(1);
        device.Rename("DRL 01", renamed);
        Assert.Equal("DRL 01", device.Name);
        Assert.Equal(renamed, device.UpdatedAt);

        var disabled = created.AddMinutes(2);
        device.Disable(disabled);
        device.Disable(disabled.AddMinutes(1));
        Assert.Equal(DeviceStatus.Disabled, device.Status);
        Assert.Equal(disabled, device.DisabledAt);
        Assert.Equal(disabled, device.UpdatedAt);
    }

    [Fact]
    public void RecipientRoleIsAccepted()
    {
        var device = new Device(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Phone", DeviceRole.Recipient, DateTimeOffset.UtcNow);
        Assert.Equal(DeviceRole.Recipient, device.Role);
        Assert.Equal(DeviceStatus.Active, device.Status);
    }

    [Fact]
    public void InvalidRoleIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Device(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Phone", "invalid_role", DateTimeOffset.UtcNow));
    }
}
