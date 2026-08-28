using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Notification.Application.Abstractions.Channels;
using Notification.Application.Abstractions.Security;
using Notification.Application.Senders;

namespace Notification.Infrastructure.Channels.Telegram;

public sealed class TelegramChannelSender(HttpClient httpClient, ISecretCipher cipher, ILogger<TelegramChannelSender> logger) : ITelegramSender
{
    public async Task<string?> SendAsync(ResolvedSender? sender, string target, string subject, string? textBody, string? htmlBody, CancellationToken ct)
    {
        var (botToken, chatId) = ResolveCredentials(sender, target);
        if (string.IsNullOrWhiteSpace(botToken))
            throw new ChannelSendException("telegram", "TELEGRAM_TOKEN_MISSING", false, "Telegram bot token is missing.");
        if (string.IsNullOrWhiteSpace(chatId))
            throw new ChannelSendException("telegram", "TELEGRAM_CHAT_ID_MISSING", false, "Telegram chat ID is missing.");

        var messageText = FormatMessage(subject, textBody, htmlBody);
        var url = $"https://api.telegram.org/bot{botToken}/sendMessage";
        var payload = new
        {
            chat_id = chatId,
            text = messageText,
            parse_mode = "HTML"
        };

        try
        {
            using var response = await httpClient.PostAsJsonAsync(url, payload, ct);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                if (content.TryGetProperty("result", out var result) && result.TryGetProperty("message_id", out var msgId))
                {
                    return msgId.ToString();
                }
                return "telegram_ok";
            }

            var status = (int)response.StatusCode;
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new ChannelSendException("telegram", "TELEGRAM_RATE_LIMITED", true, "Telegram rate limit reached.");
            if (status >= 500)
                throw new ChannelSendException("telegram", "TELEGRAM_SERVER_ERROR", true, $"Telegram server error: {status}");
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new ChannelSendException("telegram", "TELEGRAM_UNAUTHORIZED", false, "Telegram bot token is invalid or unauthorized.");
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new ChannelSendException("telegram", "TELEGRAM_NOT_FOUND", false, "Telegram endpoint or chat not found.");

            var errContent = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Telegram API error {StatusCode}: {Error}", response.StatusCode, errContent);
            throw new ChannelSendException("telegram", $"TELEGRAM_HTTP_{status}", false, $"Telegram API returned {status}.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (ChannelSendException) { throw; }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Telegram request failed with network error");
            throw new ChannelSendException("telegram", "TELEGRAM_NETWORK_ERROR", true, "Network error connecting to Telegram.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error delivering to Telegram");
            throw new ChannelSendException("telegram", "TELEGRAM_FAILED", false, ex.Message);
        }
    }

    private (string botToken, string chatId) ResolveCredentials(ResolvedSender? sender, string target)
    {
        string botToken = string.Empty;
        string chatId = target.Trim();

        // 1. Combined target format "bot_token:chat_id" (where bot_token has an internal colon)
        if (target.Contains(':'))
        {
            var lastColon = target.LastIndexOf(':');
            if (lastColon > 0 && lastColon < target.Length - 1)
            {
                botToken = target[..lastColon].Trim();
                chatId = target[(lastColon + 1)..].Trim();
                return (botToken, chatId);
            }
        }

        // 2. Resolve bot token from a telegram sender
        if (sender is not null && string.Equals(sender.Channel, "telegram", StringComparison.OrdinalIgnoreCase) && sender.PasswordEncrypted.Length > 0)
        {
            try
            {
                botToken = cipher.Decrypt(sender.PasswordEncrypted, sender.TenantId, sender.Id);
            }
            catch
            {
                botToken = System.Text.Encoding.UTF8.GetString(sender.PasswordEncrypted);
            }
        }

        return (botToken, chatId);
    }

    private static string FormatMessage(string subject, string? textBody, string? htmlBody)
    {
        var safeSubject = WebUtility.HtmlEncode(subject);
        var body = !string.IsNullOrWhiteSpace(textBody) ? WebUtility.HtmlEncode(textBody) : htmlBody ?? "";
        return $"<b>{safeSubject}</b>\n\n{body}".Trim();
    }
}
