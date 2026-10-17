using System.ComponentModel.DataAnnotations;

namespace EntraSecretWatcher.Configuration;

internal enum OtlpTransport
{
    Grpc,
    HttpProtobuf
}

internal record OpenTelemetryOptions : IValidatableObject
{
    public const string SectionName = "OpenTelemetry";
    public const string DefaultServiceName = "entra-secret-watcher";

    /// <summary>
    /// Push traces, metrics and logs over OTLP (e.g. to Grafana Alloy or an OpenTelemetry Collector).
    /// </summary>
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// OTLP endpoint. When empty, the standard <c>OTEL_EXPORTER_OTLP_*</c> environment variables apply,
    /// falling back to <c>http://localhost:4317</c> (gRPC).
    /// With <see cref="OtlpTransport.HttpProtobuf"/>, give the base URL (e.g. <c>http://alloy:4318</c>):
    /// the <c>/v1/{traces,metrics,logs}</c> paths are appended automatically.
    /// </summary>
    public string Endpoint { get; init; } = string.Empty;

    public OtlpTransport Protocol { get; init; } = OtlpTransport.Grpc;

    public string ServiceName { get; init; } = DefaultServiceName;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enabled) yield break;

        if (!string.IsNullOrWhiteSpace(Endpoint) &&
            (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri) ||
             (uri.Scheme != "http" && uri.Scheme != "https")))
            yield return new ValidationResult(
                "OpenTelemetry:Endpoint must be a valid HTTP(S) URL.",
                [nameof(Endpoint)]);

        if (string.IsNullOrWhiteSpace(ServiceName))
            yield return new ValidationResult(
                "OpenTelemetry:ServiceName must not be empty.",
                [nameof(ServiceName)]);
    }
}
