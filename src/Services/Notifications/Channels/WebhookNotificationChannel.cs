using System.Text.Json;
using System.Text.Json.Serialization;
using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Models;
using Microsoft.Extensions.Options;

namespace EntraSecretWatcher.Services.Notifications.Channels;

/// <summary>
/// Generic webhook: one JSON POST per expiring credential, so the receiver (Jira Automation,
/// n8n, Power Automate...) can deduplicate and track each credential on its own, using
/// <c>credential.keyId</c> as a stable identifier.
/// </summary>
internal class WebhookNotificationChannel(
    IOptions<NotificationOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<WebhookNotificationChannel> logger)
    : INotificationChannel
{
    internal const string EventName = "credential.expiring";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly WebhookOptions? _options = options.Value.Webhook;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<WebhookNotificationChannel> _logger = logger;

    public string Name => "Webhook";
    public bool IsEnabled => _options?.Enabled == true;

    public async Task SendAsync(ScanResult result, CancellationToken ct = default)
    {
        if (_options is null || string.IsNullOrWhiteSpace(_options.Url)) return;

        var client = _httpClientFactory.CreateClient("Webhook");
        var failures = new List<Exception>();

        foreach (var credential in result.Credentials)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _options.Url)
                {
                    Content = JsonContent.Create(BuildPayload(result, credential), options: JsonOptions)
                };

                if (!string.IsNullOrEmpty(_options.Secret))
                    request.Headers.TryAddWithoutValidation(_options.SecretHeaderName, _options.Secret);

                using var response = await client.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One failing credential must not prevent the others from being sent
                _logger.LogError(ex, "Webhook call failed for credential {KeyId} of {AppName}",
                    credential.KeyId, credential.AppName);
                failures.Add(ex);
            }
        }

        if (failures.Count > 0)
            throw new AggregateException(
                $"Webhook failed for {failures.Count} of {result.Credentials.Count} credential(s).", failures);

        _logger.LogDebug("Webhook called for {Count} credential(s)", result.Credentials.Count);
    }

    internal static object BuildPayload(ScanResult result, ExpiringCredential credential) => new
    {
        @event = EventName,
        tenantName = result.TenantName,
        tenantId = result.TenantId,
        scannedAt = result.ScannedAt,
        credential = new
        {
            keyId = credential.KeyId,
            appId = credential.AppId,
            appName = credential.AppName,
            type = credential.Type,
            name = credential.CredentialName,
            expiresOn = credential.ExpiresOn,
            daysLeft = credential.DaysLeft,
            status = credential.Status
        }
    };
}
