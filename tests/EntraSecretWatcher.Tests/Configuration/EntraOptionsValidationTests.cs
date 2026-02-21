using System.ComponentModel.DataAnnotations;
using EntraSecretWatcher.Configuration;
using FluentAssertions;

namespace EntraSecretWatcher.Tests.Configuration;

public class EntraOptionsValidationTests
{
    [Fact]
    public void FullyConfigured_IsValid()
    {
        Validate(ValidOptions()).Should().BeEmpty();
    }

    [Fact]
    public void TenantId_WhenNull_FailsRequired()
    {
        var errors = Validate(ValidOptions() with { TenantId = null! });
        errors.Should().ContainSingle(r => r.MemberNames.Contains(nameof(EntraOptions.TenantId)));
    }

    [Fact]
    public void TenantId_WhenEmpty_FailsRequired()
    {
        var errors = Validate(ValidOptions() with { TenantId = string.Empty });
        errors.Should().ContainSingle(r => r.MemberNames.Contains(nameof(EntraOptions.TenantId)));
    }

    [Fact]
    public void ClientId_WhenNull_FailsRequired()
    {
        var errors = Validate(ValidOptions() with { ClientId = null! });
        errors.Should().ContainSingle(r => r.MemberNames.Contains(nameof(EntraOptions.ClientId)));
    }

    [Fact]
    public void ClientSecret_WhenNull_FailsRequired()
    {
        var errors = Validate(ValidOptions() with { ClientSecret = null! });
        errors.Should().ContainSingle(r => r.MemberNames.Contains(nameof(EntraOptions.ClientSecret)));
    }

    [Fact]
    public void TenantName_UsesDefaultWhenNotSet()
    {
        var options = new EntraOptions { TenantId = "t", ClientId = "c", ClientSecret = "s" };
        options.TenantName.Should().Be("Default");
    }

    private static EntraOptions ValidOptions() => new()
    {
        TenantId = "00000000-0000-0000-0000-000000000001",
        ClientId = "00000000-0000-0000-0000-000000000002",
        ClientSecret = "super-secret"
    };

    private static IList<ValidationResult> Validate<T>(T instance)
    {
        var ctx = new ValidationContext(instance!);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance!, ctx, results, validateAllProperties: true);
        return results;
    }
}
