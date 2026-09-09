using ALKAROS.QrRelay.PublicGateway;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ALKAROS.QrRelay.LocalConnector.Tests;

/// <summary>
/// V12-QRT-001 LocalConnector. All timing here uses a short poll interval
/// (20ms) and backoff (20ms) via the internal test constructor — real
/// `Task.Delay`, not a fake clock, synchronized on the fake factory's own
/// signal rather than blind sleeps so the assertions aren't a race with the
/// supervisor's background loop.
/// </summary>
public sealed class RelayConnectorSupervisorTests
{
    private static readonly TimeSpan ShortInterval = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task WithNoTunnelConfiguredNeverStartsAProcess()
    {
        var factory = new FakeProcessFactory();
        var supervisor = CreateSupervisor(new FakeTunnelStore(() => null), factory);

        await supervisor.StartAsync(CancellationToken.None);
        try
        {
            // No positive signal to wait on for a negative case — a few poll
            // intervals is enough to prove it never fires.
            await Task.Delay(ShortInterval * 5);
            Assert.Empty(factory.Starts);
            Assert.Equal(RelayConnectorState.NotConfigured, supervisor.CurrentStatus.State);
        }
        finally
        {
            await supervisor.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task WithATunnelConfiguredStartsTheProcessOnce()
    {
        var factory = new FakeProcessFactory();
        var supervisor = CreateSupervisor(new FakeTunnelStore(() => "token-abc"), factory);

        await supervisor.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(await factory.StartSignal.WaitAsync(SignalTimeout));
            Assert.Single(factory.Starts);
            Assert.Equal("token-abc", factory.Starts[0].Token);
            Assert.Equal(RelayConnectorState.Running, supervisor.CurrentStatus.State);
            Assert.NotNull(supervisor.CurrentStatus.LastStartedAt);
        }
        finally
        {
            await supervisor.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task RestartsAfterAnUnexpectedExitWithIncrementingRestartCount()
    {
        var factory = new FakeProcessFactory();
        var supervisor = CreateSupervisor(new FakeTunnelStore(() => "token-abc"), factory);

        await supervisor.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(await factory.StartSignal.WaitAsync(SignalTimeout));
            factory.Starts[0].Process.SimulateExit(exitCode: 1);

            Assert.True(await factory.StartSignal.WaitAsync(SignalTimeout));
            Assert.Equal(2, factory.Starts.Count);
            Assert.Equal("token-abc", factory.Starts[1].Token);
            Assert.Equal(RelayConnectorState.Running, supervisor.CurrentStatus.State);
            Assert.Equal(1, supervisor.CurrentStatus.RestartCount);
            Assert.Equal(1, supervisor.CurrentStatus.LastExitCode);
        }
        finally
        {
            await supervisor.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// V1-RMD-138: found by an independent audit (2026-09-09) — a genuinely
    /// crash-looping cloudflared used to be retried forever on the exact
    /// same fixed backoff. Two fast failures in a row must now wait
    /// noticeably longer the second time. Uses a larger base backoff
    /// (200ms, not the shared 20ms ShortInterval) so the doubling is
    /// comfortably bigger than ordinary scheduler jitter.
    /// </summary>
    [Fact]
    public async Task ConsecutiveFastFailuresBackOffExponentially()
    {
        var baseBackoff = TimeSpan.FromMilliseconds(200);
        var factory = new FakeProcessFactory();
        var supervisor = new RelayConnectorSupervisor(
            new FakeTunnelStore(() => "token-abc"), factory, NullLogger<RelayConnectorSupervisor>.Instance,
            ShortInterval, baseBackoff);

        await supervisor.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(await factory.StartSignal.WaitAsync(SignalTimeout));
            var firstStartedAt = DateTimeOffset.UtcNow;

            // First fast failure: restart happens after ~baseBackoff.
            factory.Starts[0].Process.SimulateExit(exitCode: 1);
            Assert.True(await factory.StartSignal.WaitAsync(SignalTimeout));
            var firstGap = DateTimeOffset.UtcNow - firstStartedAt;

            // Second fast failure right away: restart must wait noticeably
            // longer than the first (2x the base backoff, not just ~baseBackoff).
            var secondStartedAt = DateTimeOffset.UtcNow;
            factory.Starts[1].Process.SimulateExit(exitCode: 1);
            Assert.True(await factory.StartSignal.WaitAsync(SignalTimeout));
            var secondGap = DateTimeOffset.UtcNow - secondStartedAt;

            Assert.Equal(3, factory.Starts.Count);
            Assert.True(
                secondGap > firstGap + TimeSpan.FromMilliseconds(100),
                $"Expected the second restart to back off noticeably longer than the first (first={firstGap}, second={secondGap}).");
        }
        finally
        {
            await supervisor.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// A connector that stays up well past the "fast failure" window before
    /// dropping is not a crash loop — the very next failure must still use
    /// the plain base backoff, not an escalated one left over from an
    /// unrelated, much earlier fast failure.
    /// </summary>
    [Fact]
    public async Task ARunThatStaysUpResetsTheFastFailureStreak()
    {
        var baseBackoff = TimeSpan.FromMilliseconds(200);
        var factory = new FakeProcessFactory();
        var supervisor = new RelayConnectorSupervisor(
            new FakeTunnelStore(() => "token-abc"), factory, NullLogger<RelayConnectorSupervisor>.Instance,
            ShortInterval, baseBackoff);

        await supervisor.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(await factory.StartSignal.WaitAsync(SignalTimeout));
            factory.Starts[0].Process.SimulateExit(exitCode: 1);
            Assert.True(await factory.StartSignal.WaitAsync(SignalTimeout));

            // Let the second run stay up well past the "fast failure" window
            // (3 * ShortInterval) before it also drops.
            await Task.Delay(ShortInterval * 10);
            var thirdStartedAt = DateTimeOffset.UtcNow;
            factory.Starts[1].Process.SimulateExit(exitCode: 1);
            Assert.True(await factory.StartSignal.WaitAsync(SignalTimeout));
            var gap = DateTimeOffset.UtcNow - thirdStartedAt;

            Assert.True(
                gap < baseBackoff + TimeSpan.FromMilliseconds(100),
                $"Expected the streak to have reset to the plain base backoff, got {gap}.");
        }
        finally
        {
            await supervisor.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ReprovisioningWithANewTokenStopsTheOldProcessAndStartsTheNewOne()
    {
        var factory = new FakeProcessFactory();
        var currentToken = "token-old";
        var supervisor = CreateSupervisor(new FakeTunnelStore(() => currentToken), factory);

        await supervisor.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(await factory.StartSignal.WaitAsync(SignalTimeout));
            currentToken = "token-new";

            Assert.True(await factory.StartSignal.WaitAsync(SignalTimeout));
            Assert.Equal(2, factory.Starts.Count);
            Assert.Equal("token-old", factory.Starts[0].Token);
            Assert.Equal("token-new", factory.Starts[1].Token);
            Assert.True(factory.Starts[0].Process.StopRequested);
        }
        finally
        {
            await supervisor.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task StoppingTheSupervisorRequestsTheProcessToStop()
    {
        var factory = new FakeProcessFactory();
        var supervisor = CreateSupervisor(new FakeTunnelStore(() => "token-abc"), factory);

        await supervisor.StartAsync(CancellationToken.None);
        Assert.True(await factory.StartSignal.WaitAsync(SignalTimeout));

        await supervisor.StopAsync(CancellationToken.None);

        Assert.True(factory.Starts[0].Process.StopRequested);
    }

    private static RelayConnectorSupervisor CreateSupervisor(FakeTunnelStore tunnelStore, FakeProcessFactory processFactory) =>
        new(tunnelStore, processFactory, NullLogger<RelayConnectorSupervisor>.Instance, ShortInterval, ShortInterval);

    private sealed class FakeTunnelStore : IRelayTunnelStore
    {
        private readonly Func<string?> _tokenProvider;

        public FakeTunnelStore(Func<string?> tokenProvider) => _tokenProvider = tokenProvider;

        public Task SaveAsync(string tunnelId, string tunnelToken, string hostname, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RelayTunnelInfo?> GetInfoAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string?> ResolveTunnelTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_tokenProvider());
    }

    private sealed class FakeProcess : ICloudflaredProcess
    {
        private volatile bool _hasExited;

        public bool HasExited => _hasExited;
        public int ExitCode { get; private set; }
        public bool StopRequested { get; private set; }

        public void SimulateExit(int exitCode)
        {
            ExitCode = exitCode;
            _hasExited = true;
        }

        public void RequestStop() => StopRequested = true;

        public void Dispose()
        {
        }
    }

    private sealed class FakeProcessFactory : ICloudflaredProcessFactory
    {
        private readonly List<(string Token, FakeProcess Process)> _starts = [];
        private readonly object _lock = new();

        public SemaphoreSlim StartSignal { get; } = new(0);

        public List<(string Token, FakeProcess Process)> Starts
        {
            get { lock (_lock) return [.. _starts]; }
        }

        public ICloudflaredProcess Start(string tunnelToken)
        {
            var process = new FakeProcess();
            lock (_lock) _starts.Add((tunnelToken, process));
            StartSignal.Release();
            return process;
        }
    }
}
