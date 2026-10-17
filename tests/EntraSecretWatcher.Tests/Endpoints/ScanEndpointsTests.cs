using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Endpoints;
using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;

namespace EntraSecretWatcher.Tests.Endpoints;

public class ScanEndpointsTests
{
    private const string Secret = "0123456789abcdef-secret";

    private static readonly WatcherOptions Options = new() { ManualTriggerSecret = Secret };

    [Fact]
    public void Trigger_ValidToken_EnqueuesScanAndReturnsAccepted()
    {
        var jobs = Substitute.For<IBackgroundJobClient>();
        jobs.Create(Arg.Any<Job>(), Arg.Any<IState>()).Returns("42");

        var result = ScanEndpoints.Trigger(Secret, Options, jobs);

        var accepted = result.Should().BeOfType<Accepted<ScanAccepted>>().Subject;
        accepted.Location.Should().Be("/hangfire/jobs/details/42");
        accepted.Value!.JobId.Should().Be("42");
        jobs.Received(1).Create(
            Arg.Is<Job>(j => j.Method.Name == "ExecuteAsync"),
            Arg.Any<IState>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong-token")]
    [InlineData("0123456789abcdef-secre")]
    public void Trigger_MissingOrWrongToken_ReturnsUnauthorizedAndEnqueuesNothing(string? token)
    {
        var jobs = Substitute.For<IBackgroundJobClient>();

        var result = ScanEndpoints.Trigger(token, Options, jobs);

        result.Should().BeOfType<UnauthorizedHttpResult>();
        jobs.DidNotReceive().Create(Arg.Any<Job>(), Arg.Any<IState>());
    }

    [Fact]
    public void Trigger_NoSecretConfigured_NeverAuthorizes()
    {
        var jobs = Substitute.For<IBackgroundJobClient>();

        var result = ScanEndpoints.Trigger("", new WatcherOptions(), jobs);

        result.Should().BeOfType<UnauthorizedHttpResult>();
        jobs.DidNotReceive().Create(Arg.Any<Job>(), Arg.Any<IState>());
    }
}
