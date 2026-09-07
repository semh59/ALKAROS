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
