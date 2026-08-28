using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Notification.Application.Abstractions.Channels;
using Notification.Infrastructure.Channels.Push;

namespace Notification.IntegrationTests.Channels;

public sealed class PushChannelSenderTests
{
    [Fact]
    public async Task FcmSuccessReturnsMessageId()
    {
        var mockHttp = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"name\":\"projects/citad/messages/msg_123\"}");
        var client = new HttpClient(mockHttp);
        var sender = new PushChannelSender(client, NullLogger<PushChannelSender>.Instance);

        var result = await sender.SendAsync("fcm", "fcm_token_123", "Alert Title", "Alert Body", null, CancellationToken.None);

        Assert.Equal("projects/citad/messages/msg_123", result);
        Assert.Equal("https://fcm.googleapis.com/v1/projects/citad-notification/messages:send", mockHttp.LastRequestUri?.ToString());
    }

    [Fact]
    public async Task ApnsSuccessReturnsMessageId()
    {
        var mockHttp = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"apns-id\":\"apns_uuid_999\"}");
        var client = new HttpClient(mockHttp);
        var sender = new PushChannelSender(client, NullLogger<PushChannelSender>.Instance);

        var result = await sender.SendAsync("apns", "apns_device_token_xyz", "APNs Title", "APNs Body", null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.StartsWith("apns_", result);
        Assert.Contains("apns_device_token_xyz", mockHttp.LastRequestUri?.ToString()!);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task InvalidTokenThrowsPermanentException(HttpStatusCode statusCode)
    {
        var mockHttp = new MockHttpMessageHandler(statusCode, "{\"error\":{\"code\":\"UNREGISTERED\"}}");
        var client = new HttpClient(mockHttp);
        var sender = new PushChannelSender(client, NullLogger<PushChannelSender>.Instance);

        var ex = await Assert.ThrowsAsync<ChannelSendException>(() =>
            sender.SendAsync("fcm", "bad_token", "Title", "Body", null, CancellationToken.None));

        Assert.Equal("push", ex.Channel);
        Assert.Equal("PUSH_TOKEN_INVALID", ex.Code);
        Assert.False(ex.IsTransient);
    }

    [Fact]
    public async Task RateLimitThrowsTransientException()
    {
        var mockHttp = new MockHttpMessageHandler(HttpStatusCode.TooManyRequests, "{\"error\":{\"code\":\"RESOURCE_EXHAUSTED\"}}");
        var client = new HttpClient(mockHttp);
        var sender = new PushChannelSender(client, NullLogger<PushChannelSender>.Instance);

        var ex = await Assert.ThrowsAsync<ChannelSendException>(() =>
            sender.SendAsync("fcm", "valid_token", "Title", "Body", null, CancellationToken.None));

        Assert.Equal("push", ex.Channel);
        Assert.Equal("PUSH_RATE_LIMITED", ex.Code);
        Assert.True(ex.IsTransient);
    }

    private sealed class MockHttpMessageHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
