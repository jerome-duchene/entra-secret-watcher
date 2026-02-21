using System.ComponentModel.DataAnnotations;

namespace EntraSecretWatcher.Configuration;

internal record NotificationOptions : IValidatableObject
{
    public const string SectionName = "Notification";

    public GotifyOptions? Gotify { get; init; }
    public EmailOptions? Email { get; init; }
    public TeamsOptions? Teams { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Gotify is not null)
            foreach (var result in ValidateNested(Gotify, nameof(Gotify)))
                yield return result;

        if (Email is not null)
            foreach (var result in ValidateNested(Email, nameof(Email)))
                yield return result;

        if (Teams is not null)
            foreach (var result in ValidateNested(Teams, nameof(Teams)))
                yield return result;
    }

    private static IEnumerable<ValidationResult> ValidateNested(object instance, string prefix)
    {
        var context = new ValidationContext(instance);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, context, results, validateAllProperties: true);
        return results.Select(r => new ValidationResult(
            r.ErrorMessage,
            [.. r.MemberNames.Select(m => $"{prefix}.{m}")]));
    }
}

internal record GotifyOptions : IValidatableObject
{
    public bool Enabled { get; init; } = false;
    public string Url { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enabled) yield break;

        if (string.IsNullOrWhiteSpace(Url))
            yield return new ValidationResult(
                "Gotify:Url is required when Gotify is enabled.",
                [nameof(Url)]);
        else if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) ||
                 (uri.Scheme != "http" && uri.Scheme != "https"))
            yield return new ValidationResult(
                "Gotify:Url must be a valid HTTP(S) URL.",
                [nameof(Url)]);

        if (string.IsNullOrWhiteSpace(Token))
            yield return new ValidationResult(
                "Gotify:Token is required when Gotify is enabled.",
                [nameof(Token)]);
    }
}

internal record EmailOptions : IValidatableObject
{
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// Sender address. Must be a valid mailbox or shared mailbox in the tenant.
    /// Uses Graph API (Mail.Send permission) to send.
    /// </summary>
    public string From { get; init; } = string.Empty;

    /// <summary>
    /// Comma-separated list of recipient email addresses.
    /// </summary>
    public string To { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enabled) yield break;

        if (string.IsNullOrWhiteSpace(From))
            yield return new ValidationResult(
                "Email:From is required when Email is enabled.",
                [nameof(From)]);
        else if (!new EmailAddressAttribute().IsValid(From))
            yield return new ValidationResult(
                "Email:From must be a valid email address.",
                [nameof(From)]);

        if (string.IsNullOrWhiteSpace(To))
            yield return new ValidationResult(
                "Email:To is required when Email is enabled.",
                [nameof(To)]);
    }
}

internal record TeamsOptions : IValidatableObject
{
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// Teams Incoming Webhook URL.
    /// See README for setup instructions.
    /// </summary>
    public string WebhookUrl { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enabled) yield break;

        if (string.IsNullOrWhiteSpace(WebhookUrl))
            yield return new ValidationResult(
                "Teams:WebhookUrl is required when Teams is enabled.",
                [nameof(WebhookUrl)]);
        else if (!Uri.TryCreate(WebhookUrl, UriKind.Absolute, out var uri) ||
                 (uri.Scheme != "http" && uri.Scheme != "https"))
            yield return new ValidationResult(
                "Teams:WebhookUrl must be a valid HTTP(S) URL.",
                [nameof(WebhookUrl)]);
    }
}
