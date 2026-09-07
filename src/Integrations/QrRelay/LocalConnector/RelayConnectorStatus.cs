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
}

public sealed record RelayConnectorStatus(
    RelayConnectorState State,
    DateTimeOffset? LastStartedAt,
    int RestartCount,
    int? LastExitCode);

public interface IRelayConnectorStatusReporter
{
    RelayConnectorStatus CurrentStatus { get; }
}
