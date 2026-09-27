namespace ALKAROS.QrRelay.LocalConnector;

/// <summary>
/// V12-QRT-001. Deliberately does not claim "Connected" — this process only
/// observes whether the supervised `cloudflared` child process is alive, not
/// whether it has an active QUIC connection to Cloudflare's edge (that would
/// require parsing cloudflared's own logs or metrics endpoint, not attempted
/// here). "Running" means the process is up; whether it is actually
/// connected is visible in the container's own log stream.
/// </summary>
public enum RelayConnectorState
{
    /// <summary>No tunnel has been provisioned yet — nothing to run.</summary>
    NotConfigured,

    /// <summary>The supervised process is currently alive.</summary>
    Running,

    /// <summary>The process exited unexpectedly; a restart is pending after the backoff delay.</summary>
    Restarting,

    /// <summary>
    /// V1-RMD-324 (independent 2026-09-26 audit, finding K17): reported by
    /// <see cref="ALKAROS.QrRelay.PublicGateway.PostgresRelayConnectorStatusReporter"/>, never by
    /// <see cref="ALKAROS.QrRelay.LocalConnector.RelayConnectorSupervisor"/> itself — the connector
    /// container's own view of ITSELF is never stale by definition. Means the last row
    /// <see cref="RelayConnectorStatusPublisher"/> wrote is older than its own publish interval would
    /// ever explain on its own: the whole connector container (this publisher included) most likely died,
    /// leaving whatever state was last written (often "Running") frozen and no longer trustworthy.
    /// </summary>
    Unknown,
}

public sealed record RelayConnectorStatus(
    RelayConnectorState State,
    DateTimeOffset? LastStartedAt,
    int RestartCount,
    int? LastExitCode);

public interface IRelayConnectorStatusReporter
{
    /// <summary>
    /// V1-RMD-353 (independent 2026-09-26 audit, a low-severity finding): async so that
    /// <see cref="ALKAROS.QrRelay.PublicGateway.PostgresRelayConnectorStatusReporter"/>'s implementation
    /// (a real Postgres round-trip) never blocks a thread-pool thread inside an already-async HTTP request
    /// handler. <see cref="RelayConnectorSupervisor"/>'s own implementation is a genuinely instant in-memory
    /// read and simply wraps its existing synchronous <c>CurrentStatus</c> property in a completed task.
    /// </summary>
    Task<RelayConnectorStatus> GetCurrentStatusAsync(CancellationToken cancellationToken);
}
