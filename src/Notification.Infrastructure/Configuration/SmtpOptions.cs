namespace Notification.Infrastructure.Configuration;

public sealed class SmtpOptions
{
    public int TimeoutMs { get; set; } = 30000;
}
