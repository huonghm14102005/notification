using Notification.Application.Abstractions.Observability;
using Notification.Infrastructure;
using Notification.Worker;
using OpenTelemetry.Metrics;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    options.UseUtcTimestamp = true;
});
builder.Services.AddNotificationFoundation(builder.Configuration);
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter(NotificationMetrics.MeterName)
        .AddRuntimeInstrumentation()
        .AddConsoleExporter());
builder.Services.AddHostedService<WorkerHealthPublisher>();

var host = builder.Build();
await host.RunAsync();
