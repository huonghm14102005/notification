using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Notification.Application.Abstractions.Channels;
using Notification.Domain.Devices;

namespace Notification.Infrastructure.Channels.Push;

public sealed class PushChannelSender(HttpClient httpClient, ILogger<PushChannelSender> logger) : IPushSender
{
    public async Task<string?> SendAsync(string platform, string token, string title, string? body, IReadOnlyDictionary<string, string>? data, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ChannelSendException("push", "PUSH_TOKEN_INVALID", isTransient: false);

        if (platform is not PushPlatform.Fcm and not PushPlatform.Apns)
            throw new ChannelSendException("push", "PUSH_PLATFORM_NOT_SUPPORTED", isTransient: false);

        try
        {
            var payload = new
            {
                platform,
                token,
                notification = new
                {
                    title,
                    body = body ?? string.Empty
                },
                data = data ?? new Dictionary<string, string>()
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // For default/fallback endpoint or provider URL
            var url = platform == PushPlatform.Fcm
                ? "https://fcm.googleapis.com/v1/projects/citad-notification/messages:send"
                : "https://api.push.apple.com/3/device/" + token;

            var response = await httpClient.PostAsync(url, content, ct);

            if (response.IsSuccessStatusCode)
            {
                var bodyText = await response.Content.ReadAsStringAsync(ct);
                try
                {
                    using var doc = JsonDocument.Parse(bodyText);
                    if (doc.RootElement.TryGetProperty("name", out var nameProp))
                        return nameProp.GetString();
                }
                catch { }
                return $"{platform}_{Guid.NewGuid():N}";
            }

            var statusCode = (int)response.StatusCode;
            logger.LogWarning("Push notification provider failed with status {StatusCode} for platform {Platform}", statusCode, platform);

            if (statusCode == 429)
                throw new ChannelSendException("push", "PUSH_RATE_LIMITED", isTransient: true);

            if (statusCode >= 500)
                throw new ChannelSendException("push", "PUSH_SERVER_UNAVAILABLE", isTransient: true);

            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.Gone)
                throw new ChannelSendException("push", "PUSH_TOKEN_INVALID", isTransient: false);

            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                throw new ChannelSendException("push", "PUSH_AUTH_FAILED", isTransient: false);

            throw new ChannelSendException("push", "PUSH_DELIVERY_FAILED", isTransient: false);
        }
        catch (ChannelSendException) { throw; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "HTTP network error when sending push notification to {Platform}", platform);
            throw new ChannelSendException("push", "PUSH_NETWORK_ERROR", isTransient: true);
        }
    }
}
