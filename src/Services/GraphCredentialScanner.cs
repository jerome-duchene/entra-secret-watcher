using System.Diagnostics;
using Azure.Identity;
using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Models;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace EntraSecretWatcher.Services;

internal class GraphCredentialScanner(
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

        var credential = new ClientSecretCredential(
            _entraOptions.TenantId,
            _entraOptions.ClientId,
            _entraOptions.ClientSecret);

        var graphClient = new GraphServiceClient(credential);

        var expiring = new List<ExpiringCredential>();
        var totalApps = 0;

        var response = await graphClient.Applications.GetAsync(config =>
        {
            config.QueryParameters.Select = ["displayName", "appId", "passwordCredentials", "keyCredentials"];
            config.QueryParameters.Top = 999;
        }, ct);

        var pageIterator = PageIterator<Application, ApplicationCollectionResponse>
            .CreatePageIterator(graphClient, response!, app =>
            {
                totalApps++;
                ProcessApplication(app, expiring);
                return true;
            });

        await pageIterator.IterateAsync(ct);

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

    private void ProcessApplication(Application app, List<ExpiringCredential> expiring)
    {
        // Scan secrets
        if (app.PasswordCredentials is not null && app.PasswordCredentials.Count > 0)
        {
            foreach (var secret in app.PasswordCredentials)
            {
                if (secret.EndDateTime is null) 
                    continue;

                var daysLeft = (secret.EndDateTime.Value - DateTimeOffset.UtcNow).Days;
                if (daysLeft > _watcherOptions.ThresholdDays)
                    continue;
                
                expiring.Add(new ExpiringCredential
                {
                    AppName = app.DisplayName ?? "Unknown",
                    AppId = app.AppId ?? "Unknown",
                    Type = CredentialType.Secret,
                    CredentialName = secret.DisplayName ?? "Unnamed",
                    ExpiresOn = secret.EndDateTime.Value,
                    DaysLeft = daysLeft
                });
            }
        }

        // Scan certificates
        if (app.KeyCredentials is not null && app.KeyCredentials.Count > 0)
        {
            foreach (var cert in app.KeyCredentials)
            {
                if (cert.EndDateTime is null) continue;

                var daysLeft = (cert.EndDateTime.Value - DateTimeOffset.UtcNow).Days;
                if (daysLeft > _watcherOptions.ThresholdDays)
                    continue;

                expiring.Add(new ExpiringCredential
                {
                    AppName = app.DisplayName ?? "Unknown",
                    AppId = app.AppId ?? "Unknown",
                    Type = CredentialType.Certificate,
                    CredentialName = cert.DisplayName ?? "Unnamed",
                    ExpiresOn = cert.EndDateTime.Value,
                    DaysLeft = daysLeft
                });
            }
        }
    }
}
