using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Jobs;
using EntraSecretWatcher.Services;
using EntraSecretWatcher.Services.Notifications;
using EntraSecretWatcher.Services.Notifications.Channels;
using Hangfire;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace EntraSecretWatcher.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEntraSecretWatcher(this IServiceCollection services)
    {
        // Configuration binding with validation
        services.AddOptions<WatcherOptions>()
            .BindConfiguration(WatcherOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<EntraOptions>()
            .BindConfiguration(EntraOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<NotificationOptions>()
            .BindConfiguration(NotificationOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Core services
        services.AddSingleton<ScanMetrics>();
        services.AddScoped<GraphCredentialScanner>();
        services.AddScoped<NotificationDispatcher>();
        services.AddScoped<CredentialScanJob>();

        // Notification channels
        services.AddScoped<INotificationChannel, GotifyNotificationChannel>();
        services.AddScoped<INotificationChannel, EmailNotificationChannel>();
        services.AddScoped<INotificationChannel, TeamsNotificationChannel>();
        services.AddScoped<INotificationChannel, WebhookNotificationChannel>();

        // HTTP client
        services.AddHttpClient();

        return services;
    }

    /// <summary>
    /// Pushes traces, metrics and logs over OTLP when <c>OpenTelemetry:Enabled</c> is true.
    /// The Prometheus <c>/metrics</c> endpoint is independent and always available.
    /// </summary>
    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OpenTelemetryOptions>()
            .BindConfiguration(OpenTelemetryOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var otel = configuration.GetSection(OpenTelemetryOptions.SectionName).Get<OpenTelemetryOptions>()
            ?? new OpenTelemetryOptions();
        if (!otel.Enabled)
            return services;

        var tenantName = configuration[$"{EntraOptions.SectionName}:{nameof(EntraOptions.TenantName)}"];
        var tenantId = configuration[$"{EntraOptions.SectionName}:{nameof(EntraOptions.TenantId)}"];

        void ConfigureExporter(OtlpExporterOptions exporter, string signal)
        {
            exporter.Protocol = otel.Protocol == OtlpTransport.HttpProtobuf
                ? OtlpExportProtocol.HttpProtobuf
                : OtlpExportProtocol.Grpc;

            if (string.IsNullOrWhiteSpace(otel.Endpoint))
                return; // standard OTEL_EXPORTER_OTLP_* variables / defaults apply

            // With HTTP/protobuf the SDK does not append the signal path to an endpoint set in code
            exporter.Endpoint = otel.Protocol == OtlpTransport.HttpProtobuf
                ? new Uri($"{otel.Endpoint.TrimEnd('/')}/v1/{signal}")
                : new Uri(otel.Endpoint);
        }

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                // One container per tenant: the tenant id makes each instance distinct (service.instance.id)
                .AddService(otel.ServiceName, serviceInstanceId: string.IsNullOrWhiteSpace(tenantId) ? null : tenantId)
                .AddAttributes(
                [
                    new KeyValuePair<string, object>("tenant.name", string.IsNullOrWhiteSpace(tenantName) ? "Default" : tenantName),
                    new KeyValuePair<string, object>("tenant.id", tenantId ?? string.Empty)
                ]))
            .WithTracing(tracing => tracing
                .AddSource("EntraSecretWatcher.Jobs", "EntraSecretWatcher.Scanner")
                .AddAspNetCoreInstrumentation(o => o.Filter = IsBusinessRequest)
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(o => ConfigureExporter(o, "traces")))
            .WithMetrics(metrics => metrics
                .AddMeter(ScanMetrics.MeterName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(o => ConfigureExporter(o, "metrics")))
            .WithLogging(logging => logging
                .AddOtlpExporter(o => ConfigureExporter(o, "logs")));

        return services;
    }

    // Do not trace the probes (health checks and Prometheus scrapes)
    private static bool IsBusinessRequest(HttpContext context) =>
        !context.Request.Path.StartsWithSegments("/metrics") &&
        !context.Request.Path.StartsWithSegments("/health");

    public static IServiceCollection AddHangfireServices(this IServiceCollection services)
    {
        services.AddHangfire(config =>
        {
            config.UseInMemoryStorage();
            config.UseSimpleAssemblyNameTypeSerializer();
            config.UseRecommendedSerializerSettings();
        });

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = 1;
        });

        return services;
    }
}
