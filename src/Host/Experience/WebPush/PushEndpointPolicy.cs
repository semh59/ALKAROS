using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace ALKAROS.Host.Experience.WebPush;

/// <summary>
/// Which addresses a push subscription may point the server at. A registered endpoint is a URL this host
/// POSTs to, so without limits any signed-in session could aim it at an internal service.
/// </summary>
public static class PushEndpointPolicy
{
    // The UNIQUE index on the endpoint column cannot hold much more than this.
    public const int MaximumEndpointLength = 2048;

    /// <summary>
    /// A push service is reached by name over https on the default port. IP literals, credentials in the URL
    /// and single-label names are refused here; names that resolve to a non-public address are refused again
    /// when the connection is made (<see cref="ConnectPublicOnlyAsync"/>).
    /// </summary>
    public static bool IsAcceptable(string endpoint)
        => endpoint.Length <= MaximumEndpointLength
           && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
           && uri.Scheme == Uri.UriSchemeHttps
           && uri.IsDefaultPort
           && uri.UserInfo.Length == 0
           && uri.HostNameType == UriHostNameType.Dns
           && uri.Host.Contains('.', StringComparison.Ordinal);

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
            return false;

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return !(bytes[0] is 0 or 10 or >= 224
                     || (bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
                     || (bytes[0] == 169 && bytes[1] == 254)
                     || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                     || (bytes[0] == 192 && bytes[1] == 168));
        }

        return address.AddressFamily == AddressFamily.InterNetworkV6
               && !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast
                    || (bytes[0] & 0xFE) == 0xFC);
    }

    /// <summary>The handler behind the push HTTP client: public addresses only, no redirects, refreshed connections.</summary>
    public static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        ConnectCallback = ConnectPublicOnlyAsync,
    };

    /// <summary>
    /// Connect callback for the push HTTP client: resolves the name itself and only ever connects to a public
    /// address, so a name that points inside the network (or is switched to do so after registration) fails.
    /// </summary>
    public static async ValueTask<Stream> ConnectPublicOnlyAsync(
        SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = (await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken))
            .Where(IsPublicAddress)
            .ToArray();
        if (addresses.Length == 0)
            throw new HttpRequestException("The push endpoint does not resolve to a public address.");

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
