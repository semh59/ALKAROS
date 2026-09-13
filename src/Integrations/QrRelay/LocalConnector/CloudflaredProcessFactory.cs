using System.Diagnostics;

namespace ALKAROS.QrRelay.LocalConnector;

/// <summary>
/// V12-QRT-001. Real process launch — requires the <c>cloudflared</c>
/// binary on PATH (baked into the `api` container image; see
/// `deploy/docker/Dockerfile`). stdout/stderr are inherited so cloudflared's
/// own connection logs ("Registered tunnel connection", errors, etc.) land
/// directly in the container's own log stream — this is the only place that
/// activity is currently observable; no metrics parsing is attempted.
/// </summary>
public sealed class CloudflaredProcessFactory : ICloudflaredProcessFactory
{
    private const string ExecutableName = "cloudflared";

    public ICloudflaredProcess Start(string tunnelToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tunnelToken);

        var startInfo = new ProcessStartInfo
        {
            FileName = ExecutableName,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
        };
        startInfo.ArgumentList.Add("tunnel");
        // V1-RMD-193: found by inspecting the running api container's own
        // logs — cloudflared defaults to checking for updates every 24h
        // (autoupdateFreq=86400000, visible in its own startup log) and, on
        // finding one, silently replaces its on-disk binary and restarts
        // itself ("cloudflared has been updated to version 2026.9.0", then
        // "...2026.9.1", each a real version/checksum change, confirmed
        // twice in production). deploy/docker/Dockerfile pins this binary
        // by exact release + sha256 specifically so a rebuild is the only
        // way it changes — the missing flag let cloudflared bypass that pin
        // entirely at runtime, and each self-update briefly dropped the
        // relay tunnel (a burst of QUIC/datagram errors around every
        // restart in the logs). --no-autoupdate keeps the pinned binary
        // pinned until the next image rebuild, like every other dependency
        // here.
        startInfo.ArgumentList.Add("--no-autoupdate");
        startInfo.ArgumentList.Add("run");
        startInfo.Environment["TUNNEL_TOKEN"] = tunnelToken;

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("cloudflared process did not start.");
        return new RealCloudflaredProcess(process);
    }

    private sealed class RealCloudflaredProcess : ICloudflaredProcess
    {
        private readonly Process _process;

        public RealCloudflaredProcess(Process process)
        {
            _process = process;
        }

        public bool HasExited => _process.HasExited;

        public int ExitCode => _process.ExitCode;

        public void RequestStop()
        {
            if (_process.HasExited)
                return;

            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already exited between the HasExited check and Kill — fine.
            }
        }

        public void Dispose() => _process.Dispose();
    }
}
