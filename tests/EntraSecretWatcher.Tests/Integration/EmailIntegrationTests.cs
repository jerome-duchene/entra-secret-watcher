using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Services.Notifications.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EntraSecretWatcher.Tests.Integration;

[Trait("Category", "Integration")]
public class EmailIntegrationTests : IntegrationTestBase
{
    [SkippableFact(DisplayName = "Email — SendAsync envoie un vrai email via Graph API")]
    public async Task SendAsync_SendsRealEmailViaGraphApi()
    {
        var entra = GetOptions<EntraOptions>("Entra");
        var email = GetOptions<EmailOptions>("Notification:Email");

        Skip.If(
            string.IsNullOrWhiteSpace(entra?.TenantId) || string.IsNullOrWhiteSpace(email?.From),
            "Email/Entra non configuré (Integration:Entra et Integration:Email requis)");

        var channel = new EmailNotificationChannel(
            Options.Create(new NotificationOptions { Email = email! with { Enabled = true } }),
            Options.Create(entra!),
            NullLogger<EmailNotificationChannel>.Instance);

        await channel.Invoking(c => c.SendAsync(BuildTestScanResult()))
                     .Should().NotThrowAsync();
    }
}
