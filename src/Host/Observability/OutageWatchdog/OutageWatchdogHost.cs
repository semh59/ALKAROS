using ALKAROS.Host.Observability.CrashChannel;
using ALKAROS.Host.Observability.NotificationWiring;
using ALKAROS.Observability.ErrorNotifications;
using Microsoft.Extensions.Configuration;

namespace ALKAROS.Host.Observability.WatchdogProcess;

/// <summary>Sends the fixed-token notice straight to the channel; an unconfigured or refusing channel is reported as not accepted.</summary>
public sealed class ChannelCriticalNotifier(INotificationChannel channel) : ICriticalNotifier
{
    public static string Format(CriticalNotice notice) =>
        $"KRITIK: {notice.Layer} / {notice.Category} / {notice.At.UtcDateTime:yyyy-MM-dd HH:mm:ss} UTC";

    public async Task<bool> SendAsync(CriticalNotice notice, CancellationToken cancellationToken) =>
        (await channel.SendAsync(Format(notice), cancellationToken).ConfigureAwait(false)).Outcome == NotificationOutcome.Sent;
}

public sealed record WatchdogOptions(Uri HealthUrl, string BufferPath, TimeSpan Interval, int FailuresBeforeAlarm)
{
    public static WatchdogOptions Parse(string[] args)
    {
        string? health = null, buffer = null;
        var seconds = 15;
        var failures = 2;
        for (var i = 0; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length)
                throw new ArgumentException($"Missing value for {args[i]}.");
            switch (args[i])
            {
                case "--health-url": health = args[i + 1]; break;
                case "--buffer": buffer = args[i + 1]; break;
                case "--interval-seconds": seconds = int.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture); break;
                case "--failures-before-alarm": failures = int.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture); break;
                default: throw new ArgumentException($"Unknown option {args[i]}.");
            }
        }

        if (health is null || !Uri.TryCreate(health, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
            throw new ArgumentException("--health-url must be an absolute http(s) address.");
        if (string.IsNullOrWhiteSpace(buffer))
            throw new ArgumentException("--buffer is required.");
        if (seconds < 1 || failures < 1)
            throw new ArgumentException("--interval-seconds and --failures-before-alarm must be at least 1.");
        return new WatchdogOptions(url, buffer, TimeSpan.FromSeconds(seconds), failures);
    }
}

/// <summary>
/// The separate-container watcher: probes the readiness address on a fixed interval and reports an outage straight to
/// the notification channel, so it keeps working while the database and the API are down. The probe carries
/// X-Forwarded-Proto because the API rejects plain HTTP from a non-loopback caller unless the proxy header is present.
/// </summary>
public static class OutageWatchdogHost
{
    public static OutageWatchdog Create(WatchdogOptions options, HttpClient probeClient, INotificationChannel channel)
    {
        probeClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        var outage = new CriticalOutageChannel(
            new ChannelCriticalNotifier(channel),
            new LocalFileBuffer(options.BufferPath),
            new CircuitBreaker(3, TimeSpan.FromMinutes(1)));
        return new OutageWatchdog(OutageWatchdog.HttpProbe(probeClient, options.HealthUrl), outage, options.FailuresBeforeAlarm);
    }

    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var options = WatchdogOptions.Parse(args);
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        using var probeClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        using var telegramClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var channel = new TelegramChannel(telegramClient, () => NotificationWiringExtensions.Credentials(configuration));
        var watchdog = Create(options, probeClient, channel);
        Console.WriteLine($"WATCHDOG: started, telegramConfigured={channel.IsConfigured}");

        using var timer = new PeriodicTimer(options.Interval);
        try
        {
            do
            {
                try
                {
                    await watchdog.CheckOnceAsync(cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // Only the type is printed: a transport message could carry the request address.
                    Console.Error.WriteLine($"WATCHDOG: check failed ({exception.GetType().Name}), will retry");
                }
            }
            while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }

        return 0;
    }
}
