using System.ComponentModel.DataAnnotations;
using EntraSecretWatcher.Configuration;
using FluentAssertions;

namespace EntraSecretWatcher.Tests.Configuration;

public class WebhookOptionsValidationTests
{
    [Fact]
    public void Webhook_Disabled_IsValid_EvenWithEmptyUrl()
    {
        Validate(new WebhookOptions { Enabled = false, Url = "" }).Should().BeEmpty();
    }

    [Fact]
    public void Webhook_Enabled_EmptyUrl_ReturnsError()
    {
        Validate(new WebhookOptions { Enabled = true, Url = "" })
            .Should().ContainSingle(r => r.MemberNames.Contains("Webhook.Url"));
    }

    [Fact]
    public void Webhook_Enabled_InvalidUrl_ReturnsError()
    {
        Validate(new WebhookOptions { Enabled = true, Url = "ftp://host/x" })
            .Should().ContainSingle(r => r.MemberNames.Contains("Webhook.Url"));
    }

    [Fact]
    public void Webhook_Enabled_ValidUrl_IsValid()
    {
        Validate(new WebhookOptions { Enabled = true, Url = "https://example.com/hook" }).Should().BeEmpty();
    }

    [Fact]
    public void Webhook_SecretWithoutHeaderName_ReturnsError()
    {
        Validate(new WebhookOptions
        {
            Enabled = true, Url = "https://example.com/hook", Secret = "s", SecretHeaderName = ""
        }).Should().ContainSingle(r => r.MemberNames.Contains("Webhook.SecretHeaderName"));
    }

    private static List<ValidationResult> Validate(WebhookOptions webhook)
    {
        var options = new NotificationOptions { Webhook = webhook };
        return [.. options.Validate(new ValidationContext(options))];
    }
}
