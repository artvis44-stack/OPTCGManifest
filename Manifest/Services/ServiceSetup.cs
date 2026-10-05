using Manifest.Data;
using Manifest.Services.Jobs;
using Manifest.Web;
using StackExchange.Redis;

namespace Manifest.Services;

/// <summary>
/// Everything the app is made of, registered once for both the web process and
/// `manifest worker` - so a job runs with the same services wherever it runs.
/// </summary>
public static class ServiceSetup
{
    public static IServiceCollection AddManifest(this IServiceCollection services, AppConfig config,
                                                 AppPaths paths, Database database, MailSettings mail)
    {
        services.AddSingleton(config);
        services.AddSingleton(paths);
        services.AddSingleton(database);
        services.AddSingleton<IClock>(SystemClock.Instance);
        services.AddSingleton<WebAssets>();

        if (config.RedisUrl is { } redisUrl)
        {
            services.AddSingleton<IConnectionMultiplexer>(
                ConnectionMultiplexer.Connect(RedisLoginThrottle.Options(redisUrl)));
            services.AddSingleton<ILoginThrottle, RedisLoginThrottle>();
        }
        else
        {
            services.AddSingleton<ILoginThrottle, MemoryLoginThrottle>();
        }

        services.AddSingleton<UserRepository>();
        services.AddSingleton<IUserRepository>(sp => sp.GetRequiredService<UserRepository>());
        services.AddSingleton<ISessionRepository>(sp => sp.GetRequiredService<UserRepository>());
        services.AddSingleton<AccessRepository>();
        services.AddSingleton<IAccessRepository>(sp => sp.GetRequiredService<AccessRepository>());
        services.AddSingleton<CardRepository>();
        services.AddSingleton<ICardRepository>(sp => sp.GetRequiredService<CardRepository>());
        services.AddSingleton<IScanRepository>(sp => sp.GetRequiredService<CardRepository>());
        services.AddSingleton<BinderRepository>();
        services.AddSingleton<DeckRepository>();
        services.AddSingleton<IDeckRepository>(sp => sp.GetRequiredService<DeckRepository>());

        services.AddSingleton(mail);
        services.AddSingleton<Mailer>();
        services.AddSingleton<Outbox>();
        services.AddSingleton<JobQueue>();
        services.AddSingleton<JobSchedule>();

        if (config.ObjectStorageConfigured)
            services.AddSingleton<IImageStore, S3ImageStore>();
        else
            services.AddSingleton<IImageStore, LocalImageStore>();
        services.AddSingleton<ImageCache>();
        services.AddSingleton<TesseractScanner>();
        services.AddSingleton<ApiScanner>();
        services.AddSingleton<ScanService>();

        // The job handlers. The two that are also services elsewhere are shared
        // instances, not second copies.
        services.AddSingleton<IJobHandler>(sp => sp.GetRequiredService<ImageCache>());
        services.AddSingleton<IJobHandler>(sp => sp.GetRequiredService<ScanService>());
        services.AddSingleton<IJobHandler, SendEmailHandler>();
        services.AddSingleton<IJobHandler, PurgeSessionsHandler>();
        services.AddSingleton<IJobHandler, PurgeAccessTokensHandler>();
        services.AddSingleton<IJobHandler, PurgeJobsHandler>();
        services.AddSingleton<IJobHandler, RefreshPricesHandler>();
        services.AddSingleton<IJobHandler, RefreshCatalogHandler>();
        services.AddSingleton<IJobHandler, ScrapeCatalogHandler>();
        return services;
    }
}
