using System.ComponentModel.DataAnnotations;
using EntraSecretWatcher.Configuration;
using FluentAssertions;

namespace EntraSecretWatcher.Tests.Configuration;

public class NotificationOptionsValidationTests
{
    // ── NotificationOptions root ───────────────────────────────────────────────

    [Fact]
    public void NotificationOptions_WithNoChannels_IsValid()
    {
        Validate(new NotificationOptions()).Should().BeEmpty();
    }

    [Fact]
    public void NotificationOptions_WithNullChannels_IsValid()
    {
        var options = new NotificationOptions { Gotify = null, Email = null, Teams = null };
        Validate(options).Should().BeEmpty();
    }

    // ── GotifyOptions ──────────────────────────────────────────────────────────

    [Fact]
    public void Gotify_Disabled_IsValid_EvenWithEmptyFields()
    {
        var options = new NotificationOptions
        {
            Gotify = new GotifyOptions { Enabled = false, Url = "", Token = "" }
        };
        Validate(options).Should().BeEmpty();
    }

    [Fact]
    public void Gotify_Enabled_EmptyUrl_ReturnsError()
    {
        var options = new NotificationOptions
        {
            Gotify = new GotifyOptions { Enabled = true, Url = "", Token = "tok" }
        };
        Validate(options).Should().ContainSingle(r => r.MemberNames.Contains("Gotify.Url"));
    }

    [Fact]
    public void Gotify_Enabled_InvalidUrl_ReturnsError()
    {
        var options = new NotificationOptions
        {
            Gotify = new GotifyOptions { Enabled = true, Url = "not-a-url", Token = "tok" }
        };
        Validate(options).Should().ContainSingle(r => r.MemberNames.Contains("Gotify.Url"));
    }

    [Fact]
    public void Gotify_Enabled_FtpUrl_ReturnsError()
    {
        var options = new NotificationOptions
        {
            Gotify = new GotifyOptions { Enabled = true, Url = "ftp://gotify.local", Token = "tok" }
        };
        Validate(options).Should().ContainSingle(r => r.MemberNames.Contains("Gotify.Url"));
    }

    [Fact]
    public void Gotify_Enabled_EmptyToken_ReturnsError()
    {
        var options = new NotificationOptions
        {
            Gotify = new GotifyOptions { Enabled = true, Url = "https://gotify.example.com", Token = "" }
        };
        Validate(options).Should().ContainSingle(r => r.MemberNames.Contains("Gotify.Token"));
    }

    [Fact]
    public void Gotify_Enabled_ValidUrlAndToken_IsValid()
    {
        var options = new NotificationOptions
        {
            Gotify = new GotifyOptions
            {
                Enabled = true,
                Url = "https://gotify.example.com",
                Token = "my-token"
            }
        };
        Validate(options).Should().BeEmpty();
    }

    [Fact]
    public void Gotify_Enabled_HttpUrl_IsValid()
    {
        var options = new NotificationOptions
        {
            Gotify = new GotifyOptions
            {
                Enabled = true,
                Url = "http://gotify.local:8080",
                Token = "my-token"
            }
        };
        Validate(options).Should().BeEmpty();
    }

    // ── EmailOptions ───────────────────────────────────────────────────────────

    [Fact]
    public void Email_Disabled_IsValid_EvenWithEmptyFields()
    {
        var options = new NotificationOptions
        {
            Email = new EmailOptions { Enabled = false, From = "", To = "" }
        };
        Validate(options).Should().BeEmpty();
    }

    [Fact]
    public void Email_Enabled_EmptyFrom_ReturnsError()
    {
        var options = new NotificationOptions
        {
            Email = new EmailOptions { Enabled = true, From = "", To = "user@example.com" }
        };
        Validate(options).Should().ContainSingle(r => r.MemberNames.Contains("Email.From"));
    }

    [Fact]
    public void Email_Enabled_InvalidFromFormat_ReturnsError()
    {
        var options = new NotificationOptions
        {
            Email = new EmailOptions { Enabled = true, From = "not-an-email", To = "user@example.com" }
        };
        Validate(options).Should().ContainSingle(r => r.MemberNames.Contains("Email.From"));
    }

    [Fact]
    public void Email_Enabled_EmptyTo_ReturnsError()
    {
        var options = new NotificationOptions
        {
            Email = new EmailOptions { Enabled = true, From = "sender@example.com", To = "" }
        };
        Validate(options).Should().ContainSingle(r => r.MemberNames.Contains("Email.To"));
    }

    [Fact]
    public void Email_Enabled_ValidFields_IsValid()
    {
        var options = new NotificationOptions
        {
            Email = new EmailOptions
            {
                Enabled = true,
                From = "sender@example.com",
                To = "a@example.com,b@example.com"
            }
        };
        Validate(options).Should().BeEmpty();
    }

    // ── TeamsOptions ───────────────────────────────────────────────────────────

    [Fact]
    public void Teams_Disabled_IsValid_EvenWithEmptyWebhookUrl()
    {
        var options = new NotificationOptions
        {
            Teams = new TeamsOptions { Enabled = false, WebhookUrl = "" }
        };
        Validate(options).Should().BeEmpty();
    }

    [Fact]
    public void Teams_Enabled_EmptyWebhookUrl_ReturnsError()
    {
        var options = new NotificationOptions
        {
            Teams = new TeamsOptions { Enabled = true, WebhookUrl = "" }
        };
        Validate(options).Should().ContainSingle(r => r.MemberNames.Contains("Teams.WebhookUrl"));
    }

    [Fact]
    public void Teams_Enabled_InvalidWebhookUrl_ReturnsError()
    {
        var options = new NotificationOptions
        {
            Teams = new TeamsOptions { Enabled = true, WebhookUrl = "not-a-url" }
        };
        Validate(options).Should().ContainSingle(r => r.MemberNames.Contains("Teams.WebhookUrl"));
    }

    [Fact]
    public void Teams_Enabled_ValidWebhookUrl_IsValid()
    {
        var options = new NotificationOptions
        {
            Teams = new TeamsOptions
            {
                Enabled = true,
                WebhookUrl = "https://teams.webhook.office.com/webhookb2/abc"
            }
        };
        Validate(options).Should().BeEmpty();
    }

    // ── Error member name prefixing ────────────────────────────────────────────

    [Fact]
    public void ErrorMemberNames_ArePrefixedWithChannelName()
    {
        var options = new NotificationOptions
        {
            Gotify = new GotifyOptions { Enabled = true, Url = "", Token = "" }
        };
        var errors = Validate(options);

        errors.Should().Contain(r => r.MemberNames.Contains("Gotify.Url"));
        errors.Should().Contain(r => r.MemberNames.Contains("Gotify.Token"));
    }

    private static IList<ValidationResult> Validate<T>(T instance)
    {
        var ctx = new ValidationContext(instance!);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance!, ctx, results, validateAllProperties: true);
        return results;
    }
}
