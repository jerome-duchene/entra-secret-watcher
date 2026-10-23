using System.Diagnostics.Metrics;
using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Models;
using Microsoft.Extensions.Options;
using Prometheus;

namespace EntraSecretWatcher.Services;

/// <summary>
/// Metrics describing the outcome of the last scan, published two ways from the same snapshot:
/// <list type="bullet">
/// <item><c>System.Diagnostics.Metrics</c> observable gauges, pushed over OTLP by OpenTelemetry;</item>
/// <item>prometheus-net gauges, served on <c>/metrics</c> for Prometheus to scrape.</item>
/// </list>
/// Both use the same names (<c>esw_*</c>): prometheus-net's own Meter adapter would prefix them with
/// the meter name, which is why <c>/metrics</c> does not rely on it for these metrics.
/// Every series carries <c>tenant_name</c> and <c>tenant_id</c> labels so that several instances
/// (one container per tenant) can be told apart whatever the collection path.
/// Registered as a singleton: scans run rarely (cron) while metrics are read continuously,
/// so the last values are kept between two scans.
/// </summary>
internal sealed class ScanMetrics : IDisposable
{
    public const string MeterName = "EntraSecretWatcher";

    private static readonly GaugeConfiguration NoInitialValue = new() { SuppressInitialValue = true };

    private sealed record Snapshot(
        IReadOnlyList<ExpiringCredential> Credentials,
        int Expired,
        int ExpiringSoon,
        int AppsScanned,
        double LastScanTimestamp,
        bool? LastScanSuccess,
        bool HasResult)
    {
        public static readonly Snapshot Empty = new([], 0, 0, 0, 0, null, false);
    }

    private volatile Snapshot _snapshot = Snapshot.Empty;
    private readonly object _writeLock = new();

    // prometheus-net side
    private readonly Gauge _promDaysLeft;
    private readonly Gauge _promExpired;
    private readonly Gauge _promExpiringSoon;
    private readonly Gauge _promAppsScanned;
    private readonly Gauge _promLastScanTimestamp;
    private readonly Gauge _promLastScanSuccess;
    private readonly List<string[]> _promReportedLabels = [];

    // Application health, independent from the credentials
    private readonly Counter<long> _scans;
    private readonly Counter<long> _notifications;
    private readonly Counter<long> _graphRequests;
    private readonly Prometheus.Counter _promScans;
    private readonly Prometheus.Counter _promNotifications;
    private readonly Prometheus.Counter _promGraphRequests;
    private readonly Gauge _promLastScanDuration;
    private readonly long _startedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    private long _lastScanDurationTicks = -1;

    private readonly KeyValuePair<string, object?>[] _tenantTags;

    public ScanMetrics(IOptions<EntraOptions> entraOptions)
        : this(MeterName, Metrics.DefaultRegistry, entraOptions.Value.TenantName, entraOptions.Value.TenantId)
    {
    }

