using EntraSecretWatcher.Services;

namespace EntraSecretWatcher.Tests.Services;

public class RetryTests
{
    private static readonly RetryPolicy Fast = new(3, TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(4));

    [Fact]
    public void Delay_doubles_and_is_capped()
    {
        var policy = new RetryPolicy(10, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(60));

        Enumerable.Range(0, 6).Select(a => policy.Delay(a).TotalSeconds)
            .Should().Equal(5, 10, 20, 40, 60, 60);
        policy.Delay(500).Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task Retries_until_success()
    {
        var calls = 0;
        var result = await Retry.ExecuteAsync("op", Fast, _ => true,
            () => ++calls < 3 ? throw new InvalidOperationException("down") : Task.FromResult(calls), null, default);

        result.Should().Be(3);
    }

    [Fact]
    public async Task Gives_up_after_the_configured_retries()
    {
        var calls = 0;
        var act = () => Retry.ExecuteAsync<int>("op", Fast, _ => true,
            () => { calls++; throw new InvalidOperationException("down"); }, null, default);

        await act.Should().ThrowAsync<InvalidOperationException>();
        calls.Should().Be(4); // first attempt + 3 retries
    }

    [Fact]
    public async Task Non_transient_errors_are_not_retried()
    {
        var calls = 0;
        var act = () => Retry.ExecuteAsync<int>("op", Fast, ex => ex.Message != "fatal",
            () => { calls++; throw new InvalidOperationException("fatal"); }, null, default);

        await act.Should().ThrowAsync<InvalidOperationException>();
        calls.Should().Be(1);
    }

    [Theory]
    [InlineData(HttpRequestError.NameResolutionError, true)]
    [InlineData(HttpRequestError.ConnectionError, true)]
    [InlineData(HttpRequestError.Unknown, false)]
    [InlineData(HttpRequestError.ResponseEnded, false)]
    public void Only_connection_errors_are_transient(HttpRequestError error, bool expected) =>
        Retry.IsConnectionError(new HttpRequestException(error, "x")).Should().Be(expected);

    [Fact]
    public void Timeouts_are_not_transient() =>
        Retry.IsConnectionError(new TaskCanceledException()).Should().BeFalse();
}
