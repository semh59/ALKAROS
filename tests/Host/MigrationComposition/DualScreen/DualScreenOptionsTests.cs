using ALKAROS.Host.DualScreen;
using Xunit;

namespace ALKAROS.Host.Tests.DualScreen;

[Collection("Host database password environment")]
public sealed class DualScreenOptionsTests : IDisposable
{
    private readonly string? _originalPassword = Environment.GetEnvironmentVariable("ALKAROS_DB_PASSWORD");
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"alkaros-display-{Guid.NewGuid():N}");

    public DualScreenOptionsTests()
    {
        Directory.CreateDirectory(_webRoot);
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html>");
        Environment.SetEnvironmentVariable("ALKAROS_DB_PASSWORD", "test-password");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ALKAROS_DB_PASSWORD", _originalPassword);
        Directory.Delete(_webRoot, recursive: true);
    }

    [Fact]
    public void ValidArgumentsProduceASecretBackedConnectionString()
    {
        var options = DualScreenOptions.Parse(
        [
            "--db-url", "postgresql://alkaros@localhost:5432/alkaros",
            "--web-root", _webRoot,
            "--urls", "http://127.0.0.1:5090",
        ]);

        Assert.Contains("Host=localhost", options.ConnectionString, StringComparison.Ordinal);
        Assert.Contains("Password=test-password", options.ConnectionString, StringComparison.Ordinal);
        Assert.DoesNotContain("test-password", string.Join(' ', new[] { options.WebRoot, options.Url }), StringComparison.Ordinal);
    }

    [Fact]
    public void PasswordInDatabaseUrlIsRejected()
    {
        var exception = Assert.Throws<DualScreenStartupException>(() => DualScreenOptions.Parse(
        [
            "--db-url", "postgresql://alkaros:leaked@localhost:5432/alkaros",
            "--web-root", _webRoot,
        ]));

        Assert.DoesNotContain("leaked", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HttpsUrlWithoutACertificateSourceIsRejected()
    {
        var exception = Assert.Throws<DualScreenStartupException>(() => DualScreenOptions.Parse(
        [
            "--db-url", "postgresql://alkaros@localhost:5432/alkaros",
            "--web-root", _webRoot,
            "--urls", "https://0.0.0.0:5443",
        ]));

        Assert.Contains("--self-signed-host", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SelfSignedHostRequiresAnHttpsUrl()
    {
        Assert.Throws<DualScreenStartupException>(() => DualScreenOptions.Parse(
        [
            "--db-url", "postgresql://alkaros@localhost:5432/alkaros",
            "--web-root", _webRoot,
            "--urls", "http://0.0.0.0:5080",
            "--self-signed-host", "pos.lan",
        ]));
    }

    [Fact]
    public void TlsCertificateAndKeyMustBeSuppliedTogether()
    {
        Assert.Throws<DualScreenStartupException>(() => DualScreenOptions.Parse(
        [
            "--db-url", "postgresql://alkaros@localhost:5432/alkaros",
            "--web-root", _webRoot,
            "--urls", "https://0.0.0.0:5443",
            "--tls-cert", "/etc/alkaros/tls/fullchain.pem",
        ]));
    }

    [Fact]
    public void ApiOnlyRejectsAWebRoot()
    {
        var exception = Assert.Throws<DualScreenStartupException>(() => DualScreenOptions.Parse(
        [
            "--db-url", "postgresql://alkaros@localhost:5432/alkaros",
            "--web-root", _webRoot,
            "--urls", "http://0.0.0.0:5080",
            "--api-only",
        ]));

        Assert.Contains("mutually exclusive", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CustomerDisplayOriginHeaderRequiresApiOnly()
    {
        var exception = Assert.Throws<DualScreenStartupException>(() => DualScreenOptions.Parse(
        [
            "--db-url", "postgresql://alkaros@localhost:5432/alkaros",
            "--web-root", _webRoot,
            "--urls", "http://0.0.0.0:5080",
            "--customer-display-origin-header", "X-Alkaros-Origin",
        ]));

        Assert.Contains("--api-only", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiOnlyRejectsACustomerDisplayPort()
    {
        Assert.Throws<DualScreenStartupException>(() => DualScreenOptions.Parse(
        [
            "--db-url", "postgresql://alkaros@localhost:5432/alkaros",
            "--urls", "http://0.0.0.0:5080",
            "--api-only",
            "--customer-display-urls", "http://0.0.0.0:5081",
        ]));
    }

    /// <summary>V1-RMD-139: mirrors CustomerDisplayOriginHeaderRequiresApiOnly for the NFC origin.</summary>
    [Fact]
    public void NfcOriginHeaderRequiresApiOnly()
    {
        var exception = Assert.Throws<DualScreenStartupException>(() => DualScreenOptions.Parse(
        [
            "--db-url", "postgresql://alkaros@localhost:5432/alkaros",
            "--web-root", _webRoot,
            "--urls", "http://0.0.0.0:5080",
            "--nfc-origin-header", "X-Alkaros-Origin",
        ]));

        Assert.Contains("--api-only", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>V1-RMD-139: mirrors ApiOnlyRejectsACustomerDisplayPort for the NFC origin.</summary>
    [Fact]
    public void ApiOnlyRejectsAnNfcOriginPort()
    {
        Assert.Throws<DualScreenStartupException>(() => DualScreenOptions.Parse(
        [
            "--db-url", "postgresql://alkaros@localhost:5432/alkaros",
            "--urls", "http://0.0.0.0:5080",
            "--api-only",
            "--nfc-urls", "http://0.0.0.0:5082",
        ]));
    }

    [Fact]
    public void NfcUrlsMustNotReuseTheMainPort()
    {
        var exception = Assert.Throws<DualScreenStartupException>(() => DualScreenOptions.Parse(
        [
            "--db-url", "postgresql://alkaros@localhost:5432/alkaros",
            "--web-root", _webRoot,
            "--urls", "http://0.0.0.0:5080",
            "--nfc-urls", "http://0.0.0.0:5080",
        ]));

        Assert.Contains("--nfc-urls", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NfcUrlsMustNotReuseTheCustomerDisplayPort()
    {
        var exception = Assert.Throws<DualScreenStartupException>(() => DualScreenOptions.Parse(
        [
            "--db-url", "postgresql://alkaros@localhost:5432/alkaros",
            "--web-root", _webRoot,
            "--urls", "http://0.0.0.0:5080",
            "--customer-display-urls", "http://0.0.0.0:5081",
            "--nfc-urls", "http://0.0.0.0:5081",
        ]));

        Assert.Contains("--customer-display-urls", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Relay scope hardening (2026-09-09): --nfc-loopback-origin's reasoning is specific to the --api-only/compose.yaml deployment topology, so it requires --api-only same as the header-based signals.</summary>
    [Fact]
    public void NfcLoopbackOriginRequiresApiOnly()
    {
        var exception = Assert.Throws<DualScreenStartupException>(() => DualScreenOptions.Parse(
        [
            "--db-url", "postgresql://alkaros@localhost:5432/alkaros",
            "--web-root", _webRoot,
            "--urls", "http://0.0.0.0:5080",
            "--nfc-loopback-origin",
        ]));

        Assert.Contains("--api-only", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>V12-CWB-001: same reasoning as --nfc-loopback-origin — this flag only makes sense in --api-only mode (the non-api-only pipeline already serves everything from --web-root).</summary>
    [Fact]
    public void QrWebRootRequiresApiOnly()
    {
        var exception = Assert.Throws<DualScreenStartupException>(() => DualScreenOptions.Parse(
        [
            "--db-url", "postgresql://alkaros@localhost:5432/alkaros",
            "--web-root", _webRoot,
            "--urls", "http://0.0.0.0:5080",
            "--qr-web-root", _webRoot,
        ]));

        Assert.Contains("--api-only", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void QrWebRootMustContainIndexHtml()
    {
        var emptyDirectory = Path.Combine(Path.GetTempPath(), $"alkaros-qr-empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(emptyDirectory);
        try
        {
            var exception = Assert.Throws<DualScreenStartupException>(() => DualScreenOptions.Parse(
            [
                "--db-url", "postgresql://alkaros@localhost:5432/alkaros",
                "--urls", "http://0.0.0.0:5080",
                "--api-only",
                "--qr-web-root", emptyDirectory,
            ]));

            Assert.Contains("index.html", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(emptyDirectory, recursive: true);
        }
    }

    [Fact]
    public void AValidQrWebRootIsResolvedToAFullPath()
    {
        var options = DualScreenOptions.Parse(
        [
            "--db-url", "postgresql://alkaros@localhost:5432/alkaros",
            "--urls", "http://0.0.0.0:5080",
            "--api-only",
            "--qr-web-root", _webRoot,
        ]);

        Assert.Equal(Path.GetFullPath(_webRoot), options.QrWebRoot);
    }

    [Fact]
    public void ValidNfcUrlsParseIntoTheirOwnPortsAndTheFullListenSet()
    {
        var options = DualScreenOptions.Parse(
        [
            "--db-url", "postgresql://alkaros@localhost:5432/alkaros",
            "--web-root", _webRoot,
            "--urls", "http://0.0.0.0:5080",
            "--nfc-urls", "http://0.0.0.0:5082",
        ]);

        Assert.Equal(5082, Assert.Single(options.NfcOriginPorts));
        Assert.Contains("http://0.0.0.0:5080", options.AllListenUrls, StringComparison.Ordinal);
        Assert.Contains("http://0.0.0.0:5082", options.AllListenUrls, StringComparison.Ordinal);
    }

    [Fact]
    public void DualHttpAndHttpsBindingWithSelfSignedHostParses()
    {
        var options = DualScreenOptions.Parse(
        [
            "--db-url", "postgresql://alkaros@localhost:5432/alkaros",
            "--web-root", _webRoot,
            "--urls", "http://0.0.0.0:5080;https://0.0.0.0:5443",
            "--self-signed-host", "pos.lan",
            "--trusted-network", "172.16.0.0/12",
        ]);

        Assert.True(options.ServesHttpsDirectly);
        Assert.Equal("pos.lan", options.SelfSignedTlsHost);
        Assert.Contains("http://0.0.0.0:5080/", options.Url, StringComparison.Ordinal);
        Assert.Contains("https://0.0.0.0:5443/", options.Url, StringComparison.Ordinal);
    }
}
