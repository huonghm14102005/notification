using Microsoft.Extensions.Options;
using Notification.Application.Abstractions.Observability;
using Notification.Infrastructure;
using Notification.Infrastructure.Configuration;
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
builder.Services.AddOptions<DeliveryWorkerOptions>().Configure(options =>
{
    options.PollIntervalMs = int.TryParse(builder.Configuration["DELIVERY_POLL_INTERVAL_MS"], out var poll) ? poll : 2000;
    options.Concurrency = int.TryParse(builder.Configuration["WORKER_CONCURRENCY"], out var concurrency) ? concurrency : 5;
}).ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<DeliveryWorkerOptions>, DeliveryWorkerOptionsValidator>();
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter(NotificationMetrics.MeterName)
        .AddRuntimeInstrumentation()
        .AddConsoleExporter());
builder.Services.AddHostedService<WorkerHealthPublisher>();
builder.Services.AddHostedService<NotificationDeliveryWorker>();

var host = builder.Build();
await host.RunAsync();
