using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Services.Notifications.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EntraSecretWatcher.Tests.Integration;

[Trait("Category", "Integration")]
public class TeamsIntegrationTests : IntegrationTestBase
{
    [Fact(DisplayName = "Teams — SendAsync envoie une vraie Adaptive Card via webhook")]
    public async Task SendAsync_SendsRealAdaptiveCardToTeams()
    {
        var teams = GetOptions<TeamsOptions>("Notification:Teams");
        if (string.IsNullOrWhiteSpace(teams?.WebhookUrl))
            Xunit.Assert.Skip("Teams non configuré (Integration:Teams:WebhookUrl manquant)");

        var channel = new TeamsNotificationChannel(
            Options.Create(new NotificationOptions { Teams = teams! with { Enabled = true } }),
            CreateHttpClientFactory(),
            NullLogger<TeamsNotificationChannel>.Instance);

        await channel.Invoking(c => c.SendAsync(BuildTestScanResult()))
                     .Should().NotThrowAsync();
    }
}
