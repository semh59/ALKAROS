namespace ALKAROS.QrRelay.LocalConnector;

public interface ICloudflaredProcessFactory
{
    /// <summary>
    /// Starts <c>cloudflared tunnel run</c> with the token passed via the
    /// <c>TUNNEL_TOKEN</c> environment variable (a documented cloudflared
    /// option — <c>--token value ... [$TUNNEL_TOKEN]</c>), never as a CLI
    /// argument, so it never appears in a process listing (`ps`, `/proc/&lt;pid&gt;/cmdline`).
    /// </summary>
    ICloudflaredProcess Start(string tunnelToken);
}
