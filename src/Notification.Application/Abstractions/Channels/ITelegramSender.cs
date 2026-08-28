using Notification.Application.Senders;

namespace Notification.Application.Abstractions.Channels;

public interface ITelegramSender
{
    Task<string?> SendAsync(ResolvedSender? sender, string target, string subject, string? textBody, string? htmlBody, CancellationToken ct);
}
