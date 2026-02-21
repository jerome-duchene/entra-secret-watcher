using EntraSecretWatcher.Models;
using EntraSecretWatcher.Services.Notifications.Channels;

namespace EntraSecretWatcher.Services.Notifications;

internal class NotificationDispatcher(
    IEnumerable<INotificationChannel> channels,
    ILogger<NotificationDispatcher> logger)
{
    private readonly IEnumerable<INotificationChannel> _channels = channels;
    private readonly ILogger<NotificationDispatcher> _logger = logger;

    public virtual async Task DispatchAsync(ScanResult result, CancellationToken ct = default)
    {
        var enabledChannels = _channels.Where(c => c.IsEnabled).ToList();

        if (enabledChannels.Count == 0)
        {
            _logger.LogWarning("No notification channels are enabled. Skipping notification dispatch.");
            return;
        }

        foreach (var channel in enabledChannels)
        {
            try
            {
                _logger.LogInformation("Sending notification via {Channel}", channel.Name);
                await channel.SendAsync(result, ct);
                _logger.LogInformation("Notification sent successfully via {Channel}", channel.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification via {Channel}", channel.Name);
            }
        }
    }
}
