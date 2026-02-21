using EntraSecretWatcher.Models;
using EntraSecretWatcher.Services.Notifications;
using EntraSecretWatcher.Services.Notifications.Channels;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace EntraSecretWatcher.Tests.Services;

public class NotificationDispatcherTests
{
    private readonly ScanResult _scanResult = new()
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
        TotalApplicationsScanned = 1
    };

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

        // Should not throw
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

    private static NotificationDispatcher CreateDispatcher(params INotificationChannel[] channels) =>
        new(channels, Substitute.For<ILogger<NotificationDispatcher>>());
}
