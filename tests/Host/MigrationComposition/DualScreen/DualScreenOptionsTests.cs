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
}
