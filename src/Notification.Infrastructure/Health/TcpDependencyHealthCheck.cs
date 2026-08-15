using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Notification.Infrastructure.Health;

internal sealed class TcpDependencyHealthCheck(
    Func<DependencyEndpoint> endpointFactory,
    TimeSpan timeout) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var endpoint = endpointFactory();
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(endpoint.Host, endpoint.Port, timeoutSource.Token);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Dependency is unavailable.");
        }
    }
}
