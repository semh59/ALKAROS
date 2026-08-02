using ALKAROS.Host.Composition;
using Xunit;

namespace ALKAROS.Host.Tests.Program;

[CollectionDefinition("Host database password environment", DisableParallelization = true)]
public sealed class HostDatabasePasswordEnvironment;

[Collection("Host database password environment")]
public sealed class ProgramArgumentTests
{
    [Fact]
    public void MissingDatabasePasswordReturnsStartupFailure()
    {
        var originalPassword = Environment.GetEnvironmentVariable("ALKAROS_DB_PASSWORD");
        try
        {
            Environment.SetEnvironmentVariable("ALKAROS_DB_PASSWORD", null);

            var exitCode = ALKAROS.Host.Program.Main(RequiredArguments());

            Assert.Equal((int)HostExitCode.StartupFailed, exitCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ALKAROS_DB_PASSWORD", originalPassword);
        }
    }

    [Fact]
    public void CommandLinePasswordIsRejectedWithoutWritingTheSecret()
    {
        const string secret = "secret-must-not-appear";
        var originalError = Console.Error;
        using var error = new StringWriter();
        try
        {
            Console.SetError(error);

            var exitCode = ALKAROS.Host.Program.Main([.. RequiredArguments(), "--db-password", secret]);

            Assert.Equal((int)HostExitCode.StartupFailed, exitCode);
            Assert.DoesNotContain(secret, error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    private static string[] RequiredArguments() =>
    [
        "--order-manifest", "manifest.json",
        "--migrations-dir", "migrations",
        "--db-url", "postgresql://user@host:5432/database",
    ];
}
