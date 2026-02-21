using EntraSecretWatcher.Models;
using FluentAssertions;

namespace EntraSecretWatcher.Tests.Models;

public class ExpiringCredentialTests
{
    // ── Status ────────────────────────────────────────────────────────────────

    [Fact]
    public void Status_WhenDaysLeftIsNegative_ReturnsExpired()
    {
        var cred = Credential(daysLeft: -1);
        cred.Status.Should().Be(CredentialStatus.Expired);
    }

    [Fact]
    public void Status_WhenDaysLeftIsZero_ReturnsExpiringSoon()
    {
        // 0 is NOT < 0, so it falls into the <= 30 branch
        var cred = Credential(daysLeft: 0);
        cred.Status.Should().Be(CredentialStatus.ExpiringSoon);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(30)]
    public void Status_WhenDaysLeftIsOneToThirty_ReturnsExpiringSoon(int days)
    {
        Credential(daysLeft: days).Status.Should().Be(CredentialStatus.ExpiringSoon);
    }

    [Fact]
    public void Status_WhenDaysLeftIsThirtyOne_ReturnsValid()
    {
        Credential(daysLeft: 31).Status.Should().Be(CredentialStatus.Valid);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(365)]
    public void Status_WhenDaysLeftIsAboveThirty_ReturnsValid(int days)
    {
        Credential(daysLeft: days).Status.Should().Be(CredentialStatus.Valid);
    }

    // ── StatusLabel ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(-1, "EXPIRED since 1 day(s)")]
    [InlineData(-5, "EXPIRED since 5 day(s)")]
    [InlineData(-30, "EXPIRED since 30 day(s)")]
    public void StatusLabel_WhenExpired_ShowsAbsoluteDays(int daysLeft, string expected)
    {
        Credential(daysLeft: daysLeft).StatusLabel.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, "Expires in 0 day(s)")]
    [InlineData(1, "Expires in 1 day(s)")]
    [InlineData(30, "Expires in 30 day(s)")]
    public void StatusLabel_WhenExpiringSoon_ShowsDaysRemaining(int daysLeft, string expected)
    {
        Credential(daysLeft: daysLeft).StatusLabel.Should().Be(expected);
    }

    [Fact]
    public void StatusLabel_WhenValid_ReturnsValid()
    {
        Credential(daysLeft: 31).StatusLabel.Should().Be("Valid");
    }

    // ── ScanResult aggregates ─────────────────────────────────────────────────

    [Fact]
    public void ScanResult_HasExpiring_IsFalse_WhenCredentialsIsEmpty()
    {
        var result = ScanResultWith([]);
        result.HasExpiring.Should().BeFalse();
    }

    [Fact]
    public void ScanResult_HasExpiring_IsTrue_WhenCredentialsIsNotEmpty()
    {
        var result = ScanResultWith([Credential(daysLeft: 5)]);
        result.HasExpiring.Should().BeTrue();
    }

    [Fact]
    public void ScanResult_Counts_AreCorrect_ForMixedCredentials()
    {
        var credentials = new List<ExpiringCredential>
        {
            Credential(daysLeft: -3),  // Expired
            Credential(daysLeft: -1),  // Expired
            Credential(daysLeft: 5),   // ExpiringSoon
            Credential(daysLeft: 20),  // ExpiringSoon
        };

        var result = ScanResultWith(credentials);

        result.ExpiredCount.Should().Be(2);
        result.ExpiringSoonCount.Should().Be(2);
    }

    [Fact]
    public void ScanResult_Counts_AreZero_WhenAllCredentialsAreValid()
    {
        // Note: ScanResult.Credentials only contains expiring/expired entries by design,
        // but the counts reflect what is in the list.
        var result = ScanResultWith([Credential(daysLeft: 100)]);

        result.ExpiredCount.Should().Be(0);
        result.ExpiringSoonCount.Should().Be(0);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ExpiringCredential Credential(int daysLeft) => new()
    {
        AppName = "Test App",
        AppId = "00000000-0000-0000-0000-000000000001",
        Type = CredentialType.Secret,
        CredentialName = "Test Secret",
        ExpiresOn = DateTimeOffset.UtcNow.AddDays(daysLeft),
        DaysLeft = daysLeft
    };

    private static ScanResult ScanResultWith(IEnumerable<ExpiringCredential> credentials) => new()
    {
        TenantName = "Test Tenant",
        TenantId = "test-tenant-id",
        ScannedAt = DateTimeOffset.UtcNow,
        Credentials = credentials.ToList().AsReadOnly(),
        TotalApplicationsScanned = 10
    };
}
