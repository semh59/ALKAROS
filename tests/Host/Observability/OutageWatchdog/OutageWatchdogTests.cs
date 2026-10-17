using System.Net;
using ALKAROS.Host.Observability.CrashChannel;
using ALKAROS.Host.Observability.WatchdogProcess;
using ALKAROS.Observability.ErrorNotifications;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Host.Observability.WatchdogProcess.Tests;

public sealed class OutageWatchdogTests : IDisposable
{
    private readonly string _buffer = Path.Combine(Path.GetTempPath(), $"alkaros-watchdog-{Guid.NewGuid():N}.log");
    private readonly HttpListener _listener = new();
    private readonly Uri _url;
    private readonly List<string?> _forwardedProto = [];
    private int _status = 503;

    public OutageWatchdogTests()
    {
        var port = FreePort();
        _url = new Uri($"http://127.0.0.1:{port}/health/ready");
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Start();
        _ = Task.Run(async () =>
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException)
                {
                    return;
                }

                _forwardedProto.Add(context.Request.Headers["X-Forwarded-Proto"]);
                context.Response.StatusCode = _status;
                context.Response.Close();
            }
        });
    }

    public void Dispose()
    {
        _listener.Close();
        File.Delete(_buffer);
    }

    private static int FreePort()
    {
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private sealed class FakeChannel(NotificationOutcome outcome) : INotificationChannel
    {
        public NotificationOutcome Outcome { get; set; } = outcome;
        public List<string> Texts { get; } = [];
        public bool IsConfigured => true;

        public Task<NotificationSendResult> SendAsync(string text, CancellationToken cancellationToken)
        {
            if (Outcome == NotificationOutcome.Sent)
                Texts.Add(text);
            return Task.FromResult(new NotificationSendResult(Outcome));
        }
    }

    private OutageWatchdog Build(FakeChannel channel, int failuresBeforeAlarm = 2) =>
        OutageWatchdogHost.Create(
            new WatchdogOptions(_url, _buffer, TimeSpan.FromSeconds(1), failuresBeforeAlarm),
            new HttpClient { Timeout = TimeSpan.FromSeconds(5) }, channel);

    [Fact]
    public async Task AnOutageIsReportedOnceAfterTheConfiguredNumberOfFailedProbes()
    {
        var channel = new FakeChannel(NotificationOutcome.Sent);
        var watchdog = Build(channel);

        await watchdog.CheckOnceAsync();
        channel.Texts.Should().BeEmpty();
        await watchdog.CheckOnceAsync();
        await watchdog.CheckOnceAsync();

        channel.Texts.Should().ContainSingle().Which.Should().StartWith("KRITIK: Api / system.unreachable / ");
    }

    [Fact]
    public async Task EveryProbeCarriesTheForwardedProtocolHeaderThatThePlainHttpGateRequires()
    {
        _status = 200;
        var watchdog = Build(new FakeChannel(NotificationOutcome.Sent));

        await watchdog.CheckOnceAsync();

        _forwardedProto.Should().Equal("https");
    }

    [Fact]
    public async Task AHealthyProbeSendsNothing()
    {
        _status = 200;
        var channel = new FakeChannel(NotificationOutcome.Sent);
        var watchdog = Build(channel, failuresBeforeAlarm: 1);

        await watchdog.CheckOnceAsync();

        channel.Texts.Should().BeEmpty();
        File.Exists(_buffer).Should().BeFalse();
    }

    [Fact]
    public async Task ARefusedAlarmIsKeptInTheFileBufferAndDeliveredOnceTheApiIsBackAndTheChannelAccepts()
    {
        var channel = new FakeChannel(NotificationOutcome.Rejected);
        var watchdog = Build(channel, failuresBeforeAlarm: 1);

        await watchdog.CheckOnceAsync();
        new LocalFileBuffer(_buffer).ReadAll().Should().ContainSingle().Which.Category.Should().Be("system.unreachable");

        _status = 200;
        channel.Outcome = NotificationOutcome.Sent;
        await watchdog.CheckOnceAsync();

        channel.Texts.Should().ContainSingle();
        new LocalFileBuffer(_buffer).ReadAll().Should().BeEmpty();
    }

    [Theory]
    [InlineData(NotificationOutcome.Sent, true)]
    [InlineData(NotificationOutcome.RetryLater, false)]
    [InlineData(NotificationOutcome.Rejected, false)]
    public async Task OnlyASentOutcomeCountsAsAcceptedByTheChannel(NotificationOutcome outcome, bool accepted)
    {
        var notifier = new ChannelCriticalNotifier(new FakeChannel(outcome));

        (await notifier.SendAsync(new CriticalNotice("Api", "system.unreachable", DateTimeOffset.UtcNow), default)).Should().Be(accepted);
    }

    [Fact]
    public void TheNoticeTextIsFixedTokensInUtc()
    {
        var notice = new CriticalNotice("Database", "db.down", new DateTimeOffset(2026, 10, 17, 14, 30, 5, TimeSpan.FromHours(3)));

        ChannelCriticalNotifier.Format(notice).Should().Be("KRITIK: Database / db.down / 2026-10-17 11:30:05 UTC");
    }

    [Fact]
    public void ValidOptionsAreParsedWithDefaults()
    {
        var options = WatchdogOptions.Parse(["--health-url", "http://api:5080/health/ready", "--buffer", "/data/outage.log"]);

        options.Should().Be(new WatchdogOptions(new Uri("http://api:5080/health/ready"), "/data/outage.log", TimeSpan.FromSeconds(15), 2));
    }

    [Fact]
    public void ExplicitIntervalAndFailureCountAreHonoured()
    {
        var options = WatchdogOptions.Parse(
            ["--health-url", "https://x/h", "--buffer", "b", "--interval-seconds", "7", "--failures-before-alarm", "4"]);

        options.Interval.Should().Be(TimeSpan.FromSeconds(7));
        options.FailuresBeforeAlarm.Should().Be(4);
    }

    [Theory]
    [InlineData("--buffer", "b")]
    [InlineData("--health-url", "http://x/h")]
    [InlineData("--health-url", "ftp://x/h", "--buffer", "b")]
    [InlineData("--health-url", "not a url", "--buffer", "b")]
    [InlineData("--health-url", "http://x/h", "--buffer", "  ")]
    [InlineData("--health-url", "http://x/h", "--buffer", "b", "--interval-seconds", "0")]
    [InlineData("--health-url", "http://x/h", "--buffer", "b", "--failures-before-alarm", "0")]
    [InlineData("--health-url", "http://x/h", "--buffer", "b", "--bogus", "1")]
    [InlineData("--health-url", "http://x/h", "--buffer")]
    public void InvalidOptionsAreRejected(params string[] args)
    {
        var act = () => WatchdogOptions.Parse(args);

        act.Should().Throw<ArgumentException>();
    }
}
