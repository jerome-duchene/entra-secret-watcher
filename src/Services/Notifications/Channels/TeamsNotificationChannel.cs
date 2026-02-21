using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Models;
using Microsoft.Extensions.Options;

namespace EntraSecretWatcher.Services.Notifications.Channels;

internal class TeamsNotificationChannel(
    IOptions<NotificationOptions> notificationOptions,
    IHttpClientFactory httpClientFactory,
    ILogger<TeamsNotificationChannel> logger) 
    : INotificationChannel
{
    private readonly TeamsOptions? _teamsOptions = notificationOptions.Value.Teams;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<TeamsNotificationChannel> _logger = logger;

    public string Name => "Teams (Webhook)";
    public bool IsEnabled => _teamsOptions?.Enabled == true;

    public async Task SendAsync(ScanResult result, CancellationToken ct = default)
    {
        if (_teamsOptions is null || string.IsNullOrEmpty(_teamsOptions.WebhookUrl)) return;

        var client = _httpClientFactory.CreateClient("Teams");
        var adaptiveCardPayload = BuildTeamsAdaptiveCard(result);

        var response = await client.PostAsync(
            _teamsOptions.WebhookUrl,
            new StringContent(adaptiveCardPayload, System.Text.Encoding.UTF8, "application/json"),
            ct);

        response.EnsureSuccessStatusCode();

        _logger.LogDebug("Teams webhook notification sent successfully");
    }

    private static string BuildTeamsAdaptiveCard(ScanResult result)
    {
        var credentialBlocks = string.Join(",\n", result.Credentials.Select(c =>
        {
            var icon = c.Status == CredentialStatus.Expired ? "🔴" : "🟠";
            var statusColor = c.Status == CredentialStatus.Expired ? "Attention" : "Warning";
            return $$"""
                {
                    "type": "Container",
                    "separator": true,
                    "items": [
                        {
                            "type": "TextBlock",
                            "text": "{{icon}} **{{EscapeJson(c.AppName)}}**",
                            "wrap": true
                        },
                        {
                            "type": "FactSet",
                            "facts": [
                                { "title": "Type", "value": "{{c.Type}}" },
                                { "title": "Name", "value": "{{EscapeJson(c.CredentialName)}}" },
                                { "title": "Expires", "value": "{{c.ExpiresOn:yyyy-MM-dd}}" },
                                { "title": "Status", "value": "{{c.StatusLabel}}" }
                            ]
                        }
                    ]
                }
            """;
        }));

        return $$"""
        {
            "type": "message",
            "attachments": [
                {
                    "contentType": "application/vnd.microsoft.card.adaptive",
                    "contentUrl": null,
                    "content": {
                        "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
                        "type": "AdaptiveCard",
                        "version": "1.4",
                        "msteams": {
                            "width": "Full"
                        },
                        "body": [
                            {
                                "type": "TextBlock",
                                "text": "🔐 Entra ID Credential Report",
                                "weight": "Bolder",
                                "size": "Large"
                            },
                            {
                                "type": "FactSet",
                                "facts": [
                                    { "title": "Tenant", "value": "{{EscapeJson(result.TenantName)}}" },
                                    { "title": "Scanned", "value": "{{result.ScannedAt:yyyy-MM-dd HH:mm}} UTC" },
                                    { "title": "Apps scanned", "value": "{{result.TotalApplicationsScanned}}" },
                                    { "title": "Expiring", "value": "{{result.ExpiringSoonCount}} ⚠️" },
                                    { "title": "Expired", "value": "{{result.ExpiredCount}} 🔴" }
                                ]
                            },
                            {{credentialBlocks}}
                        ]
                    }
                }
            ]
        }
        """;
    }

    internal static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");
    }
}
