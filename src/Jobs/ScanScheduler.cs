using Cronos;
using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Services;
using Microsoft.Extensions.Options;

namespace EntraSecretWatcher.Jobs;

/// <summary>
/// Runs <see cref="CredentialScanJob"/> on the <c>Watcher:CronSchedule</c> (UTC), and on demand via <see cref="Trigger"/>.
/// Scans never overlap.
/// </summary>
internal class ScanScheduler(
    IServiceScopeFactory scopes, IOptions<WatcherOptions> options, ILogger<ScanScheduler> logger) : BackgroundService
{
    /// <summary>Backoff for the startup scan, whose first attempt often precedes the network being ready.</summary>
    internal RetryPolicy StartupBackoff { get; init; } = RetryPolicy.Startup;

    private readonly CronExpression _cron = CronExpression.Parse(options.Value.CronSchedule);
    private readonly SemaphoreSlim _lock = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (options.Value.RunOnStartup)
        {
            // The network (DNS) is often not ready right after a container start: retry with a backoff instead of
            // waiting for the next cron occurrence. The lock is only held during each attempt.
            try
            {
                await Retry.ExecuteAsync("Startup scan", StartupBackoff, _ => true, async () =>
                {
                    await RunLockedAsync(ct);
                    return true;
                }, logger, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning("Startup scan abandoned after {Retries} retries: {Error}", StartupBackoff.Retries, ex.Message);
            }
        }

        while (_cron.GetNextOccurrence(DateTime.UtcNow) is { } next)
        {
            // Task.Delay is capped at ~24 days, so wait in chunks
            while (next - DateTime.UtcNow is { Ticks: > 0 } wait)
                await Task.Delay(wait < TimeSpan.FromDays(1) ? wait : TimeSpan.FromDays(1), ct);

            await RunAsync(ct);
        }
    }

    public void Trigger() => _ = RunAsync(CancellationToken.None);

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            await RunLockedAsync(ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Already logged by the job; the next run is the retry
        }
    }

    private async Task RunLockedAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<CredentialScanJob>().ExecuteAsync();
        }
        finally
        {
            _lock.Release();
        }
    }
}
