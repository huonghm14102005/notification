using Notification.Infrastructure.Configuration;

namespace Notification.IntegrationTests.Notifications;

public sealed class AlertOptionsTests
{
    [Fact] public void DefaultsAreValid() => Assert.True(new AlertOptionsValidator().Validate(null, new()).Succeeded);
    [Fact] public void RejectsUnsafeBounds() => Assert.False(new AlertOptionsValidator().Validate(null, new() { WindowSeconds = 59 }).Succeeded);
}