    internal ScanMetrics(string meterName, CollectorRegistry registry, string tenantName, string tenantId)
    {
        _tenantTags =
        [
            new("tenant_name", tenantName),
            new("tenant_id", tenantId)
        ];

        // ── OpenTelemetry / System.Diagnostics.Metrics ──
        Meter = new Meter(meterName);

        Meter.CreateObservableGauge(
            "esw_credential_days_left",
            ObserveCredentials,
            description: "Days left before expiration of a credential reported by the last scan (negative = expired).");
        Meter.CreateObservableGauge(
            "esw_credentials_expired",
            () => Scalar(s => s.Expired, requireResult: true),
            description: "Number of expired credentials found by the last scan.");
        Meter.CreateObservableGauge(
            "esw_credentials_expiring_soon",
            () => Scalar(s => s.ExpiringSoon, requireResult: true),
            description: "Number of credentials about to expire found by the last scan.");
        Meter.CreateObservableGauge(
            "esw_apps_scanned",
            () => Scalar(s => s.AppsScanned, requireResult: true),
            description: "Number of app registrations inspected by the last scan.");
        Meter.CreateObservableGauge(
            "esw_last_scan_timestamp_seconds",
            () => Scalar(s => s.LastScanTimestamp, requireResult: false),
            description: "Unix timestamp of the last scan attempt.");
        Meter.CreateObservableGauge(
            "esw_last_scan_success",
            ObserveLastScanSuccess,
            description: "1 if the last scan succeeded, 0 otherwise.");

        _scans = Meter.CreateCounter<long>(
            "esw_scans_total", description: "Scans run since the process started, by result.");
        _notifications = Meter.CreateCounter<long>(
            "esw_notifications_total",
            description: "Notifications attempted since the process started, by channel and result.");
        _graphRequests = Meter.CreateCounter<long>(
            "esw_graph_requests_total",
            description: "Microsoft Graph calls made since the process started, by result.");
        Meter.CreateObservableGauge(
            "esw_last_scan_duration_seconds",
            ObserveLastScanDuration,
            description: "Duration of the last scan attempt, in seconds.");
        Meter.CreateObservableGauge(
            "esw_process_start_time_seconds",
            () => new Measurement<long>(_startedAt, _tenantTags),
            description: "Unix timestamp of the start of the process.");

        // ── prometheus-net (/metrics) ──
        var factory = Metrics
            .WithCustomRegistry(registry)
            .WithLabels(new Dictionary<string, string>
            {
                ["tenant_name"] = tenantName,
                ["tenant_id"] = tenantId
            });

        _promDaysLeft = factory.CreateGauge(
            "esw_credential_days_left",
            "Days left before expiration of a credential reported by the last scan (negative = expired).",
            new GaugeConfiguration { LabelNames = ["app_name", "app_id", "key_id", "type", "credential_name"] });
        _promExpired = factory.CreateGauge(
            "esw_credentials_expired",
            "Number of expired credentials found by the last scan.",
            NoInitialValue);
        _promExpiringSoon = factory.CreateGauge(
            "esw_credentials_expiring_soon",
            "Number of credentials about to expire found by the last scan.",
            NoInitialValue);
        _promAppsScanned = factory.CreateGauge(
            "esw_apps_scanned",
            "Number of app registrations inspected by the last scan.",
            NoInitialValue);
        _promLastScanTimestamp = factory.CreateGauge(
            "esw_last_scan_timestamp_seconds",
            "Unix timestamp of the last scan attempt.",
            NoInitialValue);
        _promLastScanSuccess = factory.CreateGauge(
            "esw_last_scan_success",
            "1 if the last scan succeeded, 0 otherwise.",
            NoInitialValue);

        // A counter series only appears with its first event
        _promScans = factory.CreateCounter(
            "esw_scans_total",
            "Scans run since the process started, by result.",
            new CounterConfiguration { LabelNames = ["result"], SuppressInitialValue = true });
        _promNotifications = factory.CreateCounter(
            "esw_notifications_total",
            "Notifications attempted since the process started, by channel and result.",
            new CounterConfiguration { LabelNames = ["channel", "result"], SuppressInitialValue = true });
        _promGraphRequests = factory.CreateCounter(
            "esw_graph_requests_total",
            "Microsoft Graph calls made since the process started, by result.",
            new CounterConfiguration { LabelNames = ["result"], SuppressInitialValue = true });
        _promLastScanDuration = factory.CreateGauge(
            "esw_last_scan_duration_seconds",
            "Duration of the last scan attempt, in seconds.",
            NoInitialValue);
        factory.CreateGauge(
                "esw_process_start_time_seconds",
                "Unix timestamp of the start of the process.")
            .Set(_startedAt);
    }

    internal Meter Meter { get; }

    public void CountNotification(string channel, bool success)
    {
        _notifications.Add(1, Tag("channel", channel), Tag("result", ResultOf(success)), _tenantTags[0], _tenantTags[1]);
        _promNotifications.WithLabels(channel, ResultOf(success)).Inc();
    }

    public void CountGraphRequest(bool success)
    {
        _graphRequests.Add(1, Tag("result", ResultOf(success)), _tenantTags[0], _tenantTags[1]);
        _promGraphRequests.WithLabels(ResultOf(success)).Inc();
    }

