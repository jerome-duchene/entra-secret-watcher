using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Extensions;
using EntraSecretWatcher.Jobs;
using EntraSecretWatcher.Services;
using Hangfire;
using Prometheus;

// Our meter is published on /metrics by ScanMetrics itself (the adapter would prefix the names)
Metrics.ConfigureMeterAdapter(options =>
    options.InstrumentFilterPredicate = instrument => instrument.Meter.Name != ScanMetrics.MeterName);

var builder = WebApplication.CreateSlimBuilder(args);

// Map environment variables to configuration sections
builder.Configuration.AddEnvironmentVariables(prefix: "ESW_");

// Register services
builder.Services.AddEntraSecretWatcher();
builder.Services.AddHangfireServices();
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

// Hangfire dashboard (read-only, no auth — container is expected to run in a private network)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    IsReadOnlyFunc = _ => true,
    DashboardTitle = "Entra Secret Watcher",
    Authorization = []
});

// Register recurring job
var watcherOptions = app.Configuration.GetSection(WatcherOptions.SectionName).Get<WatcherOptions>()
    ?? new WatcherOptions();

RecurringJob.AddOrUpdate<CredentialScanJob>(
    CredentialScanJob.JobId,
    job => job.ExecuteAsync(),
    watcherOptions.CronSchedule);

app.Run();
