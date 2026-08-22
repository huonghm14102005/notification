using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Notification.Application.Abstractions.Callbacks;
using Notification.Application.Abstractions.Email;
using Notification.Application.Abstractions.Observability;
using Notification.Application.Abstractions.Security;
using Notification.Application.Abstractions.Time;
using Notification.Application.Callbacks;
using Notification.Application.Devices;
using Notification.Application.Identity.Abstractions;
using Notification.Application.Identity.ApiKeys;
using Notification.Application.Identity.Auth;
using Notification.Application.Identity.RegisterTenant;
using Notification.Application.Notifications;
using Notification.Application.Notifications.Delivery;
using Notification.Application.Senders;
using Notification.Application.Templates;
using Notification.Infrastructure.Callbacks;
using Notification.Infrastructure.Configuration;
using Notification.Infrastructure.Email;
using Notification.Infrastructure.Health;
using Notification.Infrastructure.Persistence;
using Notification.Infrastructure.Security;
using Notification.Infrastructure.Time;
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
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<DeviceHandlers>();
        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddScoped<RegisterTenantHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshSessionHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<ApiKeyHandlers>();
        services.AddScoped<SenderHandlers>();
        services.AddScoped<ISenderRepository, SenderRepository>();
        services.AddScoped<ISenderResolver, SenderResolver>();
        services.AddScoped<SendTestEmailHandler>();
        services.AddScoped<IEmailSender, MailKitEmailSender>();
        services.AddScoped<TemplateHandlers>(); services.AddScoped<ITemplateRepository, TemplateRepository>(); services.AddSingleton<ITemplateRenderer, TemplateRenderer>();
        services.AddScoped<AcceptNotificationHandler>(); services.AddScoped<GetNotificationHandler>(); services.AddScoped<ListNotificationsHandler>(); services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<DeliverNotificationHandler>(); services.AddScoped<IDeliveryRepository, DeliveryRepository>();
        services.AddScoped<DeliverCallbackHandler>(); services.AddScoped<ICallbackRepository, CallbackRepository>();
        services.AddScoped<ICallbackSender, CallbackSender>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IRefreshTokenGenerator, SecureRefreshTokenGenerator>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton<IApiKeySecretService, ApiKeySecretService>();
        services.AddSingleton<ISecretCipher, AesGcmSecretCipher>();
        services.AddSingleton<ICallbackSecretGenerator, CallbackSecretGenerator>();
        services.AddSingleton<ICallbackTargetValidator, CallbackTargetValidator>();
        services.AddSingleton(provider => new AuthLifetime(provider.GetRequiredService<IOptions<AuthOptions>>().Value.RefreshExpiresIn));
        services.AddOptions<AuthOptions>().Configure(options =>
        {
            options.Secret = configuration["JWT_SECRET"] ?? string.Empty;
            options.Issuer = configuration["JWT_ISSUER"] ?? "notification-server";
            options.Audience = configuration["JWT_AUDIENCE"] ?? "notification-admin";
            options.AccessExpiresIn = ReadInt(configuration, "JWT_EXPIRES_IN", 3600);
            options.RefreshExpiresIn = ReadInt(configuration, "JWT_REFRESH_EXPIRES_IN", 604800);
        }).ValidateOnStart();
        services.AddSingleton<IValidateOptions<AuthOptions>, AuthOptionsValidator>();
        services.AddOptions<ApiKeyOptions>().Configure(options => options.Salt = configuration["API_KEY_SALT"] ?? string.Empty).ValidateOnStart();
        services.AddSingleton<IValidateOptions<ApiKeyOptions>, ApiKeyOptionsValidator>();
        services.AddOptions<EncryptionOptions>().Configure(options => options.Key = configuration["ENCRYPTION_KEY"] ?? string.Empty).ValidateOnStart();
        services.AddSingleton<IValidateOptions<EncryptionOptions>, EncryptionOptionsValidator>();
        services.AddOptions<SmtpOptions>().Configure(options => options.TimeoutMs = ReadInt(configuration, "SMTP_TIMEOUT_MS", 30000)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<SmtpOptions>, SmtpOptionsValidator>();
        services.AddOptions<CallbackOptions>().Configure(options =>
        {
            options.TimeoutMs = ReadInt(configuration, "CALLBACK_TIMEOUT_MS", 10000);
            options.PollIntervalMs = ReadInt(configuration, "CALLBACK_POLL_INTERVAL_MS", 2000);
            options.Concurrency = ReadInt(configuration, "CALLBACK_CONCURRENCY", 5);
            options.StuckAfterSeconds = ReadInt(configuration, "CALLBACK_STUCK_AFTER_SECONDS", 120);
            options.AllowInsecureHttp = bool.TryParse(configuration["CALLBACK_ALLOW_INSECURE_HTTP"], out var allow) && allow;
            options.AllowPrivateNetwork = bool.TryParse(configuration["CALLBACK_ALLOW_PRIVATE_NETWORK"], out var allowPrivate) && allowPrivate;
            options.EnvironmentName = configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"] ?? "Production";
        }).ValidateOnStart();
        services.AddSingleton<IValidateOptions<CallbackOptions>, CallbackOptionsValidator>();

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
