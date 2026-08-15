using Notification.Infrastructure.Configuration;

namespace Notification.IntegrationTests.Senders;

public sealed class SmtpOptionsTests
{
    [Theory]
    [InlineData(1000)]
    [InlineData(30000)]
    [InlineData(120000)]
    public void AcceptsBoundedTimeout(int value) => Assert.True(new SmtpOptionsValidator().Validate(null, new() { TimeoutMs = value }).Succeeded);

    [Theory]
    [InlineData(999)]
    [InlineData(120001)]
    public void RejectsTimeoutOutsideBounds(int value) => Assert.False(new SmtpOptionsValidator().Validate(null, new() { TimeoutMs = value }).Succeeded);
}
