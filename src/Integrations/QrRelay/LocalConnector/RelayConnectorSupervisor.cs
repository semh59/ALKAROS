using ALKAROS.QrRelay.PublicGateway;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ALKAROS.QrRelay.LocalConnector;

/// <summary>
/// V12-QRT-001 LocalConnector. Supervises `cloudflared` as a managed child
/// process of the Host itself — no OS service registration (Windows Service /
/// systemd unit), no elevation: as long as the Host process is running (it
/// already must be, for the POS to work at all), this keeps the tunnel
/// connector running alongside it, restarting it after a crash and cutting
/// over cleanly when the tunnel is (re)provisioned with a new token.
///
/// Polls on a fixed interval rather than reacting to a provisioning event —
/// simpler, and correctly self-heals even if the Host process itself
/// restarted after a token was saved while it was down.
/// </summary>
public sealed class RelayConnectorSupervisor : BackgroundService, IRelayConnectorStatusReporter
{
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DefaultRestartBackoff = TimeSpan.FromSeconds(5);

    // V1-RMD-138: found by an independent audit (2026-09-09) — a genuinely
    // crash-looping cloudflared (bad token, network unreachable, etc.) used
    // to be retried forever on the same fixed 5s backoff with no ceiling and
    // no distinct log signal beyond the per-exit Warning already below —
    // easy to miss in a noisy log stream. A process that keeps exiting
    // faster than FastFailureThreshold after each start now backs off
    // exponentially (capped at MaxRestartBackoff) and, past
    // CrashLoopWarningThreshold consecutive fast failures, logs once at
    // Error. A process that stays up past FastFailureThreshold resets the
    // streak — a connector that runs fine for hours and drops once is not a
    // crash loop.
    private static readonly TimeSpan MaxRestartBackoff = TimeSpan.FromMinutes(5);
    private const int CrashLoopWarningThreshold = 5;

