using EntraSecretWatcher.Models;

namespace EntraSecretWatcher.Services.Notifications.Channels;

internal interface INotificationChannel
{
    string Name { get; }
    bool IsEnabled { get; }
    Task SendAsync(ScanResult result, CancellationToken ct = default);
}
