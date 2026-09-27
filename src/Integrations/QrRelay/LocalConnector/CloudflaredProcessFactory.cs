using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ALKAROS.QrRelay.LocalConnector;

/// <summary>
/// V12-QRT-001. Real process launch — requires the <c>cloudflared</c>
/// binary on PATH (baked into the `connector` container image, V12-QRT-005;
/// see `deploy/docker/Dockerfile`). stdout/stderr are inherited so cloudflared's
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
        // V1-RMD-354 (independent 2026-09-26 audit, a low-severity finding): RequestStop used to go straight
        // to Process.Kill (SIGKILL on the Linux container this runs in) despite ICloudflaredProcess's own
        // doc comment promising "a graceful stop". cloudflared handles SIGTERM by closing its QUIC/HTTP2
        // connections to Cloudflare's edge cleanly before exiting; SIGKILL gives it no chance to do that, so
        // the edge only notices the connection is gone after its own liveness timeout instead of immediately.
        // Docker's own `docker stop` (SIGTERM, wait, then SIGKILL) is the same shape this now follows.
        private static readonly TimeSpan GracefulShutdownTimeout = TimeSpan.FromSeconds(5);
        private const int SIGTERM = 15;

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

            if (OperatingSystem.IsLinux() && TrySendSigterm(_process.Id))
            {
                var deadline = Environment.TickCount64 + (long)GracefulShutdownTimeout.TotalMilliseconds;
                while (!_process.HasExited && Environment.TickCount64 < deadline)
                    Thread.Sleep(50);

                if (_process.HasExited)
                    return;
            }

            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already exited between the HasExited check and Kill — fine.
            }
        }

        [SupportedOSPlatform("linux")]
        private static bool TrySendSigterm(int pid)
        {
            try
            {
                return LibcKill(pid, SIGTERM) == 0;
            }
            catch (DllNotFoundException)
            {
                // No libc to P/Invoke into (should not happen on the Linux container this runs in) — the
                // caller's bounded wait immediately falls through to the hard Kill fallback below.
                return false;
            }
        }

        [SupportedOSPlatform("linux")]
        [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
        private static extern int LibcKill(int pid, int sig);

        public void Dispose() => _process.Dispose();
    }
}