    private static readonly Action<ILogger, Exception?> LogTickFault =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(5500, nameof(LogTickFault)),
            "Relay connector supervisor tick failed; retrying on the next poll.");

    private static readonly Action<ILogger, int, Exception?> LogUnexpectedExit =
        LoggerMessage.Define<int>(
            LogLevel.Warning,
            new EventId(5501, nameof(LogUnexpectedExit)),
            "cloudflared exited unexpectedly (exit code {ExitCode}); restarting after backoff.");

    private static readonly Action<ILogger, Exception?> LogTokenChanged =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(5502, nameof(LogTokenChanged)),
            "Relay tunnel was reprovisioned with a new token; restarting cloudflared with it.");

    private static readonly Action<ILogger, int, TimeSpan, Exception?> LogPossibleCrashLoop =
        LoggerMessage.Define<int, TimeSpan>(
            LogLevel.Error,
            new EventId(5503, nameof(LogPossibleCrashLoop)),
            "cloudflared has failed {ConsecutiveFastFailures} times in a row shortly after starting; " +
            "this looks like a crash loop (bad tunnel token, unreachable network?). Backing off to {Backoff}.");

    private readonly IRelayTunnelStore _tunnelStore;
    private readonly ICloudflaredProcessFactory _processFactory;
    private readonly ILogger<RelayConnectorSupervisor> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _restartBackoff;
    private readonly object _statusLock = new();

    private ICloudflaredProcess? _process;
    private string? _runningToken;
    private RelayConnectorStatus _status = new(RelayConnectorState.NotConfigured, null, 0, null);
    private int _consecutiveFastFailures;

    public RelayConnectorSupervisor(
        IRelayTunnelStore tunnelStore,
        ICloudflaredProcessFactory processFactory,
        ILogger<RelayConnectorSupervisor> logger)
        : this(tunnelStore, processFactory, logger, DefaultPollInterval, DefaultRestartBackoff)
    {
    }

    /// <summary>Lets a test (or an operator who wants a different cadence) override the production 10s/5s defaults.</summary>
    public RelayConnectorSupervisor(
        IRelayTunnelStore tunnelStore,
        ICloudflaredProcessFactory processFactory,
        ILogger<RelayConnectorSupervisor> logger,
        TimeSpan pollInterval,
        TimeSpan restartBackoff)
    {
        _tunnelStore = tunnelStore ?? throw new ArgumentNullException(nameof(tunnelStore));
        _processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pollInterval = pollInterval;
        _restartBackoff = restartBackoff;
    }

    public RelayConnectorStatus CurrentStatus
    {
        get { lock (_statusLock) return _status; }
    }

    /// <summary>V1-RMD-353: this container's own view of itself is always an instant in-memory read.</summary>
    public Task<RelayConnectorStatus> GetCurrentStatusAsync(CancellationToken cancellationToken)
        => Task.FromResult(CurrentStatus);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogTickFault(_logger, ex);
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        StopProcess();
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        var token = await _tunnelStore.ResolveTunnelTokenAsync(cancellationToken).ConfigureAwait(false);

        if (token is null)
        {
            StopProcess();
            SetStatus(new RelayConnectorStatus(RelayConnectorState.NotConfigured, null, 0, null));
            return;
        }

        if (_process is not null && !string.Equals(_runningToken, token, StringComparison.Ordinal))
        {
            LogTokenChanged(_logger, null);
            StopProcess();
        }

        if (_process is null)
        {
            StartProcess(token);
            return;
        }

        if (_process.HasExited)
        {
            var exitCode = _process.ExitCode;
            var ranFor = CurrentStatus.LastStartedAt is { } startedAt
                ? DateTimeOffset.UtcNow - startedAt
                : TimeSpan.Zero;
            LogUnexpectedExit(_logger, exitCode, null);
            _process.Dispose();
            _process = null;
            _runningToken = null;
            SetStatus(CurrentStatus with { State = RelayConnectorState.Restarting, RestartCount = CurrentStatus.RestartCount + 1, LastExitCode = exitCode });

            var backoff = ComputeBackoff(ranFor);
            await Task.Delay(backoff, cancellationToken).ConfigureAwait(false);
            StartProcess(token);
        }
    }

    /// <summary>
    /// Doubles the base backoff for every consecutive failure that happened
    /// "fast" (the process didn't stay up for at least 3 poll intervals —
    /// long enough that a genuine, momentary blip wouldn't trip it), capped
    /// at <see cref="MaxRestartBackoff"/>. A failure that wasn't fast resets
    /// the streak: an otherwise-healthy connector that drops once is not
    /// treated as a crash loop.
    /// </summary>
    private TimeSpan ComputeBackoff(TimeSpan ranFor)
    {
        var wasFastFailure = ranFor < _pollInterval * 3;
        _consecutiveFastFailures = wasFastFailure ? _consecutiveFastFailures + 1 : 0;

        if (_consecutiveFastFailures == 0)
            return _restartBackoff;

        if (_consecutiveFastFailures >= CrashLoopWarningThreshold)
            LogPossibleCrashLoop(_logger, _consecutiveFastFailures, _restartBackoff, null);

        // The first fast failure keeps the plain base backoff (matches the
        // pre-existing, always-5s-in-production behaviour exactly); only
        // the SECOND and later consecutive fast failures actually scale up.
        var multiplier = Math.Pow(2, Math.Min(_consecutiveFastFailures - 1, 10));
        var scaled = _restartBackoff.TotalMilliseconds * multiplier;
        return scaled >= MaxRestartBackoff.TotalMilliseconds
            ? MaxRestartBackoff
            : TimeSpan.FromMilliseconds(scaled);
    }

    private void StartProcess(string token)
    {
        _process = _processFactory.Start(token);
        _runningToken = token;
        SetStatus(CurrentStatus with { State = RelayConnectorState.Running, LastStartedAt = DateTimeOffset.UtcNow });
    }

    private void StopProcess()
    {
        _process?.RequestStop();
        _process?.Dispose();
        _process = null;
        _runningToken = null;
    }

    private void SetStatus(RelayConnectorStatus status)
    {
        lock (_statusLock) _status = status;
    }
}
