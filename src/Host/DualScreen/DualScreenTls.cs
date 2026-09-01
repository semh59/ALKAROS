using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ALKAROS.Host.DualScreen;

/// <summary>
/// Resolves the TLS server certificate Kestrel uses when the host terminates
/// HTTPS itself (V1-RMD-096). Two sources, in order:
/// <list type="number">
///   <item>a mounted PEM certificate + key (<c>--tls-cert</c> / <c>--tls-key</c>) — an
///   approved public or internal-CA certificate;</item>
///   <item>a self-signed certificate generated for <c>--self-signed-host</c>, cached
///   under <c>&lt;content-root&gt;/tls</c> so it stays stable across restarts (once an
///   operator installs it on a device it keeps matching).</item>
/// </list>
/// The self-signed path exists so the container image gives waiter phones a
/// secure context (service worker / offline queue) even without the reverse
/// proxy in front, and so a proxy misconfiguration cannot silently disable
/// offline mode.
/// </summary>
public static class DualScreenTls
{
    private const int SelfSignedValidityDays = 825;
    private const int RenewWhenWithinDays = 30;

    public static X509Certificate2? Resolve(DualScreenOptions options, string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.TlsCertificatePath is { } certPath && options.TlsCertificateKeyPath is { } keyPath)
        {
            if (!File.Exists(certPath))
                throw new DualScreenStartupException($"--tls-cert file not found: {certPath}");
            if (!File.Exists(keyPath))
                throw new DualScreenStartupException($"--tls-key file not found: {keyPath}");

            try
            {
                using var pem = X509Certificate2.CreateFromPemFile(certPath, keyPath);
                // Round-trip through PKCS#12 so Kestrel gets a certificate with a
                // persistable private key on every platform.
                return new X509Certificate2(pem.Export(X509ContentType.Pkcs12));
            }
            catch (CryptographicException exception)
            {
                throw new DualScreenStartupException($"--tls-cert/--tls-key could not be loaded: {exception.Message}");
            }
        }

        if (!options.ServesHttpsDirectly || string.IsNullOrWhiteSpace(options.SelfSignedTlsHost))
            return null;

        return ResolveSelfSigned(options.SelfSignedTlsHost, contentRootPath);
    }

    private static X509Certificate2 ResolveSelfSigned(string host, string contentRootPath)
    {
        var cacheDirectory = Path.Combine(contentRootPath, "tls");
        var safeHost = string.Concat(host.Select(c => char.IsLetterOrDigit(c) || c is '-' or '.' ? c : '_'));
        var cachePath = Path.Combine(cacheDirectory, $"alkaros-selfsigned-{safeHost}.pfx");

        if (TryLoadCached(cachePath) is { } cached)
            return cached;

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={host}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            critical: true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new("1.3.6.1.5.5.7.3.1") }, // id-kp-serverAuth
            critical: false));

        var sanBuilder = new SubjectAlternativeNameBuilder();
        var dnsNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ipAddresses = new HashSet<IPAddress>();
        AddSan(sanBuilder, host, dnsNames, ipAddresses);
        AddSan(sanBuilder, "localhost", dnsNames, ipAddresses);
        foreach (var loopback in new[] { IPAddress.Loopback, IPAddress.IPv6Loopback })
        {
            if (ipAddresses.Add(loopback))
                sanBuilder.AddIpAddress(loopback);
        }

        request.CertificateExtensions.Add(sanBuilder.Build());

        var now = DateTimeOffset.UtcNow;
        using var generated = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(SelfSignedValidityDays));
        var pfx = generated.Export(X509ContentType.Pkcs12);

        TryPersist(cacheDirectory, cachePath, pfx);
        return new X509Certificate2(pfx, (string?)null, X509KeyStorageFlags.Exportable);
    }

    private static X509Certificate2? TryLoadCached(string cachePath)
    {
        try
        {
            if (!File.Exists(cachePath))
                return null;

            var cached = new X509Certificate2(File.ReadAllBytes(cachePath), (string?)null, X509KeyStorageFlags.Exportable);
            if (cached.NotAfter.ToUniversalTime() > DateTime.UtcNow.AddDays(RenewWhenWithinDays))
                return cached;

            cached.Dispose();
            return null;
        }
        catch (Exception exception) when (exception is CryptographicException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void TryPersist(string cacheDirectory, string cachePath, byte[] pfx)
    {
        try
        {
            Directory.CreateDirectory(cacheDirectory);
            File.WriteAllBytes(cachePath, pfx);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(cachePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A read-only content root is fine; the in-memory certificate still
            // serves this process. It will be regenerated on the next start.
        }
    }

    private static void AddSan(
        SubjectAlternativeNameBuilder builder,
        string host,
        HashSet<string> dnsNames,
        HashSet<IPAddress> ipAddresses)
    {
        if (IPAddress.TryParse(host, out var address))
        {
            if (ipAddresses.Add(address))
                builder.AddIpAddress(address);
        }
        else if (dnsNames.Add(host))
        {
            builder.AddDnsName(host);
        }
    }
}
