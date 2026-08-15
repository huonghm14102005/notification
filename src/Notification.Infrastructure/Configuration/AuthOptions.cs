namespace Notification.Infrastructure.Configuration;

public sealed class AuthOptions
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "notification-server";
    public string Audience { get; set; } = "notification-admin";
    public int AccessExpiresIn { get; set; } = 3600;
    public int RefreshExpiresIn { get; set; } = 604800;
}
