using ALKAROS.Host.Composition.Migrations;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Experience.SecurityAdministration;
using ALKAROS.QrRelay.LocalConnector;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Tests.Composition;

// The one TLS decision every database client shares, and the connection of the restore drill.
[Collection("Host database password environment")]
public sealed class DatabaseSslTests : IDisposable
{
    private static readonly string[] Variables =
    [
        DatabaseSslSettings.ModeVariable,
        DatabaseSslSettings.RootCertVariable,
        HostDatabaseConnection.RestoreConnectionVariable,
        HostDatabaseConnection.RestoreUserVariable,
        HostDatabaseConnection.RestorePasswordFileVariable,
        "ALKAROS_DB_PASSWORD",
    ];

    private readonly Dictionary<string, string?> _original = Variables.ToDictionary(name => name, Environment.GetEnvironmentVariable);

    public DatabaseSslTests()
    {
        foreach (var name in Variables)
            Environment.SetEnvironmentVariable(name, null);
        Environment.SetEnvironmentVariable("ALKAROS_DB_PASSWORD", "test-password");
    }

    public void Dispose()
    {
        foreach (var (name, value) in _original)
            Environment.SetEnvironmentVariable(name, value);
    }

    [Theory]
    [InlineData("localhost", SslMode.Prefer)]
    [InlineData("127.0.0.1", SslMode.Prefer)]
    [InlineData("[::1]", SslMode.Prefer)]
    [InlineData("postgres", SslMode.Require)]
    [InlineData("db.example.com", SslMode.Require)]
    [InlineData("10.0.0.5", SslMode.Require)]
    public void WithoutAConfiguredModeOnlyALoopbackServerMayFallBackToPlainText(string host, SslMode expected)
    {
        Assert.Equal(expected, DatabaseSslSettings.ModeFor(host));
    }

    [Theory]
    [InlineData("disable", SslMode.Disable, "disable")]
    [InlineData("PREFER", SslMode.Prefer, "prefer")]
    [InlineData("require", SslMode.Require, "require")]
    [InlineData("verify-ca", SslMode.VerifyCA, "verify-ca")]
    [InlineData(" Verify-Full ", SslMode.VerifyFull, "verify-full")]
    public void ThePostgresStyleModeNamesMapToNpgsqlAndToPsql(string configured, SslMode expected, string psql)
    {
        Environment.SetEnvironmentVariable(DatabaseSslSettings.ModeVariable, configured);

        Assert.Equal(expected, DatabaseSslSettings.ModeFor("postgres"));
        Assert.Equal(psql, DatabaseSslSettings.PsqlMode("postgres"));
    }

    [Fact]
    public void AnUnknownModeStopsTheProcessInsteadOfFallingBackToPlainText()
    {
        Environment.SetEnvironmentVariable(DatabaseSslSettings.ModeVariable, "sometimes");

        Assert.Throws<ArgumentException>(() => DatabaseSslSettings.ModeFor("postgres"));
    }

    [Fact]
    public void TheServeConnectionStringCarriesTheModeAndTheRootCertificate()
    {
        Environment.SetEnvironmentVariable(DatabaseSslSettings.ModeVariable, "verify-full");
        Environment.SetEnvironmentVariable(DatabaseSslSettings.RootCertVariable, "/run/secrets/db_tls_ca");
        var webRoot = Path.Combine(Path.GetTempPath(), $"alkaros-ssl-{Guid.NewGuid():N}");
        Directory.CreateDirectory(webRoot);
        File.WriteAllText(Path.Combine(webRoot, "index.html"), "<!doctype html>");
        try
        {
            var options = DualScreenOptions.Parse(["--db-url", "postgresql://alkaros_app@postgres:5432/alkaros", "--web-root", webRoot, "--urls", "http://127.0.0.1:5090"]);
            var builder = new NpgsqlConnectionStringBuilder(options.ConnectionString);

            Assert.Equal(SslMode.VerifyFull, builder.SslMode);
            Assert.Equal("/run/secrets/db_tls_ca", builder.RootCertificate);
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    [Fact]
    public void ARemoteServeConnectionRequiresTlsEvenWhenNothingIsConfigured()
    {
        var webRoot = Path.Combine(Path.GetTempPath(), $"alkaros-ssl-{Guid.NewGuid():N}");
        Directory.CreateDirectory(webRoot);
        File.WriteAllText(Path.Combine(webRoot, "index.html"), "<!doctype html>");
        try
        {
            var options = DualScreenOptions.Parse(["--db-url", "postgresql://alkaros_app@postgres:5432/alkaros", "--web-root", webRoot, "--urls", "http://127.0.0.1:5090"]);

            Assert.Equal(SslMode.Require, new NpgsqlConnectionStringBuilder(options.ConnectionString).SslMode);
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    [Fact]
    public void PsqlChildProcessesGetTheSameDecision()
    {
        Environment.SetEnvironmentVariable(DatabaseSslSettings.ModeVariable, "verify-full");
        Environment.SetEnvironmentVariable(DatabaseSslSettings.RootCertVariable, "/run/secrets/db_tls_ca");

        var start = PsqlScriptRunner.CreateStartInfo(new PsqlOptions("postgresql://alkaros@postgres:5432/alkaros"));

        Assert.Equal("verify-full", start.Environment["PGSSLMODE"]);
        Assert.Equal("/run/secrets/db_tls_ca", start.Environment["PGSSLROOTCERT"]);
    }

    [Fact]
    public void TheRestoreDrillUsesItsOwnRoleWhenOneIsConfigured()
    {
        var passwordFile = Path.Combine(Path.GetTempPath(), $"alkaros-restore-pw-{Guid.NewGuid():N}");
        File.WriteAllText(passwordFile, "restore-secret\r\n");
        try
        {
            var application = new HostDatabaseConnection("Host=postgres;Database=alkaros;Username=alkaros_app;Password=app-secret");
            Assert.Equal(application.ConnectionString, application.RestoreMaintenanceConnectionString());

            Environment.SetEnvironmentVariable(HostDatabaseConnection.RestoreUserVariable, "alkaros_restore");
            Environment.SetEnvironmentVariable(HostDatabaseConnection.RestorePasswordFileVariable, passwordFile);
            var builder = new NpgsqlConnectionStringBuilder(application.RestoreMaintenanceConnectionString());

            Assert.Equal("alkaros_restore", builder.Username);
            Assert.Equal("restore-secret", builder.Password);
            Assert.Equal("postgres", builder.Host);
            Assert.Equal("alkaros", builder.Database);

            Environment.SetEnvironmentVariable(HostDatabaseConnection.RestoreConnectionVariable, "Host=elsewhere;Database=x;Username=y;Password=z");
            Assert.Equal("Host=elsewhere;Database=x;Username=y;Password=z", application.RestoreMaintenanceConnectionString());
        }
        finally
        {
            File.Delete(passwordFile);
        }
    }

    [Fact]
    public void ARestoreRoleWithoutAPasswordFileIsAConfigurationError()
    {
        Environment.SetEnvironmentVariable(HostDatabaseConnection.RestoreUserVariable, "alkaros_restore");

        Assert.Throws<InvalidOperationException>(() =>
            new HostDatabaseConnection("Host=postgres;Database=alkaros;Username=alkaros_app;Password=p").RestoreMaintenanceConnectionString());
    }
}
