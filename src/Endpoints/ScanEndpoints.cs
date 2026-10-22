using System.Security.Cryptography;
using System.Text;
using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace EntraSecretWatcher.Endpoints;

internal static class ScanEndpoints
{
    public const string TokenHeader = "X-Scan-Token";

    /// <summary>
    /// Maps <c>POST /scan</c>, which starts a scan in the background. Only mapped when a secret is configured.
    /// </summary>
    public static IEndpointRouteBuilder MapManualScan(this IEndpointRouteBuilder app, WatcherOptions options)
    {
        if (string.IsNullOrEmpty(options.ManualTriggerSecret))
            return app;

        app.MapPost("/scan", ([FromHeader(Name = TokenHeader)] string? token, ScanScheduler scheduler) =>
            Trigger(token, options, scheduler.Trigger));

        return app;
    }

    internal static IResult Trigger(string? providedToken, WatcherOptions options, Action startScan)
    {
        if (!IsAuthorized(providedToken, options.ManualTriggerSecret))
            return Results.Unauthorized();

        startScan();

        return Results.Accepted();
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
