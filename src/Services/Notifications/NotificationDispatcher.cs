using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Models;
using EntraSecretWatcher.Services.Notifications.Channels;
using Microsoft.Extensions.Options;

namespace EntraSecretWatcher.Services.Notifications;

internal class NotificationDispatcher(
    IEnumerable<INotificationChannel> channels,
    IOptions<WatcherOptions> options,
    ILogger<NotificationDispatcher> logger)
{
    private readonly IEnumerable<INotificationChannel> _channels = channels;
    private readonly WatcherOptions _options = options.Value;
    private readonly ILogger<NotificationDispatcher> _logger = logger;

    public virtual async Task DispatchAsync(ScanResult result, CancellationToken ct = default)
    {
        var enabledChannels = _channels.Where(c => c.IsEnabled).ToList();

        if (enabledChannels.Count == 0)
        {
            _logger.LogWarning("No notification channels are enabled. Skipping notification dispatch.");
            return;
        }

        var batches = _options.GroupedReport
            ? [result]
            : result.Credentials.Select(cred => result with { Credentials = [cred] }).ToList();

        foreach (var batch in batches)
        {
            foreach (var channel in enabledChannels)
            {
                try
                {
                    _logger.LogInformation("Sending notification via {Channel}", channel.Name);
                    await channel.SendAsync(batch, ct);
                    _logger.LogInformation("Notification sent successfully via {Channel}", channel.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send notification via {Channel}", channel.Name);
                }
            }
        }
    }
}
