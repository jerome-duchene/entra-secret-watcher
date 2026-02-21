using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Models;
using Microsoft.Extensions.Options;
using System.Text;

namespace EntraSecretWatcher.Services.Notifications.Channels;

internal class GotifyNotificationChannel(
    IOptions<NotificationOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<GotifyNotificationChannel> logger) 
    : INotificationChannel
{
    private readonly GotifyOptions? _options = options.Value.Gotify;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<GotifyNotificationChannel> _logger = logger;

    public string Name => "Gotify";
    public bool IsEnabled => _options?.Enabled == true;

    public async Task SendAsync(ScanResult result, CancellationToken ct = default)
    {
        if (_options is null) return;

        var client = _httpClientFactory.CreateClient("Gotify");

        var priority = ComputePriority(result);

        var message = BuildPlainText(result);

        var payload = new
        {
            title = $"🔐 Entra ID — {result.TenantName}: {result.Credentials.Count} credential(s) expiring",
            message,
            priority
        };

        var response = await client.PostAsJsonAsync(
            $"{_options.Url.TrimEnd('/')}/message?token={_options.Token}",
            payload, ct);

        response.EnsureSuccessStatusCode();

        _logger.LogDebug("Gotify notification sent with priority {Priority}", priority);
    }

    internal static int ComputePriority(ScanResult result) =>
        result.ExpiredCount > 0 ? 9 :
        result.ExpiringSoonCount > 0 ? 6 : 3;

    internal static string BuildPlainText(ScanResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"🔐 Entra ID Credential Report — {result.TenantName}");
        sb.AppendLine($"Scanned at: {result.ScannedAt:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine($"Applications scanned: {result.TotalApplicationsScanned}");
        sb.AppendLine($"Credentials expiring: {result.Credentials.Count} (Expired: {result.ExpiredCount}, Expiring soon: {result.ExpiringSoonCount})");
        sb.AppendLine(new string('─', 50));

        foreach (var cred in result.Credentials)
        {
            var icon = cred.Status == CredentialStatus.Expired ? "🔴" : "🟠";
            sb.AppendLine($"{icon} {cred.AppName}");
            sb.AppendLine($"   Type: {cred.Type} | Name: {cred.CredentialName}");
            sb.AppendLine($"   {cred.StatusLabel} (Expires: {cred.ExpiresOn:yyyy-MM-dd})");
            sb.AppendLine($"   AppId: {cred.AppId}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
