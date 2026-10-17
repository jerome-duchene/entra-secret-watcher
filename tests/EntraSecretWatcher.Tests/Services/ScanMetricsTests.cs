using EntraSecretWatcher.Models;
using EntraSecretWatcher.Services;
using FluentAssertions;
using Prometheus;

namespace EntraSecretWatcher.Tests.Services;

public class ScanMetricsTests
{
    private static ScanResult CreateResult(params int[] daysLeft) => new()
    {
        TenantName = "Test",
        TenantId = "test-id",
        ScannedAt = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000),
        TotalApplicationsScanned = 12,
        Credentials = daysLeft.Select((d, i) => new ExpiringCredential
        {
            AppName = $"App{i}",
            AppId = $"id{i}",
            Type = CredentialType.Secret,
            CredentialName = $"cred{i}",
            ExpiresOn = DateTimeOffset.UtcNow.AddDays(d),
            DaysLeft = d
        }).ToList().AsReadOnly()
    };

    private static async Task<string> ScrapeAsync(CollectorRegistry registry)
    {
        using var stream = new MemoryStream();
        await registry.CollectAndExportAsTextAsync(stream);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    [Fact]
    public async Task RecordSuccess_ExposesCountsAndPerCredentialGauge()
    {
        var registry = new CollectorRegistry();
        var metrics = new ScanMetrics(registry);

        metrics.RecordSuccess(CreateResult(-3, 5));

        var output = await ScrapeAsync(registry);
        output.Should().Contain("esw_credentials_expired 1");
        output.Should().Contain("esw_credentials_expiring_soon 1");
        output.Should().Contain("esw_apps_scanned 12");
        output.Should().Contain("esw_last_scan_success 1");
        output.Should().Contain("esw_last_scan_timestamp_seconds 1700000000");
        output.Should().Contain("esw_credential_days_left{app_name=\"App0\",app_id=\"id0\",type=\"secret\",credential_name=\"cred0\"} -3");
    }

    [Fact]
    public async Task RecordSuccess_SecondScan_DropsCredentialsNoLongerReported()
    {
        var registry = new CollectorRegistry();
        var metrics = new ScanMetrics(registry);

        metrics.RecordSuccess(CreateResult(5));
        metrics.RecordSuccess(CreateResult());

        var output = await ScrapeAsync(registry);
        output.Should().NotContain("esw_credential_days_left{");
    }

    [Fact]
    public async Task RecordFailure_SetsSuccessToZeroAndKeepsPreviousCounts()
    {
        var registry = new CollectorRegistry();
        var metrics = new ScanMetrics(registry);
        metrics.RecordSuccess(CreateResult(5));

        metrics.RecordFailure();

        var output = await ScrapeAsync(registry);
        output.Should().Contain("esw_last_scan_success 0");
        output.Should().Contain("esw_credentials_expiring_soon 1");
    }
}
