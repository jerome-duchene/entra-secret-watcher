using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Extensions;
using EntraSecretWatcher.Jobs;
using Hangfire;

var builder = WebApplication.CreateSlimBuilder(args);

// Map environment variables to configuration sections
builder.Configuration.AddEnvironmentVariables(prefix: "ESW_");

// Register services
builder.Services.AddEntraSecretWatcher();
builder.Services.AddHangfireServices();

var app = builder.Build();

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
