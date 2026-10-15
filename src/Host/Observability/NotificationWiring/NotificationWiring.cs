using ALKAROS.Host.Observability.Wiring;
using ALKAROS.Observability.ErrorNotifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ALKAROS.Host.Observability.NotificationWiring;

/// <summary>Runs the dispatcher on a fixed interval; a failed round is retried on the next one.</summary>
public sealed class NotificationDispatcherHostedService(
    NotificationDispatcher dispatcher, TimeSpan? interval = null) : BackgroundService
{
    public long Failures { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var wait = interval ?? TimeSpan.FromSeconds(30);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await dispatcher.RunOnceAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Failures++;
            }

            await Task.Delay(wait, stoppingToken);
        }
    }
}

public static class NotificationWiringExtensions
{
    public const string BotKeySetting = "Monitoring:Telegram:BotKey";
    public const string ChatIdSetting = "Monitoring:Telegram:ChatId";

    public static (string? BotKey, string? ChatId) Credentials(IConfiguration configuration) =>
        (configuration[BotKeySetting], configuration[ChatIdSetting]);

    /// <summary>
    /// Queues notifications for error events and delivers them in the background. Without both settings the
    /// channel stays off and the queue simply keeps its rows. The HTTP client is created directly (no factory),
    /// so no request address, and with it the bot key, is ever written to a log.
    /// </summary>
    public static IServiceCollection AddNotificationDelivery(this IServiceCollection services)
    {
        services.AddSingleton(sp => new NotificationEnqueuer(sp.GetRequiredService<MonitoringDataSource>().Inner));
        services.AddSingleton<INotificationChannel>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            return new TelegramChannel(new HttpClient { Timeout = TimeSpan.FromSeconds(10) }, () => Credentials(configuration));
        });
        services.AddSingleton(sp => new NotificationDispatcher(
            sp.GetRequiredService<MonitoringDataSource>().Inner, sp.GetRequiredService<INotificationChannel>()));
        services.AddHostedService(sp => new NotificationDispatcherHostedService(sp.GetRequiredService<NotificationDispatcher>()));
        return services;
    }
}
