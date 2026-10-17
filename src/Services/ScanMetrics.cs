using EntraSecretWatcher.Models;
using Prometheus;

namespace EntraSecretWatcher.Services;

/// <summary>
/// Prometheus metrics holding the outcome of the last scan.
/// Registered as a singleton: scans run rarely (cron) while Prometheus scrapes continuously,
/// so values must survive between two scans.
/// </summary>
internal class ScanMetrics
{
    private readonly List<string[]> _reportedLabels = [];
    private readonly Gauge _credentialDaysLeft;
    private readonly Gauge _expired;
    private readonly Gauge _expiringSoon;
    private readonly Gauge _appsScanned;
    private readonly Gauge _lastScanTimestamp;
    private readonly Gauge _lastScanSuccess;

    public ScanMetrics() : this(Metrics.DefaultRegistry)
    {
    }

    public ScanMetrics(CollectorRegistry registry)
    {
        var factory = Metrics.WithCustomRegistry(registry);

        _credentialDaysLeft = factory.CreateGauge(
            "esw_credential_days_left",
            "Days left before expiration of a credential reported by the last scan (negative = expired).",
            "app_name", "app_id", "type", "credential_name");
        _expired = factory.CreateGauge(
            "esw_credentials_expired",
            "Number of expired credentials found by the last scan.");
        _expiringSoon = factory.CreateGauge(
            "esw_credentials_expiring_soon",
            "Number of credentials about to expire found by the last scan.");
        _appsScanned = factory.CreateGauge(
            "esw_apps_scanned",
            "Number of app registrations inspected by the last scan.");
        _lastScanTimestamp = factory.CreateGauge(
            "esw_last_scan_timestamp_seconds",
            "Unix timestamp of the last scan attempt.");
        _lastScanSuccess = factory.CreateGauge(
            "esw_last_scan_success",
            "1 if the last scan succeeded, 0 otherwise.");
    }

    public void RecordSuccess(ScanResult result)
    {
        // Drop series of the previous scan: a renewed credential must disappear from the metrics
        foreach (var labels in _reportedLabels)
        {
            _credentialDaysLeft.RemoveLabelled(labels);
        }
        _reportedLabels.Clear();

        foreach (var cred in result.Credentials)
        {
            string[] labels = [cred.AppName, cred.AppId, cred.Type.ToString().ToLowerInvariant(), cred.CredentialName];
            _credentialDaysLeft.WithLabels(labels).Set(cred.DaysLeft);
            _reportedLabels.Add(labels);
        }

        _expired.Set(result.ExpiredCount);
        _expiringSoon.Set(result.ExpiringSoonCount);
        _appsScanned.Set(result.TotalApplicationsScanned);
        _lastScanTimestamp.Set(result.ScannedAt.ToUnixTimeSeconds());
        _lastScanSuccess.Set(1);
    }

    public void RecordFailure()
    {
        _lastScanTimestamp.Set(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        _lastScanSuccess.Set(0);
    }
}
