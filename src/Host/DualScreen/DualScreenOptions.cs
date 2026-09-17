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
    string? SelfSignedTlsHost = null,
    string? CustomerDisplayUrl = null,
    // True when the host runs behind a static-serving reverse proxy and only
    // exposes the JSON API + hubs (serve --api-only): the static-file pipeline
    // and SPA fallback are not registered and --web-root is not required. The
    // proxy serves the PosTerminal / WaiterPwa / Cashier bundles directly.
    bool ApiOnly = false,
    // Name of the request header the trusted reverse proxy sets to "display" on
    // the customer-display virtual host. In --api-only mode this replaces the
    // dedicated listen port as the customer-display origin signal, keeping the
    // B-4 isolation (the display origin sees only display routes; the main
    // origin refuses them). Honoured only from a --trusted-network / --trusted-
    // proxy peer.
    string? CustomerDisplayOriginHeader = null,
    // V1-RMD-139: found by an independent audit (2026-09-09) — the NFC
    // customer ordering page (/nfc/{tableId}) shared the exact same origin
    // as the cashier/admin bundle, so an anonymous customer's phone could
    // reach every non-NFC API too (server-side authorization still refused
    // those calls, but the origin gave a false sense that "the NFC surface"
    // was contained). Mirrors CustomerDisplayUrl/CustomerDisplayOriginHeader
    // (deep-analysis finding B-4) exactly, one origin-isolation mechanism
    // per anonymous/single-purpose surface.
    string? NfcOriginUrl = null,
    string? NfcOriginHeader = null,
    // V12-QRO-002/relay scope hardening (2026-09-09): the Cloudflare Tunnel
    // connector (cloudflared, ALKAROS.QrRelay.LocalConnector) originally ran
    // alongside this process in the same container and reached it over
    // loopback (RelayProvisioningService's own LocalOriginService was
    // literally "http://localhost:5080") — a real, structural signal
    // distinct from every other caller: Caddy (the LAN-facing reverse
    // proxy) always arrives over the Docker network, on the container's
    // own interface, never loopback, because it runs in a different
    // container. The `api` service publishes no port at all (compose.yaml)
    // so nothing outside this container could reach 5080 to forge a
    // loopback-looking connection. Opt-in and requires --api-only, same as
    // the header-based signals above, since the whole reasoning is
    // specific to that deployment topology (compose.yaml's actual `api`
    // service). Still honoured (a genuine loopback connection still
    // qualifies) but V12-QRT-005 moved the connector into its own
    // container, so production traffic no longer arrives this way — see
    // <see cref="NfcTrustedNetworks"/>.
    bool NfcLoopbackOriginTrusted = false,
    // V12-QRT-005: the relay connector's own container (no longer sharing
    // this process's loopback, see NfcLoopbackOriginTrusted's own updated
    // doc comment) reaches this process over a small, DEDICATED Docker
    // network (compose.yaml's `relay-internal`, joined only by `api` and
    // `connector` — not `web`, not `postgres`, not any other service). That
    // narrowness is the actual trust argument, the same shape as the
    // loopback one it replaces: nothing outside this specific two-container
    // network can ever present a source address inside it, the same way
    // nothing outside the container could forge loopback before. The
    // generic, broader --trusted-network allowlist (ForwardedHeaders'
    // KnownNetworks) is deliberately NOT reused for this — that list also
    // covers `postgres`/`migrate`/`provision`'s network, which have no
    // business satisfying an NFC-relay transport exemption.
    IReadOnlyList<ForwardedNetwork>? NfcTrustedNetworks = null,
    // V12-CWB-001: found while wiring the QR customer page — in --api-only
    // mode this process serves no static files at all (the reverse proxy
    // does, per ApiOnly's own doc comment), but the Cloudflare Tunnel
    // connector reaches this process directly over loopback and never goes
    // through that proxy (RelayProvisioningService.LocalOriginService).
    // Without this, a QR customer's phone had no way to ever load the menu
    // page's HTML/CSS/JS through the actual public relay — only the JSON
    // API was reachable. Requires --api-only, same reasoning as the origin
    // header flags above; unset, this process serves no static files at
    // all, exactly as before.
    string? QrWebRoot = null,
    // V1-RMD-141: the symmetric gap for NFC, found while checking QR/NFC
    // parity after V12-CWB-001/002 shipped — NFC's own customer page
    // (`/nfc/{tableId}`) is built as part of PosTerminal's bundle, served
    // only by the reverse proxy (`web`), which the Cloudflare Tunnel
    // connector's loopback connection never reaches either. Unlike
    // QrWebRoot, this serves a small dedicated build (nfc.html/
    // vite.nfc.config.ts) rather than the whole PosTerminal SPA, so a
    // relay-connected customer's origin never carries Cashier/
    // RelaySettings/ReservationStation's own code or route names at all —
    // the same isolation goal V1-RMD-139's origin gate already protects at
    // the API layer, now also true of the static bundle itself.
    string? NfcWebRoot = null)
{

    /// <summary>
    /// Local TCP ports that belong to the customer-display origin. A request
    /// arriving on one of these ports is restricted to the display route
    /// allowlist; requests on the main ports are refused the display-only
    /// routes. Empty when <see cref="CustomerDisplayUrl"/> is not configured, in
    /// which case a single origin serves everything as before
    /// (deep-analysis finding B-4).
    /// </summary>
    public IReadOnlyCollection<int> CustomerDisplayPorts =>
        string.IsNullOrWhiteSpace(CustomerDisplayUrl)
            ? Array.Empty<int>()
            : CustomerDisplayUrl
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(u => new Uri(u).Port)
                .ToHashSet();

    /// <summary>V1-RMD-139: same shape as <see cref="CustomerDisplayPorts"/>, for the NFC origin.</summary>
    public IReadOnlyCollection<int> NfcOriginPorts =>
        string.IsNullOrWhiteSpace(NfcOriginUrl)
            ? Array.Empty<int>()
            : NfcOriginUrl
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(u => new Uri(u).Port)
                .ToHashSet();

    /// <summary>The main <c>--urls</c> plus any <c>--customer-display-urls</c>/<c>--nfc-urls</c>, the full Kestrel listen set.</summary>
    public string AllListenUrls => string.Join(
        ';',
        new[] { Url, CustomerDisplayUrl, NfcOriginUrl }.Where(u => !string.IsNullOrWhiteSpace(u)));

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
        AllListenUrls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
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
        string? customerDisplayUrls = null;
        var apiOnly = false;
        string? customerDisplayOriginHeader = null;
        string? nfcUrls = null;
        string? nfcOriginHeader = null;
        var nfcLoopbackOriginTrusted = false;
        var nfcTrustedNetworks = new List<ForwardedNetwork>();
        string? qrWebRoot = null;
        string? nfcWebRoot = null;

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
                case "--api-only" when !apiOnly:
                    apiOnly = true;
                    break;
                case "--customer-display-urls" when index + 1 < args.Length && customerDisplayUrls is null:
                    customerDisplayUrls = args[++index];
                    break;
                case "--customer-display-origin-header" when index + 1 < args.Length && customerDisplayOriginHeader is null:
                    customerDisplayOriginHeader = args[++index].Trim();
                    break;
                case "--nfc-urls" when index + 1 < args.Length && nfcUrls is null:
                    nfcUrls = args[++index];
                    break;
                case "--nfc-origin-header" when index + 1 < args.Length && nfcOriginHeader is null:
                    nfcOriginHeader = args[++index].Trim();
                    break;
                case "--nfc-loopback-origin" when !nfcLoopbackOriginTrusted:
                    nfcLoopbackOriginTrusted = true;
                    break;
                case "--nfc-trusted-network" when index + 1 < args.Length:
                    nfcTrustedNetworks.Add(ParseTrustedNetwork(args[++index]));
                    break;
                case "--qr-web-root" when index + 1 < args.Length && qrWebRoot is null:
                    qrWebRoot = args[++index];
                    break;
                case "--nfc-web-root" when index + 1 < args.Length && nfcWebRoot is null:
                    nfcWebRoot = args[++index];
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

        if (string.IsNullOrWhiteSpace(databaseUrl))
            throw new DualScreenStartupException("--db-url is required.");
        if (!apiOnly && string.IsNullOrWhiteSpace(webRoot))
            throw new DualScreenStartupException("--web-root is required unless --api-only is set.");
        if (apiOnly && !string.IsNullOrWhiteSpace(webRoot))
            throw new DualScreenStartupException("--web-root and --api-only are mutually exclusive; the reverse proxy serves the static bundles.");
        if (apiOnly && !string.IsNullOrWhiteSpace(customerDisplayUrls))
            throw new DualScreenStartupException("--customer-display-urls and --api-only are mutually exclusive; use --customer-display-origin-header instead.");
        if (customerDisplayOriginHeader is not null)
        {
            if (!apiOnly)
                throw new DualScreenStartupException("--customer-display-origin-header requires --api-only.");
            if (customerDisplayOriginHeader.Length == 0
                || !customerDisplayOriginHeader.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
            {
                throw new DualScreenStartupException("--customer-display-origin-header must be a non-empty token of ASCII letters, digits and '-'.");
            }
        }
        if (apiOnly && !string.IsNullOrWhiteSpace(nfcUrls))
            throw new DualScreenStartupException("--nfc-urls and --api-only are mutually exclusive; use --nfc-origin-header instead.");
        if (nfcOriginHeader is not null)
        {
            if (!apiOnly)
                throw new DualScreenStartupException("--nfc-origin-header requires --api-only.");
            if (nfcOriginHeader.Length == 0
                || !nfcOriginHeader.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
            {
                throw new DualScreenStartupException("--nfc-origin-header must be a non-empty token of ASCII letters, digits and '-'.");
            }
        }
        if (nfcLoopbackOriginTrusted && !apiOnly)
            throw new DualScreenStartupException("--nfc-loopback-origin requires --api-only.");
        if (nfcTrustedNetworks.Count > 0 && !apiOnly)
            throw new DualScreenStartupException("--nfc-trusted-network requires --api-only.");
        if (qrWebRoot is not null && !apiOnly)
            throw new DualScreenStartupException("--qr-web-root requires --api-only.");
        if (nfcWebRoot is not null && !apiOnly)
            throw new DualScreenStartupException("--nfc-web-root requires --api-only.");

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

        var resolvedWebRoot = string.Empty;
        if (!apiOnly)
        {
            resolvedWebRoot = Path.GetFullPath(webRoot!);
            if (!File.Exists(Path.Combine(resolvedWebRoot, "index.html")))
                throw new DualScreenStartupException("--web-root must contain the built index.html file.");
        }

        string? resolvedQrWebRoot = null;
        if (qrWebRoot is not null)
        {
            resolvedQrWebRoot = Path.GetFullPath(qrWebRoot);
            if (!File.Exists(Path.Combine(resolvedQrWebRoot, "index.html")))
                throw new DualScreenStartupException("--qr-web-root must contain the built index.html file.");
        }

        string? resolvedNfcWebRoot = null;
        if (nfcWebRoot is not null)
        {
            resolvedNfcWebRoot = Path.GetFullPath(nfcWebRoot);
            if (!File.Exists(Path.Combine(resolvedNfcWebRoot, "index.html")))
                throw new DualScreenStartupException("--nfc-web-root must contain the built index.html file.");
        }

        var listenUris = ParseListenUrls(url);
        var displayUris = string.IsNullOrWhiteSpace(customerDisplayUrls)
            ? new List<Uri>()
            : ParseListenUrls(customerDisplayUrls);
        var nfcUris = string.IsNullOrWhiteSpace(nfcUrls)
            ? new List<Uri>()
            : ParseListenUrls(nfcUrls);
        var allUris = listenUris.Concat(displayUris).Concat(nfcUris).ToList();
        var hasHttps = allUris.Any(u => u.Scheme == Uri.UriSchemeHttps);
        var hasHttp = allUris.Any(u => u.Scheme == Uri.UriSchemeHttp);

        if (displayUris.Count > 0 && listenUris.Select(u => u.Port).Intersect(displayUris.Select(u => u.Port)).Any())
        {
            throw new DualScreenStartupException(
                "--customer-display-urls must not reuse a --urls port; the display origin needs its own port.");
        }
        if (nfcUris.Count > 0 && listenUris.Select(u => u.Port).Intersect(nfcUris.Select(u => u.Port)).Any())
        {
            throw new DualScreenStartupException(
                "--nfc-urls must not reuse a --urls port; the NFC origin needs its own port.");
        }
        if (nfcUris.Count > 0 && displayUris.Select(u => u.Port).Intersect(nfcUris.Select(u => u.Port)).Any())
        {
            throw new DualScreenStartupException(
                "--nfc-urls must not reuse a --customer-display-urls port; each origin needs its own port.");
        }

        if (allowInsecureLoopbackDevelopment
            && allUris.Any(u => u.Scheme != Uri.UriSchemeHttp || !IsLoopbackHost(u.Host)))
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
            string.IsNullOrWhiteSpace(selfSignedHost) ? null : selfSignedHost,
            displayUris.Count == 0 ? null : string.Join(';', displayUris.Select(u => u.ToString())),
            apiOnly,
            customerDisplayOriginHeader,
            nfcUris.Count == 0 ? null : string.Join(';', nfcUris.Select(u => u.ToString())),
            nfcOriginHeader,
            nfcLoopbackOriginTrusted,
            nfcTrustedNetworks,
            resolvedQrWebRoot,
            resolvedNfcWebRoot);
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
