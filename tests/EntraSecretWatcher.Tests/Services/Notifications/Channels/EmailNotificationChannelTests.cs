using EntraSecretWatcher.Models;
using EntraSecretWatcher.Services.Notifications.Channels;
using FluentAssertions;

namespace EntraSecretWatcher.Tests.Services.Notifications.Channels;

public class EmailNotificationChannelTests
{
    // ── BuildHtml — contenu de base ────────────────────────────────────────────

    [Fact]
    public void BuildHtml_ContainsTenantName()
    {
        var result = ScanResultWith(daysLeft: 10, tenantName: "Contoso");
        EmailNotificationChannel.BuildHtml(result).Should().Contain("Contoso");
    }

    [Fact]
    public void BuildHtml_ContainsAppName()
    {
        var result = ScanResultWith(daysLeft: 10, appName: "My App");
        EmailNotificationChannel.BuildHtml(result).Should().Contain("My App");
    }

    [Fact]
    public void BuildHtml_ContainsCredentialName()
    {
        var result = ScanResultWith(daysLeft: 10, credName: "prod-secret");
        EmailNotificationChannel.BuildHtml(result).Should().Contain("prod-secret");
    }

    [Fact]
    public void BuildHtml_ContainsExpirationDate()
    {
        var expiresOn = new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);
        var cred = new ExpiringCredential
        {
            AppName = "App", AppId = "id", Type = CredentialType.Secret,
            CredentialName = "secret", ExpiresOn = expiresOn, DaysLeft = 10
        };
        var result = new ScanResult
        {
            TenantName = "T", TenantId = "id", ScannedAt = DateTimeOffset.UtcNow,
            TotalApplicationsScanned = 1, Credentials = new[] { cred }.ToList().AsReadOnly()
        };

        EmailNotificationChannel.BuildHtml(result).Should().Contain("2026-09-30");
    }

    [Fact]
    public void BuildHtml_ContainsStatusLabel()
    {
        var result = ScanResultWith(daysLeft: 5);
        EmailNotificationChannel.BuildHtml(result).Should().Contain("Expires in 5 day(s)");
    }

    // ── BuildHtml — bloc d'alerte expired ────────────────────────────────────

    [Fact]
    public void BuildHtml_WithExpiredCredential_ContainsWarningBlock()
    {
        var result = ScanResultWith(daysLeft: -2);
        EmailNotificationChannel.BuildHtml(result).Should().Contain("already expired");
    }

    [Fact]
    public void BuildHtml_WithoutExpiredCredential_DoesNotContainWarningBlock()
    {
        var result = ScanResultWith(daysLeft: 10);
        EmailNotificationChannel.BuildHtml(result).Should().NotContain("already expired");
    }

    // ── BuildHtml — couleurs par statut ───────────────────────────────────────

    [Fact]
    public void BuildHtml_ExpiredCredentialRow_HasRedBackground()
    {
        var result = ScanResultWith(daysLeft: -1);
        // Row color for expired = #fdecea
        EmailNotificationChannel.BuildHtml(result).Should().Contain("#fdecea");
    }

    [Fact]
    public void BuildHtml_ExpiringSoonCredentialRow_HasYellowBackground()
    {
        var result = ScanResultWith(daysLeft: 5);
        // Row color for expiring soon = #fff8e1
        EmailNotificationChannel.BuildHtml(result).Should().Contain("#fff8e1");
    }

    // ── BuildHtml — cas multiples ─────────────────────────────────────────────

    [Fact]
    public void BuildHtml_MultipleCredentials_ContainsAllAppNames()
    {
        var result = new ScanResult
        {
            TenantName = "T", TenantId = "id", ScannedAt = DateTimeOffset.UtcNow,
            TotalApplicationsScanned = 2,
            Credentials = new List<ExpiringCredential>
            {
                CredentialWith("Alpha App", "secret-a", daysLeft: -1),
                CredentialWith("Beta App",  "secret-b", daysLeft: 5)
            }.AsReadOnly()
        };

        var html = EmailNotificationChannel.BuildHtml(result);
        html.Should().Contain("Alpha App");
        html.Should().Contain("Beta App");
    }

    [Fact]
    public void BuildHtml_NoCredentials_ReturnsValidHtmlWithTable()
    {
        var result = new ScanResult
        {
            TenantName = "T", TenantId = "id", ScannedAt = DateTimeOffset.UtcNow,
            TotalApplicationsScanned = 0,
            Credentials = new List<ExpiringCredential>().AsReadOnly()
        };

        var html = EmailNotificationChannel.BuildHtml(result);
        html.Should().Contain("<table");
        html.Should().Contain("</table>");
    }

    [Fact]
    public void BuildHtml_ContainsTableHeaders()
    {
        var result = ScanResultWith(daysLeft: 5);
        var html = EmailNotificationChannel.BuildHtml(result);

        html.Should().Contain("Application");
        html.Should().Contain("Type");
        html.Should().Contain("Status");
        html.Should().Contain("Expires");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ExpiringCredential CredentialWith(string appName, string credName, int daysLeft) => new()
    {
        AppName = appName,
        AppId = "00000000-0000-0000-0000-000000000001",
        Type = CredentialType.Secret,
        CredentialName = credName,
        ExpiresOn = DateTimeOffset.UtcNow.AddDays(daysLeft),
        DaysLeft = daysLeft
    };

    private static ScanResult ScanResultWith(
        int daysLeft,
        string tenantName = "Test Tenant",
        string appName = "Test App",
        string credName = "Test Secret") => new()
    {
        TenantName = tenantName,
        TenantId = "test-id",
        ScannedAt = DateTimeOffset.UtcNow,
        TotalApplicationsScanned = 1,
        Credentials = new List<ExpiringCredential>
        {
            CredentialWith(appName, credName, daysLeft)
        }.AsReadOnly()
    };
}
