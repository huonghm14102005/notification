using System.Net;
using Microsoft.Extensions.Options;
using Notification.Infrastructure.Callbacks;
using Notification.Infrastructure.Configuration;

namespace Notification.IntegrationTests.Callbacks;

public sealed class CallbackSecurityTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.1.1")]
    [InlineData("::1")]
    [InlineData("fc00::1")]
    [InlineData("fe80::1")]
    public void BlocksNonPublicAddresses(string value) => Assert.True(CallbackTargetValidator.IsBlocked(IPAddress.Parse(value)));

    [Fact]
    public void CallbackOptionsRejectInsecureHttpInProduction()
    {
        var result = new CallbackOptionsValidator().Validate(null, new() { AllowInsecureHttp = true, EnvironmentName = "Production" });
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void CallbackOptionsRejectPrivateNetworkInProduction()
    {
        var result = new CallbackOptionsValidator().Validate(null, new() { AllowPrivateNetwork = true, EnvironmentName = "Production" });
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void SecretHasThirtyTwoBytesOfEntropy()
    {
        var raw = new CallbackSecretGenerator().Generate();
        var padded = raw.Replace('-', '+').Replace('_', '/').PadRight(44, '=');
        Assert.Equal(32, Convert.FromBase64String(padded).Length);
    }
}