    public void RecordScanDuration(TimeSpan duration)
    {
        Volatile.Write(ref _lastScanDurationTicks, duration.Ticks);
        _promLastScanDuration.Set(duration.TotalSeconds);
    }

    private void CountScan(bool success)
    {
        _scans.Add(1, Tag("result", ResultOf(success)), _tenantTags[0], _tenantTags[1]);
        _promScans.WithLabels(ResultOf(success)).Inc();
    }

    private static string ResultOf(bool success) => success ? "success" : "failure";

    private static KeyValuePair<string, object?> Tag(string key, string value) => new(key, value);

    public void RecordSuccess(ScanResult result)
    {
        CountScan(true);
        lock (_writeLock)
        {
            _snapshot = new Snapshot(
                result.Credentials,
                result.ExpiredCount,
                result.ExpiringSoonCount,
                result.TotalApplicationsScanned,
                result.ScannedAt.ToUnixTimeSeconds(),
                LastScanSuccess: true,
                HasResult: true);

            // Drop the series of the previous scan: a renewed credential must disappear
            foreach (var labels in _promReportedLabels)
                _promDaysLeft.RemoveLabelled(labels);
            _promReportedLabels.Clear();

            foreach (var cred in result.Credentials)
            {
                string[] labels = LabelsOf(cred);
                _promDaysLeft.WithLabels(labels).Set(cred.DaysLeft);
                _promReportedLabels.Add(labels);
            }

            _promExpired.Set(result.ExpiredCount);
            _promExpiringSoon.Set(result.ExpiringSoonCount);
            _promAppsScanned.Set(result.TotalApplicationsScanned);
            _promLastScanTimestamp.Set(_snapshot.LastScanTimestamp);
            _promLastScanSuccess.Set(1);
        }
    }

    public void RecordFailure()
    {
        CountScan(false);
        lock (_writeLock)
        {
            // Keep the figures of the last successful scan, only flag the failure
            _snapshot = _snapshot with
            {
                LastScanTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                LastScanSuccess = false
            };

            _promLastScanTimestamp.Set(_snapshot.LastScanTimestamp);
            _promLastScanSuccess.Set(0);
        }
    }

    private static string[] LabelsOf(ExpiringCredential c) =>
        [c.AppName, c.AppId, c.KeyId, c.Type.ToString().ToLowerInvariant(), c.CredentialName];

    // No measurement before the first scan: an absent series is clearer than a misleading 0.
    // requireResult: the figures only exist once a scan has succeeded; the timestamp also after a failure.
    private IEnumerable<Measurement<T>> Scalar<T>(Func<Snapshot, T> read, bool requireResult) where T : struct
    {
        var snapshot = _snapshot;
        if (snapshot.LastScanSuccess is null || (requireResult && !snapshot.HasResult))
            yield break;
        yield return new Measurement<T>(read(snapshot), _tenantTags);
    }

    private IEnumerable<Measurement<double>> ObserveLastScanDuration()
    {
        var ticks = Volatile.Read(ref _lastScanDurationTicks);
        if (ticks >= 0)
            yield return new Measurement<double>(TimeSpan.FromTicks(ticks).TotalSeconds, _tenantTags);
    }

    private IEnumerable<Measurement<int>> ObserveLastScanSuccess()
    {
        if (_snapshot.LastScanSuccess is { } success)
            yield return new Measurement<int>(success ? 1 : 0, _tenantTags);
    }

    private IEnumerable<Measurement<int>> ObserveCredentials() =>
        _snapshot.Credentials.Select(c => new Measurement<int>(
            c.DaysLeft,
            new KeyValuePair<string, object?>("app_name", c.AppName),
            new KeyValuePair<string, object?>("app_id", c.AppId),
            new KeyValuePair<string, object?>("key_id", c.KeyId),
            new KeyValuePair<string, object?>("type", c.Type.ToString().ToLowerInvariant()),
            new KeyValuePair<string, object?>("credential_name", c.CredentialName),
            _tenantTags[0],
            _tenantTags[1]));

    public void Dispose() => Meter.Dispose();
}
