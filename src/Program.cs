using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Endpoints;
using EntraSecretWatcher.Extensions;
using EntraSecretWatcher.Services;
using Prometheus;

// Our meter is published on /metrics by ScanMetrics itself (the adapter would prefix the names)
Metrics.ConfigureMeterAdapter(options =>
    options.InstrumentFilterPredicate = instrument => instrument.Meter.Name != ScanMetrics.MeterName);

var builder = WebApplication.CreateSlimBuilder(args);

// Map environment variables to configuration sections
builder.Configuration.AddEnvironmentVariables(prefix: "ESW_");

// Register services
builder.Services.AddEntraSecretWatcher();
builder.Services.AddObservability(builder.Configuration);

var app = builder.Build();

// Create the metrics now so that they are registered before the first scan
app.Services.GetRequiredService<ScanMetrics>();

// Health check endpoint
app.MapGet("/health", (IConfiguration config) =>
{
    var entra = config.GetSection(EntraOptions.SectionName).Get<EntraOptions>();
    return Results.Ok(new
    {
        status = "healthy",
        tenant = entra?.TenantName ?? "Unknown",
        timestamp = DateTimeOffset.UtcNow
    });
});

// Prometheus metrics endpoint
app.MapMetrics();

var watcherOptions = app.Configuration.GetSection(WatcherOptions.SectionName).Get<WatcherOptions>()
    ?? new WatcherOptions();

// On-demand scan: POST /scan (disabled unless Watcher:ManualTriggerSecret is set)
app.MapManualScan(watcherOptions);

app.Run();
