using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Endpoints;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace EntraSecretWatcher.Tests.Endpoints;

public class ScanEndpointsTests
{
    private const string Secret = "0123456789abcdef-secret";

    private static readonly WatcherOptions Options = new() { ManualTriggerSecret = Secret };

    [Fact]
    public void Trigger_ValidToken_StartsScanAndReturnsAccepted()
    {
        var started = 0;

        var result = ScanEndpoints.Trigger(Secret, Options, () => started++);

        result.Should().BeOfType<Accepted>();
        started.Should().Be(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong-token")]
    [InlineData("0123456789abcdef-secre")]
    public void Trigger_MissingOrWrongToken_ReturnsUnauthorizedAndStartsNothing(string? token)
    {
        var started = 0;

        var result = ScanEndpoints.Trigger(token, Options, () => started++);

        result.Should().BeOfType<UnauthorizedHttpResult>();
        started.Should().Be(0);
    }

    [Fact]
    public void Trigger_NoSecretConfigured_NeverAuthorizes()
    {
        var started = 0;

        var result = ScanEndpoints.Trigger("", new WatcherOptions(), () => started++);

        result.Should().BeOfType<UnauthorizedHttpResult>();
        started.Should().Be(0);
    }
}
