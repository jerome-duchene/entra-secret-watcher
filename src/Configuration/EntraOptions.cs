using System.ComponentModel.DataAnnotations;

namespace EntraSecretWatcher.Configuration;

internal record EntraOptions
{
    public const string SectionName = "Entra";

    [Required(ErrorMessage = "Entra:TenantId is required.")]
    public string TenantId { get; init; } = default!;

    [Required(ErrorMessage = "Entra:ClientId is required.")]
    public string ClientId { get; init; } = default!;

    [Required(ErrorMessage = "Entra:ClientSecret is required.")]
    public string ClientSecret { get; init; } = default!;

    /// <summary>
    /// Friendly name for this tenant (used in notifications).
    /// </summary>
    public string TenantName { get; init; } = "Default";
}
