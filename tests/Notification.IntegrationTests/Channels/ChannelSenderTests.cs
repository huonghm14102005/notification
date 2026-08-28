using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Notification.Application.Abstractions.Channels;
using Notification.Application.Abstractions.Security;
using Notification.Application.Senders;
using Notification.Infrastructure.Channels.Discord;
using Notification.Infrastructure.Channels.Telegram;

namespace Notification.IntegrationTests.Channels;

public sealed class ChannelSenderTests
{
    [Fact]
    public async Task TelegramSuccessReturnsMessageId()
    {
        var mockHttp = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"ok\":true,\"result\":{\"message_id\":9876}}");
        var client = new HttpClient(mockHttp);
        var sender = new TelegramChannelSender(client, new Cipher(), NullLogger<TelegramChannelSender>.Instance);

        var result = await sender.SendAsync(null, "bot_token_123:chat_456", "Test Subject", "Test Body", null, CancellationToken.None);

        Assert.Equal("9876", result);
        Assert.Equal("https://api.telegram.org/botbot_token_123/sendMessage", mockHttp.LastRequestUri?.ToString());
    }

    [Fact]
    public async Task TelegramRateLimitThrowsTransientException()
    {
        var mockHttp = new MockHttpMessageHandler(HttpStatusCode.TooManyRequests, "{\"ok\":false,\"description\":\"Too Many Requests\"}");
        var client = new HttpClient(mockHttp);
        var sender = new TelegramChannelSender(client, new Cipher(), NullLogger<TelegramChannelSender>.Instance);

        var ex = await Assert.ThrowsAsync<ChannelSendException>(() =>
            sender.SendAsync(null, "bot_token_123:chat_456", "Test Subject", "Test Body", null, CancellationToken.None));

        Assert.Equal("telegram", ex.Channel);
        Assert.Equal("TELEGRAM_RATE_LIMITED", ex.Code);
        Assert.True(ex.IsTransient);
    }

    [Fact]
    public async Task TelegramUnauthorizedThrowsPermanentException()
    {
        var mockHttp = new MockHttpMessageHandler(HttpStatusCode.Unauthorized, "{\"ok\":false,\"description\":\"Unauthorized\"}");
        var client = new HttpClient(mockHttp);
        var sender = new TelegramChannelSender(client, new Cipher(), NullLogger<TelegramChannelSender>.Instance);

        var ex = await Assert.ThrowsAsync<ChannelSendException>(() =>
            sender.SendAsync(null, "bad_token:chat_456", "Test Subject", "Test Body", null, CancellationToken.None));

        Assert.Equal("telegram", ex.Channel);
        Assert.Equal("TELEGRAM_UNAUTHORIZED", ex.Code);
        Assert.False(ex.IsTransient);
    }

    [Fact]
    public async Task DiscordSuccessReturnsMessageId()
    {
        var mockHttp = new MockHttpMessageHandler(HttpStatusCode.NoContent, "");
        var client = new HttpClient(mockHttp);
        var sender = new DiscordChannelSender(client, new Cipher(), NullLogger<DiscordChannelSender>.Instance);

        var webhookUrl = "https://discord.com/api/webhooks/123/xyz";
        var result = await sender.SendAsync(null, webhookUrl, "Discord Title", "Hello Discord", null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.StartsWith("discord_", result);
        Assert.Equal(webhookUrl, mockHttp.LastRequestUri?.ToString());
    }

    [Fact]
    public async Task DiscordRateLimitThrowsTransientException()
    {
        var mockHttp = new MockHttpMessageHandler(HttpStatusCode.TooManyRequests, "{\"message\":\"You are being rate limited.\"}");
        var client = new HttpClient(mockHttp);
        var sender = new DiscordChannelSender(client, new Cipher(), NullLogger<DiscordChannelSender>.Instance);

        var ex = await Assert.ThrowsAsync<ChannelSendException>(() =>
            sender.SendAsync(null, "https://discord.com/api/webhooks/123/xyz", "Discord Title", "Hello", null, CancellationToken.None));

        Assert.Equal("discord", ex.Channel);
        Assert.Equal("DISCORD_RATE_LIMITED", ex.Code);
        Assert.True(ex.IsTransient);
    }

    [Fact]
    public async Task DiscordNotFoundThrowsPermanentException()
    {
        var mockHttp = new MockHttpMessageHandler(HttpStatusCode.NotFound, "{\"message\":\"Unknown Webhook\"}");
        var client = new HttpClient(mockHttp);
        var sender = new DiscordChannelSender(client, new Cipher(), NullLogger<DiscordChannelSender>.Instance);

        var ex = await Assert.ThrowsAsync<ChannelSendException>(() =>
            sender.SendAsync(null, "https://discord.com/api/webhooks/123/deleted", "Discord Title", "Hello", null, CancellationToken.None));

        Assert.Equal("discord", ex.Channel);
        Assert.Equal("DISCORD_WEBHOOK_NOT_FOUND", ex.Code);
        Assert.False(ex.IsTransient);
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

    private sealed class Cipher : ISecretCipher
    {
        public byte[] Encrypt(string plaintext, Guid tenantId, Guid recordId) => System.Text.Encoding.UTF8.GetBytes(plaintext);
        public string Decrypt(byte[] envelope, Guid tenantId, Guid recordId) => System.Text.Encoding.UTF8.GetString(envelope);
    }
}
