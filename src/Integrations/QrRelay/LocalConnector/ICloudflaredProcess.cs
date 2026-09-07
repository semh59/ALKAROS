namespace ALKAROS.QrRelay.LocalConnector;

/// <summary>
/// V12-QRT-001. A thin wrapper over the running <c>cloudflared</c> child
/// process — just enough surface for the supervisor to detect an unexpected
/// exit and to stop it cleanly. Never exposes the process's command line or
/// environment (the tunnel token lives only in the environment, never a CLI
/// argument, so it never appears in a process listing).
/// </summary>
public interface ICloudflaredProcess : IDisposable
{
    bool HasExited { get; }

    /// <summary>Valid only once <see cref="HasExited"/> is true.</summary>
    int ExitCode { get; }

    /// <summary>Requests a graceful stop; the supervisor does not wait beyond its own shutdown timeout.</summary>
    void RequestStop();
}
