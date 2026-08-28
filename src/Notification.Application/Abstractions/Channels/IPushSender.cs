namespace Notification.Application.Abstractions.Channels;

public interface IPushSender
{
    Task<string?> SendAsync(string platform, string token, string title, string? body, IReadOnlyDictionary<string, string>? data, CancellationToken ct);
}
