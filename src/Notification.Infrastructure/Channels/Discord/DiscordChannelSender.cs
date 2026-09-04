using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Notification.Application.Abstractions.Channels;
using Notification.Application.Abstractions.Security;
using Notification.Application.Senders;

namespace Notification.Infrastructure.Channels.Discord;

public sealed class DiscordChannelSender(HttpClient httpClient, ISecretCipher cipher, ILogger<DiscordChannelSender> logger) : IDiscordSender
{
    public async Task<string?> SendAsync(ResolvedSender? sender, string target, string subject, string? textBody, string? htmlBody, CancellationToken ct)
    {
        var webhookUrl = ResolveWebhookUrl(sender, target);
        if (string.IsNullOrWhiteSpace(webhookUrl) || !Uri.TryCreate(webhookUrl, UriKind.Absolute, out var uri))
            throw new ChannelSendException("discord", "DISCORD_WEBHOOK_INVALID", false, "Invalid Discord webhook URL.");

        var contentBody = !string.IsNullOrWhiteSpace(textBody) ? textBody : htmlBody ?? "";

        var payload = new
        {
            content = $"**{subject}**\n\n{contentBody}".Trim(),
            embeds = new[]
            {
                new
                {
                    title = subject,
                    description = contentBody.Length > 2000 ? contentBody[..2000] : contentBody,
                    color = 0x5865F2 // Discord Blurple
                }
            }
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.TryAddWithoutValidation("User-Agent", "DiscordBot (https://github.com/huonghm14102005/notification, 1.0.0)");
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await httpClient.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                return $"discord_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            }

            var status = (int)response.StatusCode;
            var errContent = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Discord API error {StatusCode}: {Error}", response.StatusCode, errContent);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                if (uri.Host.Equals("discord.com", StringComparison.OrdinalIgnoreCase))
                {
                    var fallbackUri = new UriBuilder(uri) { Host = "canary.discord.com" }.Uri;
                    logger.LogInformation("discord.com returned rate limit; attempting automatic fallback to canary.discord.com");
                    try
                    {
                        using var fallbackReq = new HttpRequestMessage(HttpMethod.Post, fallbackUri)
                        {
                            Content = JsonContent.Create(payload)
                        };
                        fallbackReq.Headers.TryAddWithoutValidation("User-Agent", "DiscordBot (https://github.com/huonghm14102005/notification, 1.0.0)");
                        fallbackReq.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                        using var fallbackRes = await httpClient.SendAsync(fallbackReq, ct);
                        if (fallbackRes.IsSuccessStatusCode)
                        {
                            return $"discord_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogWarning(ex, "Fallback to canary.discord.com failed");
                    }
                }

                var retryAfter = response.Headers.RetryAfter?.Delta?.TotalSeconds.ToString() ?? "unknown";
                throw new ChannelSendException("discord", "DISCORD_RATE_LIMITED", true,
                    $"Discord rate limit reached (retry after: {retryAfter}s). {errContent}".Trim());
            }
            if (status >= 500)
                throw new ChannelSendException("discord", "DISCORD_SERVER_ERROR", true, $"Discord server error: {status}");
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new ChannelSendException("discord", "DISCORD_WEBHOOK_NOT_FOUND", false,
                    $"Discord webhook is invalid or deleted (HTTP {status}): {errContent}".Trim());

            throw new ChannelSendException("discord", $"DISCORD_HTTP_{status}", false, $"Discord API returned {status}: {errContent}".Trim());
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (ChannelSendException) { throw; }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Discord request failed with network error");
            throw new ChannelSendException("discord", "DISCORD_NETWORK_ERROR", true, "Network error connecting to Discord.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error delivering to Discord");
            throw new ChannelSendException("discord", "DISCORD_FAILED", false, ex.Message);
        }
    }

    private string ResolveWebhookUrl(ResolvedSender? sender, string target)
    {
        if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return target.Trim();
        }

        if (sender is not null && sender.PasswordEncrypted.Length > 0)
        {
            try
            {
                var decrypted = cipher.Decrypt(sender.PasswordEncrypted, sender.TenantId, sender.Id);
                if (decrypted.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return decrypted;
            }
            catch
            {
                var raw = System.Text.Encoding.UTF8.GetString(sender.PasswordEncrypted);
                if (raw.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return raw;
            }
        }

        if (sender is not null && !string.IsNullOrWhiteSpace(sender.Host))
        {
            return sender.Host.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? sender.Host
                : $"https://discord.com/api/webhooks/{sender.Username}/{target}";
        }

        return target.Trim();
    }
}
