namespace Notification.Application.Abstractions.Channels;

public sealed class ChannelSendException(string channel, string code, bool isTransient, string? message = null)
    : Exception(message ?? $"{channel} delivery failed: {code}")
{
    public string Channel { get; } = channel;
    public string Code { get; } = code;
    public bool IsTransient { get; } = isTransient;
}
