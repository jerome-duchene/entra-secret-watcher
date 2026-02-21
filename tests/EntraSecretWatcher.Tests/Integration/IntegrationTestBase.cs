using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EntraSecretWatcher.Tests.Integration;

public abstract class IntegrationTestBase
{
    protected readonly IConfiguration Configuration;

    protected IntegrationTestBase()
    {
        Configuration = new ConfigurationBuilder()
            .AddUserSecrets<IntegrationTestBase>()
            .AddEnvironmentVariables()
            .Build();
    }

    protected T? GetOptions<T>(string path) where T : class, new()
        => Configuration.GetSection(path).Get<T>();

    protected static IHttpClientFactory CreateHttpClientFactory() =>
        new ServiceCollection()
            .AddHttpClient()
            .BuildServiceProvider()
            .GetRequiredService<IHttpClientFactory>();

    internal static ScanResult BuildTestScanResult() => new()
    {
        TenantName = "Integration Test Tenant",
        TenantId = "integration-test",
        ScannedAt = DateTimeOffset.UtcNow,
        TotalApplicationsScanned = 3,
        Credentials = new List<ExpiringCredential>
        {
            new()
            {
                AppName = "App A",
                AppId = "00000000-0000-0000-0000-000000000001",
                Type = CredentialType.Secret,
                CredentialName = "prod-secret",
                DaysLeft = -2,
                ExpiresOn = DateTimeOffset.UtcNow.AddDays(-2)
            },
            new()
            {
                AppName = "App B",
                AppId = "00000000-0000-0000-0000-000000000002",
                Type = CredentialType.Certificate,
                CredentialName = "tls-cert",
                DaysLeft = 15,
                ExpiresOn = DateTimeOffset.UtcNow.AddDays(15)
            }
        }.AsReadOnly()
    };
}
