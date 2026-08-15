namespace Notification.Infrastructure.Health;

internal sealed record DependencyEndpoint(string Host, int Port)
{
    public static DependencyEndpoint FromUrl(string value, int defaultPort)
    {
        var uri = new Uri(value, UriKind.Absolute);
        return new DependencyEndpoint(uri.Host, uri.IsDefaultPort ? defaultPort : uri.Port);
    }
}
