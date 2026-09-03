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
