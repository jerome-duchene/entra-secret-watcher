using Azure.Identity;
using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Models;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;
using System.Text;

namespace EntraSecretWatcher.Services.Notifications.Channels;

internal class EmailNotificationChannel(
    IOptions<NotificationOptions> notificationOptions,
    IOptions<EntraOptions> entraOptions,
    ILogger<EmailNotificationChannel> logger) 
    : INotificationChannel
{
    private readonly EmailOptions? _emailOptions = notificationOptions.Value.Email;
    private readonly EntraOptions _entraOptions = entraOptions.Value;
    private readonly ILogger<EmailNotificationChannel> _logger = logger;

    public string Name => "Email (Graph API)";
    public bool IsEnabled => _emailOptions?.Enabled == true;

    public async Task SendAsync(ScanResult result, CancellationToken ct = default)
    {
        if (_emailOptions is null) return;

        var credential = new ClientSecretCredential(
            _entraOptions.TenantId,
            _entraOptions.ClientId,
            _entraOptions.ClientSecret);

        var graphClient = new GraphServiceClient(credential);
        var htmlBody = BuildHtml(result);

        var recipients = _emailOptions.To
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(email => new Recipient
            {
                EmailAddress = new EmailAddress { Address = email }
            })
            .ToList();

        var message = new Message
        {
            Subject = $"🔐 Entra ID Credential Report — {result.TenantName} ({result.Credentials.Count} expiring)",
            Body = new ItemBody
            {
                ContentType = BodyType.Html,
                Content = htmlBody
            },
            ToRecipients = recipients
        };

        var sendMailBody = new SendMailPostRequestBody
        {
            Message = message,
            SaveToSentItems = false
        };

        await graphClient.Users[_emailOptions.From].SendMail
            .PostAsync(sendMailBody, cancellationToken: ct);

        _logger.LogDebug("Email notification sent to {Recipients}", _emailOptions.To);
    }

    internal static string BuildHtml(ScanResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""
            <div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 700px; margin: 0 auto;">
                <h2 style="color: #1a1a1a; border-bottom: 2px solid #e74c3c; padding-bottom: 8px;">
                    🔐 Entra ID Credential Report
                </h2>
            """);

        sb.AppendLine($"""
                <p style="color: #555;">
                    <strong>Tenant:</strong> {result.TenantName}<br/>
                    <strong>Scanned:</strong> {result.ScannedAt:yyyy-MM-dd HH:mm} UTC<br/>
                    <strong>Applications scanned:</strong> {result.TotalApplicationsScanned}
                </p>
            """);

        if (result.ExpiredCount > 0)
        {
            sb.AppendLine($"""
                <div style="background: #fdecea; border-left: 4px solid #e74c3c; padding: 10px; margin: 10px 0;">
                    ⚠️ <strong>{result.ExpiredCount}</strong> credential(s) already expired!
                </div>
            """);
        }

        sb.AppendLine("""
                <table style="width: 100%; border-collapse: collapse; margin-top: 15px;">
                    <thead>
                        <tr style="background: #f5f5f5;">
                            <th style="padding: 8px; text-align: left; border: 1px solid #ddd;">Application</th>
                            <th style="padding: 8px; text-align: left; border: 1px solid #ddd;">Type</th>
                            <th style="padding: 8px; text-align: left; border: 1px solid #ddd;">Name</th>
                            <th style="padding: 8px; text-align: left; border: 1px solid #ddd;">Status</th>
                            <th style="padding: 8px; text-align: left; border: 1px solid #ddd;">Expires</th>
                        </tr>
                    </thead>
                    <tbody>
            """);

        foreach (var cred in result.Credentials)
        {
            var rowColor = cred.Status == CredentialStatus.Expired ? "#fdecea" : "#fff8e1";
            var statusColor = cred.Status == CredentialStatus.Expired ? "#e74c3c" : "#f39c12";

            sb.AppendLine($"""
                        <tr style="background: {rowColor};">
                            <td style="padding: 8px; border: 1px solid #ddd;">{cred.AppName}</td>
                            <td style="padding: 8px; border: 1px solid #ddd;">{cred.Type}</td>
                            <td style="padding: 8px; border: 1px solid #ddd;">{cred.CredentialName}</td>
                            <td style="padding: 8px; border: 1px solid #ddd; color: {statusColor}; font-weight: bold;">{cred.StatusLabel}</td>
                            <td style="padding: 8px; border: 1px solid #ddd;">{cred.ExpiresOn:yyyy-MM-dd}</td>
                        </tr>
                """);
        }

        sb.AppendLine("""
                    </tbody>
                </table>
                <p style="color: #999; font-size: 12px; margin-top: 20px;">
                    Generated by <strong>entra-secret-watcher</strong>
                </p>
            </div>
            """);

        return sb.ToString();
    }
}
