using Cronos;
using EntraSecretWatcher.Configuration;
using Microsoft.Extensions.Options;

namespace EntraSecretWatcher.Jobs;

/// <summary>
/// Runs <see cref="CredentialScanJob"/> on the <c>Watcher:CronSchedule</c> (UTC), and on demand via <see cref="Trigger"/>.
/// Scans never overlap.
/// </summary>
internal class ScanScheduler(IServiceScopeFactory scopes, IOptions<WatcherOptions> options) : BackgroundService
{
    private readonly CronExpression _cron = CronExpression.Parse(options.Value.CronSchedule);
    private readonly SemaphoreSlim _lock = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (options.Value.RunOnStartup)
            await RunAsync(ct);

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
        await _lock.WaitAsync(ct);
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<CredentialScanJob>().ExecuteAsync();
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Already logged by the job; the next run is the retry
        }
        finally
        {
            _lock.Release();
        }
    }
}
