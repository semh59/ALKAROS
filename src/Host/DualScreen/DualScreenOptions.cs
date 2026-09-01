using System.Net;
using Npgsql;
using ForwardedNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

namespace ALKAROS.Host.DualScreen;

public sealed class DualScreenStartupException : Exception
{
    public DualScreenStartupException(string message)
        : base(message)
    {
    }
}

public sealed record DualScreenOptions(
    string ConnectionString,
    string WebRoot,
    string Url,
    IReadOnlyList<IPAddress>? TrustedProxies = null,
    IReadOnlyList<ForwardedNetwork>? TrustedNetworks = null,
    bool AllowInsecureLoopbackDevelopment = false,
    string? TlsCertificatePath = null,
    string? TlsCertificateKeyPath = null,
    string? SelfSignedTlsHost = null)
{
    private const string PasswordEnvironmentVariable = "ALKAROS_DB_PASSWORD";

    /// <summary>
    /// True when at least one <c>--urls</c> endpoint is <c>https://</c>. When set,
    /// Kestrel terminates TLS itself using either the mounted certificate
    /// (<see cref="TlsCertificatePath"/> / <see cref="TlsCertificateKeyPath"/>) or a
    /// self-signed certificate generated for <see cref="SelfSignedTlsHost"/>. The
    /// plain-HTTP endpoint stays available for a trusted TLS-terminating reverse
    /// proxy (Caddy) that sets <c>X-Forwarded-Proto: https</c>.
    /// </summary>
    public bool ServesHttpsDirectly =>
        Url.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(u => u.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

    public static DualScreenOptions Parse(string[] args)
    {
        string? databaseUrl = null;
        string? webRoot = null;
        var url = "http://127.0.0.1:5080";
        var trustedProxies = new List<IPAddress>();
        var trustedNetworks = new List<ForwardedNetwork>();
        var allowInsecureLoopbackDevelopment = false;
        string? tlsCertPath = null;
        string? tlsKeyPath = null;
        string? selfSignedHost = null;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--db-url" when index + 1 < args.Length && databaseUrl is null:
                    databaseUrl = args[++index];
                    break;
                case "--web-root" when index + 1 < args.Length && webRoot is null:
                    webRoot = args[++index];
                    break;
                case "--urls" when index + 1 < args.Length:
                    url = args[++index];
                    break;
                case "--trusted-proxy" when index + 1 < args.Length:
                    trustedProxies.Add(ParseTrustedProxy(args[++index]));
                    break;
                case "--trusted-network" when index + 1 < args.Length:
                    trustedNetworks.Add(ParseTrustedNetwork(args[++index]));
                    break;
                case "--tls-cert" when index + 1 < args.Length && tlsCertPath is null:
                    tlsCertPath = args[++index];
                    break;
                case "--tls-key" when index + 1 < args.Length && tlsKeyPath is null:
                    tlsKeyPath = args[++index];
                    break;
                case "--self-signed-host" when index + 1 < args.Length && selfSignedHost is null:
                    selfSignedHost = args[++index].Trim();
                    break;
                case "--allow-insecure-loopback-development" when !allowInsecureLoopbackDevelopment:
                    allowInsecureLoopbackDevelopment = true;
                    break;
                default:
                    throw new DualScreenStartupException("Invalid dual-screen serve arguments.");
            }
        }

        if (string.IsNullOrWhiteSpace(databaseUrl) || string.IsNullOrWhiteSpace(webRoot))
            throw new DualScreenStartupException("Both --db-url and --web-root are required.");

        var password = Environment.GetEnvironmentVariable(PasswordEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(password))
            throw new DualScreenStartupException($"{PasswordEnvironmentVariable} is required.");

        if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != "postgresql" && uri.Scheme != "postgres")
            || string.IsNullOrWhiteSpace(uri.Host)
            || string.IsNullOrWhiteSpace(uri.AbsolutePath.Trim('/')))
        {
            throw new DualScreenStartupException("--db-url must be a PostgreSQL URL with host and database.");
        }

        var userInfo = uri.UserInfo.Split(':', 2);
        if (userInfo.Length != 1 || string.IsNullOrWhiteSpace(userInfo[0]))
            throw new DualScreenStartupException("--db-url must contain a username and must not contain a password.");

        var resolvedWebRoot = Path.GetFullPath(webRoot);
        if (!File.Exists(Path.Combine(resolvedWebRoot, "index.html")))
            throw new DualScreenStartupException("--web-root must contain the built index.html file.");

        var listenUris = ParseListenUrls(url);
        var hasHttps = listenUris.Any(u => u.Scheme == Uri.UriSchemeHttps);
        var hasHttp = listenUris.Any(u => u.Scheme == Uri.UriSchemeHttp);

        if (allowInsecureLoopbackDevelopment
            && listenUris.Any(u => u.Scheme != Uri.UriSchemeHttp || !IsLoopbackHost(u.Host)))
        {
            throw new DualScreenStartupException(
                "--allow-insecure-loopback-development requires only HTTP loopback --urls addresses.");
        }

        var mountedCert = tlsCertPath is not null || tlsKeyPath is not null;
        if (mountedCert && (tlsCertPath is null || tlsKeyPath is null))
            throw new DualScreenStartupException("--tls-cert and --tls-key must be supplied together.");
        if (mountedCert && !hasHttps)
            throw new DualScreenStartupException("--tls-cert/--tls-key require an https:// --urls address.");
        if (selfSignedHost is not null && !hasHttps)
            throw new DualScreenStartupException("--self-signed-host requires an https:// --urls address.");
        if (hasHttps && !mountedCert && string.IsNullOrWhiteSpace(selfSignedHost))
        {
            throw new DualScreenStartupException(
                "an https:// --urls address requires either --tls-cert/--tls-key or --self-signed-host.");
        }

        if (!hasHttp && !hasHttps)
            throw new DualScreenStartupException("--urls must contain at least one absolute HTTP or HTTPS URL.");

        var connectionString = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = password,
            ApplicationName = "ALKAROS.DualScreen",
            Pooling = true,
        }.ConnectionString;

        return new DualScreenOptions(
            connectionString,
            resolvedWebRoot,
            string.Join(';', listenUris.Select(u => u.ToString())),
            trustedProxies,
            trustedNetworks,
            allowInsecureLoopbackDevelopment,
            tlsCertPath,
            tlsKeyPath,
            string.IsNullOrWhiteSpace(selfSignedHost) ? null : selfSignedHost);
    }

    private static List<Uri> ParseListenUrls(string value)
    {
        var parts = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            throw new DualScreenStartupException("--urls must contain at least one absolute HTTP or HTTPS URL.");

        var result = new List<Uri>();
        foreach (var part in parts)
        {
            if (!Uri.TryCreate(part, UriKind.Absolute, out var listenUri)
                || (listenUri.Scheme != Uri.UriSchemeHttp && listenUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new DualScreenStartupException("--urls must contain absolute HTTP or HTTPS URLs.");
            }

            result.Add(listenUri);
        }

        return result;
    }

    private static IPAddress ParseTrustedProxy(string value)
    {
        if (!IPAddress.TryParse(value, out var address))
            throw new DualScreenStartupException("--trusted-proxy must contain an IP address.");
        return address;
    }

    private static ForwardedNetwork ParseTrustedNetwork(string value)
    {
        var parts = value.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !IPAddress.TryParse(parts[0], out var prefix)
            || !int.TryParse(parts[1], out var prefixLength)
            || prefixLength < 0
            || prefixLength > (prefix.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128))
        {
            throw new DualScreenStartupException("--trusted-network must contain an IPv4 or IPv6 CIDR range.");
        }

        try
        {
            return new ForwardedNetwork(prefix, prefixLength);
        }
        catch (ArgumentException)
        {
            throw new DualScreenStartupException("--trusted-network must contain a canonical CIDR range.");
        }
    }

    private static bool IsLoopbackHost(string host)
        => string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));
}
