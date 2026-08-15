using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Notification.Api.Contracts.Identity;
using Notification.Api.Endpoints.Identity;
using Notification.Api.Health;
using Notification.Api.Middleware;
using Notification.Application.Abstractions.Observability;
using Notification.Infrastructure;
using Notification.Infrastructure.Bootstrap;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    options.UseUtcTimestamp = true;
});

builder.Services.AddNotificationFoundation(builder.Configuration);
builder.Services.AddValidatorsFromAssemblyContaining<RegisterTenantValidator>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Too many registration attempts", code = "RATE_LIMITED", statusCode = 429 },
            cancellationToken);
    };
    options.AddPolicy("registration", context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new()
        {
            PermitLimit = 5,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
        }));
});
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter(NotificationMetrics.MeterName)
        .AddRuntimeInstrumentation()
        .AddConsoleExporter());

var app = builder.Build();
if (args.Contains("--migrate", StringComparer.Ordinal))
{
    var targetIndex = Array.IndexOf(args, "--migrate") + 1;
    var target = targetIndex < args.Length ? args[targetIndex] : "latest";
    await DatabaseMigrator.MigrateAsync(app.Services, target);
    return;
}
await TestAdminSeeder.SeedAsync(app.Services, app.Environment, app.Configuration);
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseRateLimiter();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthResponseWriter.WriteAsync,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status200OK,
    },
});

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.WriteAsync,
});
app.MapRegisterTenant();

app.Run();

public partial class Program;
