using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Services.Notifications.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EntraSecretWatcher.Tests.Integration;

[Trait("Category", "Integration")]
public class TeamsIntegrationTests : IntegrationTestBase
{
    [SkippableFact(DisplayName = "Teams — SendAsync envoie une vraie Adaptive Card via webhook")]
    public async Task SendAsync_SendsRealAdaptiveCardToTeams()
    {
        var teams = GetOptions<TeamsOptions>("Notification:Teams");
        Skip.If(string.IsNullOrWhiteSpace(teams?.WebhookUrl), "Teams non configuré (Integration:Teams:WebhookUrl manquant)");

        var channel = new TeamsNotificationChannel(
            Options.Create(new NotificationOptions { Teams = teams! with { Enabled = true } }),
            CreateHttpClientFactory(),
            NullLogger<TeamsNotificationChannel>.Instance);

        await channel.Invoking(c => c.SendAsync(BuildTestScanResult()))
                     .Should().NotThrowAsync();
    }
}
