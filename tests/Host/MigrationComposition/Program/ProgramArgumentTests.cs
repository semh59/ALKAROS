using ALKAROS.Host.Composition;
using ALKAROS.Host.Tests.Fixtures;
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

    [Fact]
    public void RollbackArgumentIsForwardedToTheHostComposition()
    {
        using var set = TestMigrationSet.CreateWithFiles(
            ["001"],
            ("001-stores.up.sql", TestMigrationSet.DefaultUpSql("stores")));
        var originalPassword = Environment.GetEnvironmentVariable("ALKAROS_DB_PASSWORD");
        var originalOutput = Console.Out;
        using var output = new StringWriter();
        try
        {
            Environment.SetEnvironmentVariable("ALKAROS_DB_PASSWORD", "test-password");
            Console.SetOut(output);

            var exitCode = ALKAROS.Host.Program.Main(
                [
                    "--order-manifest", set.ManifestPath,
                    "--migrations-dir", set.DirectoryPath,
                    "--db-url", "postgresql://user@host:5432/database",
                    "--psql", Path.Combine(Path.GetTempPath(), $"alkaros-{Guid.NewGuid():N}.exe"),
                    "--rollback", "001",
                ]);

            Assert.Equal((int)HostExitCode.StartupFailed, exitCode);
            Assert.Contains(
                "Rollback refused: no rollback script declares position [001].",
                output.ToString(),
                StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOutput);
            Environment.SetEnvironmentVariable("ALKAROS_DB_PASSWORD", originalPassword);
        }
    }

    private static string[] RequiredArguments() =>
    [
        "--order-manifest", "manifest.json",
        "--migrations-dir", "migrations",
        "--db-url", "postgresql://user@host:5432/database",
    ];
}
