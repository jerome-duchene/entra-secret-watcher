using System.Diagnostics;
using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Services;
using EntraSecretWatcher.Services.Notifications;
using Microsoft.Extensions.Options;

namespace EntraSecretWatcher.Jobs;

internal class CredentialScanJob(
    GraphCredentialScanner scanner,
    NotificationDispatcher dispatcher,
    IOptions<WatcherOptions> options,
    ILogger<CredentialScanJob> logger)
{
    public const string JobId = "credential-scan";

    private readonly GraphCredentialScanner _scanner = scanner;
    private readonly NotificationDispatcher _dispatcher = dispatcher;
    private readonly WatcherOptions _options = options.Value;
    private readonly ILogger<CredentialScanJob> _logger = logger;

    private static readonly ActivitySource ActivitySource = new("EntraSecretWatcher.Jobs");

    public async Task ExecuteAsync()
    {
        using var activity = ActivitySource.StartActivity("CredentialScanJob.Execute");

        _logger.LogInformation("Credential scan job started (DryRun: {DryRun})", _options.DryRun);

        try
        {
            var result = await _scanner.ScanAsync();

            activity?.SetTag("scan.credentials_found", result.Credentials.Count);
            activity?.SetTag("scan.apps_scanned", result.TotalApplicationsScanned);

            if (!result.HasExpiring)
            {
                _logger.LogInformation("No expiring credentials found. All clear! ✅");
                return;
            }

            _logger.LogWarning(
                "Found {Count} expiring credential(s): {Expired} expired, {ExpiringSoon} expiring soon",
                result.Credentials.Count, result.ExpiredCount, result.ExpiringSoonCount);

            if (_options.DryRun)
            {
                _logger.LogInformation("[DRY-RUN] Would send notifications for {Count} credential(s). Skipping.",
                    result.Credentials.Count);
                foreach (var cred in result.Credentials)
                {
                    _logger.LogInformation("[DRY-RUN] {AppName} — {Type} '{CredName}' — {Status}",
                        cred.AppName, cred.Type, cred.CredentialName, cred.StatusLabel);
                }
                return;
            }

            await _dispatcher.DispatchAsync(result);

            _logger.LogInformation("Credential scan job completed successfully");
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Credential scan job failed");
            throw; // Let Hangfire handle retry
        }
    }
}
