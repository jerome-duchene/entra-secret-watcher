namespace EntraSecretWatcher.Services;

/// <summary>
/// <paramref name="Retries"/> extra attempts after the first one, waiting <paramref name="Initial"/>,
/// 2 × <paramref name="Initial"/>, ... capped at <paramref name="Max"/>.
/// </summary>
internal sealed record RetryPolicy(int Retries, TimeSpan Initial, TimeSpan Max)
{
    /// <summary>Connection errors on a single Graph call: ~2 + 4 + 8 + 16 seconds.</summary>
    public static RetryPolicy Graph { get; } = new(4, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30));

    /// <summary>Startup scan: the network (DNS) may need a few minutes to come up, ~5 + 10 + 20 + 40 + 60 × 4 seconds.</summary>
    public static RetryPolicy Startup { get; } = new(8, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(60));

    public TimeSpan Delay(int attempt) =>
        attempt >= 30 ? Max : TimeSpan.FromTicks(Math.Min(Initial.Ticks * (1L << attempt), Max.Ticks));
}

/// <summary>Exponential backoff for transient failures (DNS not ready yet at container start, network blip, ...).</summary>
internal static class Retry
{
    /// <summary>Runs <paramref name="operation"/>, retrying while <paramref name="isTransient"/> accepts the exception.</summary>
    public static async Task<T> ExecuteAsync<T>(
        string what,
        RetryPolicy policy,
        Func<Exception, bool> isTransient,
        Func<Task<T>> operation,
        ILogger? logger,
        CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < policy.Retries && !ct.IsCancellationRequested && isTransient(ex))
            {
                var delay = policy.Delay(attempt);
                logger?.LogWarning("{What} failed ({Error}); retry {Attempt}/{Retries} in {Delay}",
                    what, ex.Message, attempt + 1, policy.Retries, delay);
                await Task.Delay(delay, ct);
            }
        }
    }

    /// <summary>
    /// DNS resolution failure or refused/unreachable connection: the request never reached the server,
    /// so retrying is safe even for a non-idempotent call such as sendMail.
    /// </summary>
    public static bool IsConnectionError(Exception ex) =>
        ex is HttpRequestException { HttpRequestError: HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError };
}
