using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Notification.Api.Authentication;
using Notification.Api.Contracts.Identity;
using Notification.Api.Endpoints.Identity;
using Notification.Api.Endpoints.Senders;
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
var jwtSecret = builder.Configuration["JWT_SECRET"] ?? string.Empty;
var jwtIssuer = builder.Configuration["JWT_ISSUER"] ?? "notification-server";
var jwtAudience = builder.Configuration["JWT_AUDIENCE"] ?? "notification-admin";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = true;
    options.TokenValidationParameters = new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSecret)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
    };
    options.Events = new()
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized", code = "UNAUTHORIZED", statusCode = 401 });
        },
    };
}).AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser().RequireRole("owner"));
    options.AddPolicy("ApiKey", policy => policy.AddAuthenticationSchemes(ApiKeyAuthenticationHandler.SchemeName).RequireAuthenticatedUser().RequireClaim("actor_type", "machine"));
});
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
    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new()
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
    options.AddPolicy("api-key-create", context =>
        RateLimitPartition.GetFixedWindowLimiter(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new()
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
    options.AddPolicy("sender-mutation", context => RateLimitPartition.GetFixedWindowLimiter(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "unknown", _ => new() { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("sender-test", context => RateLimitPartition.GetFixedWindowLimiter(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "unknown", _ => new() { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
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
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

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
app.MapAuthEndpoints();
app.MapApiKeyEndpoints();
app.MapSenderEndpoints();

app.Run();

public partial class Program;
