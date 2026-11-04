using System.Net;
using System.Net.Http.Json;
using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Services;
using Microsoft.Extensions.Options;

namespace EntraSecretWatcher.Tests.Services;

public class GraphClientRetryTests
{
    private static readonly RetryPolicy Fast = new(2, TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(2));

    private sealed class StubHandler(int failuresBeforeSuccess, Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (Calls++ < failuresBeforeSuccess)
                throw new HttpRequestException(HttpRequestError.NameResolutionError, "No such host is known");
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Ok(HttpRequestMessage request) =>
        request.RequestUri!.AbsolutePath.EndsWith("/token")
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { access_token = "tok" }) }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { value = Array.Empty<object>() }) };

    private static GraphClient Client(StubHandler handler) => new(
        new HttpClient(handler),
        Options.Create(new EntraOptions { TenantId = "t", ClientId = "c", ClientSecret = "s" }))
    { Backoff = Fast };

    [Fact]
    public async Task Dns_failures_are_retried_then_succeed()
    {
        var handler = new StubHandler(failuresBeforeSuccess: 2, Ok);

        var apps = await Client(handler).GetApplicationsAsync().ToListAsync();

        apps.Should().BeEmpty();
        handler.Calls.Should().Be(4); // token: 2 failures + success, then the applications call
    }

    [Fact]
    public async Task Connection_errors_are_reported_once_the_retries_are_exhausted()
    {
        var handler = new StubHandler(failuresBeforeSuccess: int.MaxValue, Ok);

        var act = () => Client(handler).GetApplicationsAsync().ToListAsync().AsTask();

        await act.Should().ThrowAsync<HttpRequestException>();
        handler.Calls.Should().Be(3); // first attempt + 2 retries
    }

    [Fact]
    public async Task Http_errors_are_not_retried()
    {
        var handler = new StubHandler(0, _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var act = () => Client(handler).GetApplicationsAsync().ToListAsync().AsTask();

        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("Token request failed: 503*");
        handler.Calls.Should().Be(1);
    }
}
