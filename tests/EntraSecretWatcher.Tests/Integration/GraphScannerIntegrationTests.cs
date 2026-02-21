using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EntraSecretWatcher.Tests.Integration;

[Trait("Category", "Integration")]
public class GraphScannerIntegrationTests : IntegrationTestBase
{
    [SkippableFact(DisplayName = "Graph — ScanAsync retourne des résultats réels du tenant")]
    public async Task ScanAsync_ReturnsRealResultFromTenant()
    {
        var entra = GetOptions<EntraOptions>("Entra");
        Skip.If(string.IsNullOrWhiteSpace(entra?.TenantId), "Entra non configuré (Integration:Entra:TenantId manquant)");

        var scanner = new GraphCredentialScanner(
            Options.Create(entra!),
            Options.Create(new WatcherOptions()),
            NullLogger<GraphCredentialScanner>.Instance);

        var result = await scanner.ScanAsync();

        result.TenantId.Should().Be(entra!.TenantId);
        result.TenantName.Should().Be(entra.TenantName);
        result.TotalApplicationsScanned.Should().BeGreaterThanOrEqualTo(0);
        result.Credentials.Should().OnlyContain(
            c => c.DaysLeft <= new WatcherOptions().ThresholdDays);
    }
}
