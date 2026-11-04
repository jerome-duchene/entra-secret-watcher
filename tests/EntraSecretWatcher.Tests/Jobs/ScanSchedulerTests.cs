using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Jobs;
using EntraSecretWatcher.Models;
using EntraSecretWatcher.Services;
using EntraSecretWatcher.Services.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EntraSecretWatcher.Tests.Jobs;

public class ScanSchedulerTests
{
    private static readonly RetryPolicy Fast = new(3, TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(4));

    private static readonly ScanResult Empty = new()
    {
        TenantName = "Test",
        TenantId = "test-id",
        ScannedAt = DateTimeOffset.UtcNow,
        Credentials = new List<ExpiringCredential>().AsReadOnly(),
        TotalApplicationsScanned = 0
    };

    private static (ScanScheduler scheduler, GraphCredentialScanner scanner) Create(Func<int, bool> fails)
    {
        var entra = Options.Create(new EntraOptions { TenantId = "t", ClientId = "c", ClientSecret = "s" });
        var watcher = Options.Create(new WatcherOptions { RunOnStartup = true, CronSchedule = "0 0 1 1 *" });

        var calls = 0;
        var scanner = Substitute.For<GraphCredentialScanner>(
            new GraphClient(new HttpClient(), entra), entra, watcher, Substitute.For<ILogger<GraphCredentialScanner>>());
        scanner.ScanAsync(Arg.Any<CancellationToken>()).Returns(_ =>
            fails(++calls) ? Task.FromException<ScanResult>(new HttpRequestException("dns")) : Task.FromResult(Empty));

        var job = new CredentialScanJob(
            scanner,
            Substitute.For<NotificationDispatcher>(
                Array.Empty<EntraSecretWatcher.Services.Notifications.Channels.INotificationChannel>(),
                Options.Create(new WatcherOptions()),
                Substitute.For<ILogger<NotificationDispatcher>>()),
            new ScanMetrics($"test-{Guid.NewGuid()}", new Prometheus.CollectorRegistry(), "Test", "test-id"),
            watcher,
            Substitute.For<ILogger<CredentialScanJob>>());

        var services = new ServiceCollection().AddScoped(_ => job).BuildServiceProvider();
        var scheduler = new ScanScheduler(
            services.GetRequiredService<IServiceScopeFactory>(), watcher, Substitute.For<ILogger<ScanScheduler>>())
        { StartupBackoff = Fast };
        return (scheduler, scanner);
    }

    private static async Task RunBrieflyAsync(ScanScheduler scheduler)
    {
        await scheduler.StartAsync(CancellationToken.None);
        await Task.Delay(300);
        await scheduler.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Failed_startup_scan_is_retried_until_it_succeeds()
    {
        var (scheduler, scanner) = Create(call => call <= 2);

        await RunBrieflyAsync(scheduler);

        await scanner.Received(3).ScanAsync(Arg.Any<CancellationToken>()); // 2 failures + 1 success
    }

    [Fact]
    public async Task Startup_scan_retries_are_bounded()
    {
        var (scheduler, scanner) = Create(_ => true);

        await RunBrieflyAsync(scheduler);

        await scanner.Received(4).ScanAsync(Arg.Any<CancellationToken>()); // first attempt + 3 retries
    }
}
