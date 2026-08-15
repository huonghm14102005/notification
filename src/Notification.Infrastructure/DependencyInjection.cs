using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Notification.Application.Abstractions.Observability;
using Notification.Application.Abstractions.Security;
using Notification.Application.Identity.Abstractions;
using Notification.Application.Identity.RegisterTenant;
using Notification.Infrastructure.Configuration;
using Notification.Infrastructure.Health;
using Notification.Infrastructure.Persistence;
using Notification.Infrastructure.Security;
using Npgsql;

namespace Notification.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationFoundation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<FoundationOptions>()
            .Configure(options =>
            {
                options.DatabaseUrl = configuration["DATABASE_URL"] ?? string.Empty;
                options.RedisUrl = configuration["REDIS_URL"] ?? "redis://localhost:6379";
                options.HealthCheckTimeoutSeconds = ReadInt(configuration, "HEALTH_CHECK_TIMEOUT_SECONDS", 3);
                options.WorkerHealthIntervalSeconds = ReadInt(configuration, "WORKER_HEALTH_INTERVAL_SECONDS", 10);
                options.WorkerHealthFile = configuration["WORKER_HEALTH_FILE"] ?? "/tmp/notification-worker-health";
            })
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<FoundationOptions>, FoundationOptionsValidator>();
        services.AddSingleton<NotificationMetrics>();
        services.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(ToConnectionString(configuration["DATABASE_URL"]!)));
        services.AddScoped<IIdentityRepository, IdentityRepository>();
        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddScoped<RegisterTenantHandler>();

        services.AddHealthChecks()
            .Add(new HealthCheckRegistration(
                "postgresql",
                provider => CreateHealthCheck(provider, static o => o.DatabaseUrl, 5432),
                HealthStatus.Unhealthy,
                ["ready"]))
            .Add(new HealthCheckRegistration(
                "redis",
                provider => CreateHealthCheck(provider, static o => o.RedisUrl, 6379),
                HealthStatus.Unhealthy,
                ["ready"]));

        return services;
    }

    private static TcpDependencyHealthCheck CreateHealthCheck(
        IServiceProvider provider,
        Func<FoundationOptions, string> urlSelector,
        int defaultPort)
    {
        var options = provider.GetRequiredService<IOptions<FoundationOptions>>().Value;
        return new TcpDependencyHealthCheck(
            () => DependencyEndpoint.FromUrl(urlSelector(options), defaultPort),
            TimeSpan.FromSeconds(options.HealthCheckTimeoutSeconds));
    }

    private static int ReadInt(IConfiguration configuration, string name, int defaultValue) =>
        int.TryParse(configuration[name], out var value) ? value : defaultValue;

    private static string ToConnectionString(string url)
    {
        var uri = new Uri(url);
        var credentials = uri.UserInfo.Split(':', 2);
        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : string.Empty,
        }.ConnectionString;
    }
}
