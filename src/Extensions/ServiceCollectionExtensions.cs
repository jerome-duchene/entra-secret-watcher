using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Jobs;
using EntraSecretWatcher.Services;
using EntraSecretWatcher.Services.Notifications;
using EntraSecretWatcher.Services.Notifications.Channels;
using Hangfire;

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
        services.AddScoped<GraphCredentialScanner>();
        services.AddScoped<NotificationDispatcher>();
        services.AddScoped<CredentialScanJob>();

        // Notification channels
        services.AddScoped<INotificationChannel, GotifyNotificationChannel>();
        services.AddScoped<INotificationChannel, EmailNotificationChannel>();
        services.AddScoped<INotificationChannel, TeamsNotificationChannel>();

        // HTTP client
        services.AddHttpClient();

        return services;
    }

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
