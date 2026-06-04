using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Models;
using EntraSecretWatcher.Services.Notifications;
using EntraSecretWatcher.Services.Notifications.Channels;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;

namespace EntraSecretWatcher.Tests.Services;

public class NotificationDispatcherTests
{
    private readonly ScanResult _scanResult = ScanResultWith(credentialCount: 1);

    // ── Canal activé / désactivé ──────────────────────────────────────────────

    [Fact]
    public async Task DispatchAsync_EnabledChannel_CallsSendAsync()
    {
        var channel = Substitute.For<INotificationChannel>();
        channel.IsEnabled.Returns(true);

        var dispatcher = CreateDispatcher(channel);
        await dispatcher.DispatchAsync(_scanResult);

        await channel.Received(1).SendAsync(_scanResult, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_DisabledChannel_NeverCallsSendAsync()
    {
        var channel = Substitute.For<INotificationChannel>();
        channel.IsEnabled.Returns(false);

        var dispatcher = CreateDispatcher(channel);
        await dispatcher.DispatchAsync(_scanResult);

        await channel.DidNotReceive().SendAsync(Arg.Any<ScanResult>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_MultipleEnabledChannels_CallsAllChannels()
    {
        var channel1 = Substitute.For<INotificationChannel>();
        var channel2 = Substitute.For<INotificationChannel>();
        channel1.IsEnabled.Returns(true);
        channel2.IsEnabled.Returns(true);

        var dispatcher = CreateDispatcher(channel1, channel2);
        await dispatcher.DispatchAsync(_scanResult);

        await channel1.Received(1).SendAsync(_scanResult, Arg.Any<CancellationToken>());
        await channel2.Received(1).SendAsync(_scanResult, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_OneChannelFails_OtherChannelStillCalled()
    {
        var failing = Substitute.For<INotificationChannel>();
        var succeeding = Substitute.For<INotificationChannel>();
        failing.IsEnabled.Returns(true);
        succeeding.IsEnabled.Returns(true);
        failing.SendAsync(Arg.Any<ScanResult>(), Arg.Any<CancellationToken>())
               .ThrowsAsync(new HttpRequestException("connection refused"));

        var dispatcher = CreateDispatcher(failing, succeeding);

        await dispatcher.Invoking(d => d.DispatchAsync(_scanResult))
                        .Should().NotThrowAsync();

        await succeeding.Received(1).SendAsync(_scanResult, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_NoEnabledChannels_DoesNotThrow()
    {
        var channel = Substitute.For<INotificationChannel>();
        channel.IsEnabled.Returns(false);

        var dispatcher = CreateDispatcher(channel);

        await dispatcher.Invoking(d => d.DispatchAsync(_scanResult))
                        .Should().NotThrowAsync();
    }

    [Fact]
    public async Task DispatchAsync_EmptyChannelList_DoesNotThrow()
    {
        var dispatcher = CreateDispatcher();

        await dispatcher.Invoking(d => d.DispatchAsync(_scanResult))
                        .Should().NotThrowAsync();
    }

    // ── GroupedReport = true ──────────────────────────────────────────────────

    [Fact]
    public async Task DispatchAsync_GroupedReport_True_MultipleCredentials_CallsSendAsyncOnce()
    {
        var channel = Substitute.For<INotificationChannel>();
        channel.IsEnabled.Returns(true);
        var result = ScanResultWith(credentialCount: 3);

        var dispatcher = CreateDispatcher(groupedReport: true, channel);
        await dispatcher.DispatchAsync(result);

        await channel.Received(1).SendAsync(result, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_GroupedReport_True_SendReceivesAllCredentials()
    {
        var channel = Substitute.For<INotificationChannel>();
        channel.IsEnabled.Returns(true);
        var result = ScanResultWith(credentialCount: 3);

        ScanResult? captured = null;
        channel.SendAsync(Arg.Do<ScanResult>(r => captured = r), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var dispatcher = CreateDispatcher(groupedReport: true, channel);
        await dispatcher.DispatchAsync(result);

        captured!.Credentials.Should().HaveCount(3);
    }

    // ── GroupedReport = false ─────────────────────────────────────────────────

    [Fact]
    public async Task DispatchAsync_GroupedReport_False_MultipleCredentials_CallsSendAsyncPerCredential()
    {
        var channel = Substitute.For<INotificationChannel>();
        channel.IsEnabled.Returns(true);
        var result = ScanResultWith(credentialCount: 3);

        var dispatcher = CreateDispatcher(groupedReport: false, channel);
        await dispatcher.DispatchAsync(result);

        await channel.Received(3).SendAsync(Arg.Any<ScanResult>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_GroupedReport_False_EachBatchContainsExactlyOneCredential()
    {
        var channel = Substitute.For<INotificationChannel>();
        channel.IsEnabled.Returns(true);
        var result = ScanResultWith(credentialCount: 3);

        var capturedResults = new List<ScanResult>();
        channel.SendAsync(Arg.Do<ScanResult>(r => capturedResults.Add(r)), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var dispatcher = CreateDispatcher(groupedReport: false, channel);
        await dispatcher.DispatchAsync(result);

        capturedResults.Should().AllSatisfy(r => r.Credentials.Should().HaveCount(1));
    }

    [Fact]
    public async Task DispatchAsync_GroupedReport_False_AllCredentialsCovered()
    {
        var channel = Substitute.For<INotificationChannel>();
        channel.IsEnabled.Returns(true);
        var result = ScanResultWith(credentialCount: 2);

        var capturedResults = new List<ScanResult>();
        channel.SendAsync(Arg.Do<ScanResult>(r => capturedResults.Add(r)), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var dispatcher = CreateDispatcher(groupedReport: false, channel);
        await dispatcher.DispatchAsync(result);

        capturedResults.SelectMany(r => r.Credentials)
                       .Should().BeEquivalentTo(result.Credentials);
    }

    [Fact]
    public async Task DispatchAsync_GroupedReport_False_SingleCredential_CallsSendAsyncOnce()
    {
        var channel = Substitute.For<INotificationChannel>();
        channel.IsEnabled.Returns(true);
        var result = ScanResultWith(credentialCount: 1);

        var dispatcher = CreateDispatcher(groupedReport: false, channel);
        await dispatcher.DispatchAsync(result);

        await channel.Received(1).SendAsync(Arg.Any<ScanResult>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_GroupedReport_False_MultipleChannels_EachReceivesAllBatches()
    {
        var channel1 = Substitute.For<INotificationChannel>();
        var channel2 = Substitute.For<INotificationChannel>();
        channel1.IsEnabled.Returns(true);
        channel2.IsEnabled.Returns(true);
        var result = ScanResultWith(credentialCount: 2);

        var dispatcher = CreateDispatcher(groupedReport: false, channel1, channel2);
        await dispatcher.DispatchAsync(result);

        await channel1.Received(2).SendAsync(Arg.Any<ScanResult>(), Arg.Any<CancellationToken>());
        await channel2.Received(2).SendAsync(Arg.Any<ScanResult>(), Arg.Any<CancellationToken>());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static NotificationDispatcher CreateDispatcher(params INotificationChannel[] channels) =>
        CreateDispatcher(groupedReport: true, channels);

    private static NotificationDispatcher CreateDispatcher(bool groupedReport, params INotificationChannel[] channels) =>
        new(channels,
            Options.Create(new WatcherOptions { GroupedReport = groupedReport }),
            Substitute.For<ILogger<NotificationDispatcher>>());

    private static ScanResult ScanResultWith(int credentialCount) => new()
    {
        TenantName = "Test",
        TenantId = "test-id",
        ScannedAt = DateTimeOffset.UtcNow,
        TotalApplicationsScanned = credentialCount,
        Credentials = Enumerable.Range(1, credentialCount)
            .Select(i => new ExpiringCredential
            {
                AppName = $"App{i}",
                AppId = $"app-id-{i}",
                Type = CredentialType.Secret,
                CredentialName = $"Secret{i}",
                ExpiresOn = DateTimeOffset.UtcNow.AddDays(5),
                DaysLeft = 5
            })
            .ToList()
            .AsReadOnly()
    };
}
