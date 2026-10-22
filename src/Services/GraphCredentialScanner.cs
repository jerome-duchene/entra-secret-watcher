using System.Diagnostics;
using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Models;
using Microsoft.Extensions.Options;

namespace EntraSecretWatcher.Services;

internal class GraphCredentialScanner(
    GraphClient graph,
    IOptions<EntraOptions> entraOptions,
    IOptions<WatcherOptions> watcherOptions,
    ILogger<GraphCredentialScanner> logger)
{
    private readonly EntraOptions _entraOptions = entraOptions.Value;
    private readonly WatcherOptions _watcherOptions = watcherOptions.Value;
    private readonly ILogger<GraphCredentialScanner> _logger = logger;

    private static readonly ActivitySource ActivitySource = new("EntraSecretWatcher.Scanner");

    public virtual async Task<ScanResult> ScanAsync(CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("ScanCredentials");
        activity?.SetTag("tenant.id", _entraOptions.TenantId);
        activity?.SetTag("tenant.name", _entraOptions.TenantName);

        _logger.LogInformation("Starting credential scan for tenant {TenantName} ({TenantId})",
            _entraOptions.TenantName, _entraOptions.TenantId);

        var expiring = new List<ExpiringCredential>();
        var totalApps = 0;

        await foreach (var app in graph.GetApplicationsAsync(ct))
        {
            totalApps++;
            ProcessApplication(app, expiring);
        }

        _logger.LogInformation(
            "Scan complete for {TenantName}: {Total} apps scanned, {Expiring} credential(s) expiring within {Threshold} days",
            _entraOptions.TenantName, totalApps, expiring.Count, _watcherOptions.ThresholdDays);

        activity?.SetTag("apps.total", totalApps);
        activity?.SetTag("credentials.expiring", expiring.Count);

        return new ScanResult
        {
            TenantName = _entraOptions.TenantName,
            TenantId = _entraOptions.TenantId,
            ScannedAt = DateTimeOffset.UtcNow,
            Credentials = [.. expiring.OrderBy(c => c.DaysLeft)],
            TotalApplicationsScanned = totalApps
        };
    }

    private void ProcessApplication(GraphApplication app, List<ExpiringCredential> expiring)
    {
        foreach (var (type, credentials) in new[]
        {
            (CredentialType.Secret, app.PasswordCredentials),
            (CredentialType.Certificate, app.KeyCredentials)
        })
        {
            foreach (var credential in credentials ?? [])
            {
                if (credential.EndDateTime is not { } endDate)
                    continue;

                var daysLeft = (endDate - DateTimeOffset.UtcNow).Days;
                if (daysLeft > _watcherOptions.ThresholdDays)
                    continue;

                expiring.Add(new ExpiringCredential
                {
                    AppName = app.DisplayName ?? "Unknown",
                    AppId = app.AppId ?? "Unknown",
                    KeyId = credential.KeyId?.ToString() ?? string.Empty,
                    Type = type,
                    CredentialName = credential.DisplayName ?? "Unnamed",
                    ExpiresOn = endDate,
                    DaysLeft = daysLeft
                });
            }
        }
    }
}
