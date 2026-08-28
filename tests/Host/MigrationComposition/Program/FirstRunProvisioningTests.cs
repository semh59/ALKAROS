using ALKAROS.Host.Composition;
using Xunit;

namespace ALKAROS.Host.Tests.Program;

[Collection("Host database password environment")]
public sealed class FirstRunProvisioningTests : IDisposable
{
    private static readonly string[] Variables =
    [
        "ALKAROS_DB_PASSWORD",
        "ALKAROS_BOOTSTRAP_USERNAME",
        "ALKAROS_BOOTSTRAP_DISPLAY_NAME",
        "ALKAROS_BOOTSTRAP_PASSWORD",
    ];

    private readonly Dictionary<string, string?> _original = Variables.ToDictionary(
        variable => variable,
        Environment.GetEnvironmentVariable);

    [Fact]
    public void MissingBootstrapPasswordFailsClosed()
    {
        SetValidEnvironment();
        Environment.SetEnvironmentVariable("ALKAROS_BOOTSTRAP_PASSWORD", null);

        var result = ALKAROS.Host.Program.Main(
            ["provision-manager", "--db-url", "postgresql://alkaros@localhost:5432/alkaros"]);

        Assert.Equal((int)HostExitCode.StartupFailed, result);
    }

    [Fact]
    public void WhitespacePasswordFailsWithoutWritingTheSecret()
    {
        const string secret = "not allowed secret";
        SetValidEnvironment();
        Environment.SetEnvironmentVariable("ALKAROS_BOOTSTRAP_PASSWORD", secret);
        using var error = new StringWriter();
        var originalError = Console.Error;

        try
        {
            Console.SetError(error);
            var result = ALKAROS.Host.Program.Main(
                ["provision-manager", "--db-url", "postgresql://alkaros@localhost:5432/alkaros"]);

            Assert.Equal((int)HostExitCode.StartupFailed, result);
            Assert.DoesNotContain(secret, error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void PasswordInDatabaseUrlIsRejectedBeforeConnecting()
    {
        SetValidEnvironment();

        var result = ALKAROS.Host.Program.Main(
            ["provision-manager", "--db-url", "postgresql://alkaros:forbidden@localhost:5432/alkaros"]);

        Assert.Equal((int)HostExitCode.StartupFailed, result);
    }

    public void Dispose()
    {
        foreach (var item in _original)
            Environment.SetEnvironmentVariable(item.Key, item.Value);
    }

    private static void SetValidEnvironment()
    {
        Environment.SetEnvironmentVariable("ALKAROS_DB_PASSWORD", "database-secret");
        Environment.SetEnvironmentVariable("ALKAROS_BOOTSTRAP_USERNAME", "admin");
        Environment.SetEnvironmentVariable("ALKAROS_BOOTSTRAP_DISPLAY_NAME", "ALKAROS Manager");
        Environment.SetEnvironmentVariable("ALKAROS_BOOTSTRAP_PASSWORD", "manager-secret-42");
    }
}
