using EntraSecretWatcher.Models;
using EntraSecretWatcher.Services.Notifications.Channels;
using FluentAssertions;

namespace EntraSecretWatcher.Tests.Services.Notifications.Channels;

public class GotifyNotificationChannelTests
{
    // ── ComputePriority ────────────────────────────────────────────────────────

    [Fact]
    public void ComputePriority_WithExpiredCredentials_Returns9()
    {
        var result = ScanResultWith(daysLeft: -1);
        GotifyNotificationChannel.ComputePriority(result).Should().Be(9);
    }

    [Fact]
    public void ComputePriority_WithOnlyExpiringSoon_Returns6()
    {
        var result = ScanResultWith(daysLeft: 10);
        GotifyNotificationChannel.ComputePriority(result).Should().Be(6);
    }

    [Fact]
    public void ComputePriority_ExpiredTakesPrecedenceOverExpiringSoon_Returns9()
    {
        var result = new ScanResult
        {
            TenantName = "T", TenantId = "id", ScannedAt = DateTimeOffset.UtcNow,
            TotalApplicationsScanned = 2,
            Credentials = new List<ExpiringCredential>
            {
                Credential(daysLeft: -2),
                Credential(daysLeft: 5)
            }.AsReadOnly()
        };
        GotifyNotificationChannel.ComputePriority(result).Should().Be(9);
    }

    [Fact]
    public void ComputePriority_EmptyCredentials_Returns3()
    {
        var result = new ScanResult
        {
            TenantName = "T", TenantId = "id", ScannedAt = DateTimeOffset.UtcNow,
            TotalApplicationsScanned = 0,
            Credentials = new List<ExpiringCredential>().AsReadOnly()
        };
        GotifyNotificationChannel.ComputePriority(result).Should().Be(3);
    }

    // ── BuildPlainText ─────────────────────────────────────────────────────────

    [Fact]
    public void BuildPlainText_ContainsTenantName()
    {
        var result = ScanResultWith(daysLeft: 5, tenantName: "Contoso");
        GotifyNotificationChannel.BuildPlainText(result).Should().Contain("Contoso");
    }

    [Fact]
    public void BuildPlainText_ContainsAppName()
    {
        var result = ScanResultWith(daysLeft: 5, appName: "My App");
        GotifyNotificationChannel.BuildPlainText(result).Should().Contain("My App");
    }

    [Fact]
    public void BuildPlainText_ContainsCredentialName()
    {
        var result = ScanResultWith(daysLeft: 5, credName: "prod-secret");
        GotifyNotificationChannel.BuildPlainText(result).Should().Contain("prod-secret");
    }

    [Fact]
    public void BuildPlainText_ExpiredCredential_ShowsRedCircleIcon()
    {
        var result = ScanResultWith(daysLeft: -1);
        GotifyNotificationChannel.BuildPlainText(result).Should().Contain("🔴");
    }

    [Fact]
    public void BuildPlainText_ExpiringSoonCredential_ShowsOrangeCircleIcon()
    {
        var result = ScanResultWith(daysLeft: 10);
        GotifyNotificationChannel.BuildPlainText(result).Should().Contain("🟠");
    }

    [Fact]
    public void BuildPlainText_ContainsExpirationDate()
    {
        var expiresOn = new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero);
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

        GotifyNotificationChannel.BuildPlainText(result).Should().Contain("2026-06-15");
    }

    [Fact]
    public void BuildPlainText_MultipleCredentials_ContainsAllAppNames()
    {
        var result = new ScanResult
        {
            TenantName = "T", TenantId = "id", ScannedAt = DateTimeOffset.UtcNow,
            TotalApplicationsScanned = 2,
            Credentials = new List<ExpiringCredential>
            {
                CredentialWith("Alpha App", "secret-a", daysLeft: -1),
                CredentialWith("Beta App", "secret-b", daysLeft: 5)
            }.AsReadOnly()
        };

        var text = GotifyNotificationChannel.BuildPlainText(result);
        text.Should().Contain("Alpha App");
        text.Should().Contain("Beta App");
    }

    [Fact]
    public void BuildPlainText_NoCredentials_ReturnsValidMessage()
    {
        var result = new ScanResult
        {
            TenantName = "T", TenantId = "id", ScannedAt = DateTimeOffset.UtcNow,
            TotalApplicationsScanned = 0,
            Credentials = new List<ExpiringCredential>().AsReadOnly()
        };

        GotifyNotificationChannel.BuildPlainText(result).Should().Contain("Entra ID Credential Report");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ExpiringCredential Credential(int daysLeft) =>
        CredentialWith("Test App", "Test Secret", daysLeft);

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
