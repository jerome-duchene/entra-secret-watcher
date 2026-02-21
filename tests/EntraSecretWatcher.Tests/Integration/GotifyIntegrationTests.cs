using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Services.Notifications.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EntraSecretWatcher.Tests.Integration;

[Trait("Category", "Integration")]
public class GotifyIntegrationTests : IntegrationTestBase
{
    [SkippableFact(DisplayName = "Gotify — SendAsync envoie une vraie notification")]
    public async Task SendAsync_SendsRealNotificationToGotify()
    {
        var gotify = GetOptions<GotifyOptions>("Notification:Gotify");
        Skip.If(string.IsNullOrWhiteSpace(gotify?.Url), "Gotify non configuré (Integration:Gotify:Url manquant)");

        var channel = new GotifyNotificationChannel(
            Options.Create(new NotificationOptions { Gotify = gotify! with { Enabled = true } }),
            CreateHttpClientFactory(),
            NullLogger<GotifyNotificationChannel>.Instance);

        await channel.Invoking(c => c.SendAsync(BuildTestScanResult()))
                     .Should().NotThrowAsync();
    }
}
