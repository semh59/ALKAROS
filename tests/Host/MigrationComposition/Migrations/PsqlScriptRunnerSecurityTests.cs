using ALKAROS.Host.Composition.Migrations;
using Xunit;

namespace ALKAROS.Host.Tests.Migrations;

public sealed class PsqlScriptRunnerSecurityTests
{
    [Fact]
    public void RedactMasksPlaintextPasswordInConnectionString()
    {
        var raw = "psql: error: connection to server at \"localhost\", port 5432 failed: FATAL: password authentication failed for user \"postgres\" password=supersecret";
        var redacted = PsqlScriptRunner.Redact(raw);

        Assert.DoesNotContain("supersecret", redacted, StringComparison.Ordinal);
        Assert.Contains("password=***", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactMasksPostgresUrlCredentials()
    {
        var raw = "FATAL: could not connect to postgresql://admin:MySecretPassword123@db.example.com:5432/alkaros_db";
        var redacted = PsqlScriptRunner.Redact(raw);

        Assert.DoesNotContain("MySecretPassword123", redacted, StringComparison.Ordinal);
        Assert.Contains("postgresql://admin:***@db.example.com:5432/alkaros_db", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactPreservesNonSensitiveText()
    {
        var raw = "ERROR: relation \"orders\" does not exist at character 15";
        var redacted = PsqlScriptRunner.Redact(raw);

        Assert.Equal(raw, redacted);
    }
}
