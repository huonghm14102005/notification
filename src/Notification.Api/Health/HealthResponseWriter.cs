using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Notification.Api.Health;

public static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        context.Response.Headers.CacheControl = "no-store";
        var checks = report.Entries.ToDictionary(
            static entry => entry.Key,
            static entry => Status(entry.Value.Status),
            StringComparer.Ordinal);
        var response = new HealthResponse(
            Status(report.Status),
            "notification-api",
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0",
            checks.Count == 0 ? null : checks);
        return context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    private static string Status(HealthStatus status) =>
        status == HealthStatus.Unhealthy ? "unhealthy" : "healthy";

    private sealed record HealthResponse(
        string Status,
        string Service,
        string Version,
        IReadOnlyDictionary<string, string>? Checks);
}
