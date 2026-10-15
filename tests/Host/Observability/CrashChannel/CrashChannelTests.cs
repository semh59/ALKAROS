using FluentAssertions;
using Xunit;

namespace ALKAROS.Host.Observability.CrashChannel.Tests;

public sealed class CrashChannelTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"alkaros-crash-{Guid.NewGuid():N}.log");

    public void Dispose() => File.Delete(_path);

    private sealed class FakeNotifier : ICriticalNotifier
    {
        public bool Accept { get; set; }
        public List<CriticalNotice> Sent { get; } = [];
        public int Calls { get; private set; }

        public Task<bool> SendAsync(CriticalNotice notice, CancellationToken cancellationToken)
        {
            Calls++;
            if (Accept)
                Sent.Add(notice);
            return Task.FromResult(Accept);
        }
    }

    private static CriticalNotice Notice(string category = "db.down") =>
        new("Database", category, DateTimeOffset.UtcNow);

    [Fact]
    public async Task ClosedChannelBuffersLocallyAndDrainsWhenItReturns()
    {
        var notifier = new FakeNotifier();
        var buffer = new LocalFileBuffer(_path);
        var channel = new CriticalOutageChannel(notifier, buffer, new CircuitBreaker(5, TimeSpan.Zero));

        (await channel.ReportAsync(Notice())).Should().BeFalse();
        buffer.ReadAll().Should().HaveCount(1);

        notifier.Accept = true;
        (await channel.DrainAsync()).Should().Be(1);
        notifier.Sent.Should().HaveCount(1);
        buffer.ReadAll().Should().BeEmpty();
    }

    [Fact]
    public async Task OpenBreakerSkipsTheNotifierUntilCoolDownPasses()
    {
        var now = DateTimeOffset.UtcNow;
        var notifier = new FakeNotifier();
        var channel = new CriticalOutageChannel(
            notifier, new LocalFileBuffer(_path), new CircuitBreaker(2, TimeSpan.FromSeconds(30), () => now));

        await channel.ReportAsync(Notice());
        await channel.ReportAsync(Notice());
        var callsWhenOpen = notifier.Calls;
        await channel.ReportAsync(Notice());
        notifier.Calls.Should().Be(callsWhenOpen);

        now = now.AddSeconds(31);
        await channel.ReportAsync(Notice());
        notifier.Calls.Should().Be(callsWhenOpen + 1);
    }

    [Fact]
    public void FullBufferDropsOldestAndCountsThem()
    {
        var buffer = new LocalFileBuffer(_path, maxBytes: 400);

        for (var i = 0; i < 10; i++)
            buffer.Append(Notice($"cat.{i}"));

        var kept = buffer.ReadAll();
        buffer.Dropped.Should().BeGreaterThan(0);
        kept.Count.Should().Be(10 - (int)buffer.Dropped);
        kept[^1].Category.Should().Be("cat.9");
        kept[0].Category.Should().NotBe("cat.0");
    }

    [Fact]
    public async Task WatchdogRaisesOneAlarmAfterConsecutiveFailuresAndResetsOnRecovery()
    {
        var notifier = new FakeNotifier { Accept = true };
        var channel = new CriticalOutageChannel(notifier, new LocalFileBuffer(_path), new CircuitBreaker(5, TimeSpan.Zero));
        var healthy = false;
        var watchdog = new OutageWatchdog(_ => Task.FromResult(healthy), channel, failuresBeforeAlarm: 2);

        await watchdog.CheckOnceAsync();
        notifier.Sent.Should().BeEmpty();
        await watchdog.CheckOnceAsync();
        await watchdog.CheckOnceAsync();
        notifier.Sent.Should().ContainSingle(n => n.Category == "system.unreachable");

        healthy = true;
        await watchdog.CheckOnceAsync();
        healthy = false;
        await watchdog.CheckOnceAsync();
        await watchdog.CheckOnceAsync();
        notifier.Sent.Should().HaveCount(2);
        watchdog.Heartbeats.Should().Be(6);
    }
}
