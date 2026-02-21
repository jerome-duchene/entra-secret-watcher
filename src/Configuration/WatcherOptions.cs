using System.ComponentModel.DataAnnotations;

namespace EntraSecretWatcher.Configuration;

internal record WatcherOptions
{
    public const string SectionName = "Watcher";

    /// <summary>
    /// Number of days before expiration to trigger a warning.
    /// </summary>
    [Range(1, 365, ErrorMessage = "ThresholdDays must be between 1 and 365.")]
    public int ThresholdDays { get; init; } = 30;

    /// <summary>
    /// Cron expression for the scan schedule (Hangfire format).
    /// Default: daily at 8:00 AM.
    /// </summary>
    [Required(ErrorMessage = "CronSchedule is required.")]
    public string CronSchedule { get; init; } = "0 8 * * *";

    /// <summary>
    /// Dry-run mode: scan and log but don't send notifications.
    /// </summary>
    public bool DryRun { get; init; } = false;

    /// <summary>
    /// Send a single grouped notification instead of one per credential.
    /// </summary>
    public bool GroupedReport { get; init; } = true;
}
