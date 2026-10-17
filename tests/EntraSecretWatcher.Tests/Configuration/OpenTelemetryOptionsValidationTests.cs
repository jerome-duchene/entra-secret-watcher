using System.ComponentModel.DataAnnotations;
using EntraSecretWatcher.Configuration;
using FluentAssertions;

namespace EntraSecretWatcher.Tests.Configuration;

public class OpenTelemetryOptionsValidationTests
{
    [Fact]
    public void Disabled_IsValid_EvenWithInvalidEndpoint()
    {
        Validate(new OpenTelemetryOptions { Enabled = false, Endpoint = "not-a-url" }).Should().BeEmpty();
    }

    [Fact]
    public void Enabled_WithoutEndpoint_IsValid()
    {
        Validate(new OpenTelemetryOptions { Enabled = true }).Should().BeEmpty();
    }

    [Theory]
    [InlineData("http://alloy:4317")]
    [InlineData("https://otel.example.com:4318")]
    public void Enabled_WithHttpEndpoint_IsValid(string endpoint)
    {
        Validate(new OpenTelemetryOptions { Enabled = true, Endpoint = endpoint }).Should().BeEmpty();
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://alloy:4317")]
    public void Enabled_WithInvalidEndpoint_ReturnsError(string endpoint)
    {
        Validate(new OpenTelemetryOptions { Enabled = true, Endpoint = endpoint })
            .Should().ContainSingle(r => r.MemberNames.Contains(nameof(OpenTelemetryOptions.Endpoint)));
    }

    [Fact]
    public void Enabled_WithEmptyServiceName_ReturnsError()
    {
        Validate(new OpenTelemetryOptions { Enabled = true, ServiceName = " " })
            .Should().ContainSingle(r => r.MemberNames.Contains(nameof(OpenTelemetryOptions.ServiceName)));
    }

    private static List<ValidationResult> Validate(OpenTelemetryOptions options) =>
        [.. options.Validate(new ValidationContext(options))];
}
