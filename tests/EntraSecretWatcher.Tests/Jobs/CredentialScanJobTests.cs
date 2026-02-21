using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Jobs;
using EntraSecretWatcher.Models;
using EntraSecretWatcher.Services;
using EntraSecretWatcher.Services.Notifications;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace EntraSecretWatcher.Tests.Jobs;

public class CredentialScanJobTests
{
    private static readonly ScanResult EmptyResult = new()
    {
        TenantName = "Test",
        TenantId = "test-id",
        ScannedAt = DateTimeOffset.UtcNow,
        Credentials = new List<ExpiringCredential>().AsReadOnly(),
        TotalApplicationsScanned = 5
    };

    private static readonly ScanResult ResultWithCredentials = new()
    {
        TenantName = "Test",
        TenantId = "test-id",
        ScannedAt = DateTimeOffset.UtcNow,
        Credentials = new List<ExpiringCredential>
        {
            new()
            {
                AppName = "App",
                AppId = "id",
                Type = CredentialType.Secret,
                CredentialName = "Secret",
                ExpiresOn = DateTimeOffset.UtcNow.AddDays(5),
                DaysLeft = 5
            }
        }.AsReadOnly(),
        TotalApplicationsScanned = 5
    };

    [Fact]
    public async Task ExecuteAsync_NoExpiringCredentials_DispatcherIsNotCalled()
    {
        var (scanner, dispatcher, job) = CreateJob(dryRun: false);
        scanner.ScanAsync(Arg.Any<CancellationToken>()).Returns(EmptyResult);

        await job.ExecuteAsync();

        await dispatcher.DidNotReceive().DispatchAsync(Arg.Any<ScanResult>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithExpiringCredentials_DispatcherIsCalled()
    {
        var (scanner, dispatcher, job) = CreateJob(dryRun: false);
        scanner.ScanAsync(Arg.Any<CancellationToken>()).Returns(ResultWithCredentials);
        dispatcher.DispatchAsync(Arg.Any<ScanResult>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await job.ExecuteAsync();

        await dispatcher.Received(1).DispatchAsync(ResultWithCredentials, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_DryRunEnabled_ScannerIsCalledButDispatcherIsNot()
    {
        var (scanner, dispatcher, job) = CreateJob(dryRun: true);
        scanner.ScanAsync(Arg.Any<CancellationToken>()).Returns(ResultWithCredentials);

        await job.ExecuteAsync();

        await scanner.Received(1).ScanAsync(Arg.Any<CancellationToken>());
        await dispatcher.DidNotReceive().DispatchAsync(Arg.Any<ScanResult>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ScannerThrows_ExceptionIsPropagated()
    {
        var (scanner, _, job) = CreateJob(dryRun: false);
        scanner.ScanAsync(Arg.Any<CancellationToken>())
               .ThrowsAsync(new InvalidOperationException("Graph API error"));

        await job.Invoking(j => j.ExecuteAsync())
                 .Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("Graph API error");
    }

    private static (GraphCredentialScanner scanner, NotificationDispatcher dispatcher, CredentialScanJob job)
        CreateJob(bool dryRun)
    {
        var entraOpts = Options.Create(new EntraOptions
        {
            TenantId = "test-tenant",
            ClientId = "test-client",
            ClientSecret = "test-secret"
        });
        var watcherOpts = Options.Create(new WatcherOptions { DryRun = dryRun });

        var scanner = Substitute.For<GraphCredentialScanner>(
            entraOpts,
            watcherOpts,
            Substitute.For<ILogger<GraphCredentialScanner>>());

        var dispatcher = Substitute.For<NotificationDispatcher>(
            Array.Empty<EntraSecretWatcher.Services.Notifications.Channels.INotificationChannel>(),
            Substitute.For<ILogger<NotificationDispatcher>>());

        var job = new CredentialScanJob(
            scanner,
            dispatcher,
            watcherOpts,
            Substitute.For<ILogger<CredentialScanJob>>());

        return (scanner, dispatcher, job);
    }
}
