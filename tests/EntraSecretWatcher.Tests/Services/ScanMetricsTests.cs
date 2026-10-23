using System.Diagnostics.Metrics;
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
            KeyId = $"key{i}",
            Type = CredentialType.Secret,
            CredentialName = $"cred{i}",
            ExpiresOn = DateTimeOffset.UtcNow.AddDays(d),
            DaysLeft = d
        }).ToList().AsReadOnly()
    };

    [Fact]
    public void RecordSuccess_ExposesCountsAndPerCredentialGauge()
    {
        using var metrics = NewMetrics();

        metrics.RecordSuccess(CreateResult(-3, 5));

        var observed = Observe(metrics);
        observed.Single("esw_credentials_expired").Value.Should().Be(1);
        observed.Single("esw_credentials_expired").Tags["tenant_name"].Should().Be("Contoso");
        observed.Single("esw_credentials_expiring_soon").Value.Should().Be(1);
        observed.Single("esw_apps_scanned").Value.Should().Be(12);
        observed.Single("esw_last_scan_success").Value.Should().Be(1);
        observed.Single("esw_last_scan_timestamp_seconds").Value.Should().Be(1_700_000_000);

        var daysLeft = observed.All("esw_credential_days_left");
        daysLeft.Should().HaveCount(2);
        var expired = daysLeft.Single(m => m.Value == -3);
        expired.Tags["app_name"].Should().Be("App0");
        expired.Tags["app_id"].Should().Be("id0");
        expired.Tags["key_id"].Should().Be("key0");
        expired.Tags["type"].Should().Be("secret");
        expired.Tags["credential_name"].Should().Be("cred0");
        expired.Tags["tenant_name"].Should().Be("Contoso");
        expired.Tags["tenant_id"].Should().Be("tid-1");
    }

    [Fact]
    public void BeforeFirstScan_NoSuccessSeriesAndNoCredentials()
    {
        using var metrics = NewMetrics();

        var observed = Observe(metrics);

        observed.All("esw_last_scan_success").Should().BeEmpty();
        observed.All("esw_credentials_expired").Should().BeEmpty();
        observed.All("esw_credential_days_left").Should().BeEmpty();
    }

    [Fact]
    public void FailureBeforeAnySuccess_ExposesFailureButNoFigures()
    {
        using var metrics = NewMetrics();

        metrics.RecordFailure();

        var observed = Observe(metrics);
        observed.Single("esw_last_scan_success").Value.Should().Be(0);
        observed.All("esw_last_scan_timestamp_seconds").Should().ContainSingle();
        observed.All("esw_credentials_expired").Should().BeEmpty();
        observed.All("esw_apps_scanned").Should().BeEmpty();
    }

    [Fact]
    public void RecordSuccess_SecondScan_DropsCredentialsNoLongerReported()
    {
        using var metrics = NewMetrics();

        metrics.RecordSuccess(CreateResult(5));
        metrics.RecordSuccess(CreateResult());

        Observe(metrics).All("esw_credential_days_left").Should().BeEmpty();
    }

    [Fact]
    public void RecordFailure_SetsSuccessToZeroAndKeepsPreviousFigures()
    {
        using var metrics = NewMetrics();
        metrics.RecordSuccess(CreateResult(5));

        metrics.RecordFailure();

        var observed = Observe(metrics);
        observed.Single("esw_last_scan_success").Value.Should().Be(0);
        observed.Single("esw_credentials_expiring_soon").Value.Should().Be(1);
        observed.All("esw_credential_days_left").Should().HaveCount(1);
    }

    [Fact]
    public async Task PrometheusEndpoint_PublishesExactMetricNames()
    {
        var registry = new CollectorRegistry();
        using var metrics = NewMetrics(registry);
        metrics.RecordSuccess(CreateResult(-3, 5));

        var output = await ScrapeAsync(registry);

        // exact lines: the names must not be prefixed (e.g. with the meter name)
        AssertSample(output, "esw_credentials_expired", "1");
        AssertSample(output, "esw_credentials_expiring_soon", "1");
        AssertSample(output, "esw_apps_scanned", "12");
        AssertSample(output, "esw_last_scan_success", "1");
        AssertSample(output, "esw_last_scan_timestamp_seconds", "1700000000");
        AssertSample(output, "esw_credential_days_left", "-3",
            "app_name=\"App0\"", "app_id=\"id0\"", "key_id=\"key0\"", "type=\"secret\"", "credential_name=\"cred0\"");
    }

    [Fact]
    public async Task PrometheusEndpoint_BeforeFirstScan_PublishesNoSeries()
    {
        var registry = new CollectorRegistry();
        using var metrics = NewMetrics(registry);

        (await ScrapeAsync(registry)).Should().NotMatchRegex(@"(?m)^esw_credentials_expired[ {]");
    }

    [Fact]
    public async Task PrometheusEndpoint_SecondScan_DropsRenewedCredentials()
    {
        var registry = new CollectorRegistry();
        using var metrics = NewMetrics(registry);

        metrics.RecordSuccess(CreateResult(5));
        metrics.RecordSuccess(CreateResult());

        (await ScrapeAsync(registry)).Should().NotContain("esw_credential_days_left{");
    }

    [Fact]
    public async Task PrometheusEndpoint_Failure_FlagsSuccessAsZero()
    {
        var registry = new CollectorRegistry();
        using var metrics = NewMetrics(registry);
        metrics.RecordSuccess(CreateResult(5));

        metrics.RecordFailure();

        var output = await ScrapeAsync(registry);
        AssertSample(output, "esw_last_scan_success", "0");
        AssertSample(output, "esw_credentials_expiring_soon", "1");
    }

    [Fact]
    public async Task PrometheusEndpoint_PublishesApplicationHealthMetrics()
    {
        var registry = new CollectorRegistry();
        using var metrics = NewMetrics(registry);

        metrics.RecordSuccess(CreateResult());
        metrics.RecordFailure();
        metrics.CountNotification("Webhook", success: false);
        metrics.CountGraphRequest(success: true);
        metrics.RecordScanDuration(TimeSpan.FromSeconds(1.5));

        var output = await ScrapeAsync(registry);
        AssertSample(output, "esw_scans_total", "1", "result=\"success\"");
        AssertSample(output, "esw_scans_total", "1", "result=\"failure\"");
        AssertSample(output, "esw_notifications_total", "1", "channel=\"Webhook\"", "result=\"failure\"");
        AssertSample(output, "esw_graph_requests_total", "1", "result=\"success\"");
        AssertSample(output, "esw_last_scan_duration_seconds", "1.5");
        output.Should().MatchRegex(@"(?m)^esw_process_start_time_seconds\{");
        output.Should().Contain("# TYPE esw_scans_total counter");
    }

    [Fact]
    public async Task PrometheusEndpoint_BeforeAnyEvent_PublishesNoCounterAndNoDuration()
    {
        var registry = new CollectorRegistry();
        using var metrics = NewMetrics(registry);

        var output = await ScrapeAsync(registry);

        output.Should().NotMatchRegex(@"(?m)^esw_(scans|notifications|graph_requests)_total[ {]");
        output.Should().NotMatchRegex(@"(?m)^esw_last_scan_duration_seconds[ {]");
    }

    [Fact]
    public void Counters_ArePushedWithTheirLabelsAndTheTenant()
    {
        using var metrics = NewMetrics();
        var items = new List<(string Name, long Value, Dictionary<string, object?> Tags)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (ReferenceEquals(instrument.Meter, metrics.Meter))
                    l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((i, v, tags, _) => items.Add((i.Name, v, ToDictionary(tags))));
        listener.Start();

        metrics.CountNotification("Teams", success: true);
        metrics.CountGraphRequest(success: false);
        metrics.RecordFailure();

        var notification = items.Single(i => i.Name == "esw_notifications_total");
        notification.Value.Should().Be(1);
        notification.Tags["channel"].Should().Be("Teams");
        notification.Tags["result"].Should().Be("success");
        notification.Tags["tenant_name"].Should().Be("Contoso");
        items.Single(i => i.Name == "esw_graph_requests_total").Tags["result"].Should().Be("failure");
        items.Single(i => i.Name == "esw_scans_total").Tags["result"].Should().Be("failure");
    }

    [Fact]
    public void ScanDuration_IsObservedInSecondsOnceRecorded()
    {
        using var metrics = NewMetrics();
        Observe(metrics).All("esw_last_scan_duration_seconds").Should().BeEmpty();

        metrics.RecordScanDuration(TimeSpan.FromMilliseconds(2500));

        Observe(metrics).Single("esw_last_scan_duration_seconds").Value.Should().Be(2.5);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ScanMetrics NewMetrics(CollectorRegistry? registry = null) =>
        new($"test-{Guid.NewGuid()}", registry ?? new CollectorRegistry(), "Contoso", "tid-1");

    private static async Task<string> ScrapeAsync(CollectorRegistry registry)
    {
        using var stream = new MemoryStream();
        await registry.CollectAndExportAsTextAsync(stream, TestContext.Current.CancellationToken);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    // Exact sample line: the name must not be prefixed, and the tenant labels must be present
    private static void AssertSample(string output, string name, string value, params string[] labels)
    {
        var lines = output.Split('\n').Select(l => l.TrimEnd('\r'))
            .Where(l => (l.StartsWith(name + "{") || l.StartsWith(name + " ")) && labels.All(l.Contains)).ToList();

        lines.Should().ContainSingle($"{name} with {string.Join(',', labels)} should be published once");
        var line = lines[0];
        line.Should().Contain("tenant_name=\"Contoso\"").And.Contain("tenant_id=\"tid-1\"");
        line.Should().EndWith(" " + value);
    }

    private sealed record Observation(string Instrument, double Value, IReadOnlyDictionary<string, object?> Tags);

    private sealed class Observations(List<Observation> items)
    {
        public IReadOnlyList<Observation> All(string instrument) => [.. items.Where(i => i.Instrument == instrument)];
        public Observation Single(string instrument) => All(instrument).Should().ContainSingle().Subject;
    }

    private static Observations Observe(ScanMetrics metrics)
    {
        var items = new List<Observation>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (ReferenceEquals(instrument.Meter, metrics.Meter))
                    l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<int>((i, v, tags, _) =>
            items.Add(new Observation(i.Name, v, ToDictionary(tags))));
        listener.SetMeasurementEventCallback<double>((i, v, tags, _) =>
            items.Add(new Observation(i.Name, v, ToDictionary(tags))));
        listener.Start();
        listener.RecordObservableInstruments();
        return new Observations(items);
    }

    private static Dictionary<string, object?> ToDictionary(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var tag in tags) dict[tag.Key] = tag.Value;
        return dict;
    }
}
