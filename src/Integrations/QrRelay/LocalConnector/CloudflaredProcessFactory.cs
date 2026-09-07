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
