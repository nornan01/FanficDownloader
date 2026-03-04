using FanficDownloader.Application.Services;
using FanficDownloader.Core.Sources;
using FanficDownloader.Core.Parsers;
using FanficDownloader.Core.Formatting;
using FanficDownloader.Core.Clients;
using FanficDownloader.Application.Configuration;
using FanficDownloader.Web.Services;
using FanficDownloader.Bot.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FanficDownloader.Web;

public static class DependencyInjection
{
    public static IServiceCollection AddFanficDownloader(this IServiceCollection services)
    {
        // Queue + background services
        services.AddSingleton<DownloadQueueService>();
        services.AddHostedService(sp => sp.GetRequiredService<DownloadQueueService>());
        services.AddHostedService<TelegramBotBackgroundService>();

        // Core services
        services.AddSingleton<SourceManager>();
        services.AddScoped<FanficDownloadService>();
        services.AddScoped<FanficService>();

        // Http services
        services.AddHttpClient<FanficEpubFormatter>();
        services.AddHttpClient<ImageDownloadService>();

        // Parsers
        services.AddTransient<FicbookParser>();
        services.AddTransient<SnapetalesParser>();
        services.AddTransient<FanfictionNetParser>();
        services.AddTransient<WalkingThePlankParser>();

        // FlareSolverr client
        services.AddHttpClient<FlareSolverrClient>((sp, http) =>
        {
            var settings = sp
                .GetRequiredService<IOptions<FlareSolverrSettings>>()
                .Value;

            http.BaseAddress = new Uri(settings.Url);
            http.Timeout = TimeSpan.FromSeconds(120);
        });

        // Sources
        services.AddHttpClient<IFanficSource, FicbookSource>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient<IFanficSource, SnapetalesSource>();
        services.AddTransient<IFanficSource, FanfictionNetSource>();
        services.AddHttpClient<IFanficSource, WalkingThePlankSource>();

        return services;
    }
}