using System.Security.Cryptography;
using System.Text;
using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Jobs;
using Hangfire;
using Microsoft.AspNetCore.Mvc;

namespace EntraSecretWatcher.Endpoints;

internal static class ScanEndpoints
{
    public const string TokenHeader = "X-Scan-Token";

    /// <summary>
    /// Maps <c>POST /scan</c>, which enqueues a scan. Only mapped when a secret is configured:
    /// the Hangfire dashboard has no authentication, so triggering is kept out of it.
    /// </summary>
    public static IEndpointRouteBuilder MapManualScan(this IEndpointRouteBuilder app, WatcherOptions options)
    {
        if (string.IsNullOrEmpty(options.ManualTriggerSecret))
            return app;

        app.MapPost("/scan", ([FromHeader(Name = TokenHeader)] string? token, IBackgroundJobClient jobs) =>
            Trigger(token, options, jobs));

        return app;
    }

    internal static IResult Trigger(string? providedToken, WatcherOptions options, IBackgroundJobClient jobs)
    {
        if (!IsAuthorized(providedToken, options.ManualTriggerSecret))
            return Results.Unauthorized();

        var jobId = jobs.Enqueue<CredentialScanJob>(job => job.ExecuteAsync());

        return Results.Accepted($"/hangfire/jobs/details/{jobId}", new ScanAccepted(jobId));
    }

    private static bool IsAuthorized(string? provided, string expected)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(provided))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided),
            Encoding.UTF8.GetBytes(expected));
    }
}

internal sealed record ScanAccepted(string JobId);
