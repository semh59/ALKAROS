using ALKAROS.TestHelpers;

namespace ALKAROS.Operations.RestoreVerification.Tests.Fixtures;

/// <summary>Real Postgres fixture for migrations 138 (offsite_backup_receipts) and 139 (restore_attempts).</summary>
public sealed class RestoreVerificationTestDatabase : PgTestDatabase
{
    public RestoreVerificationTestDatabase()
        : base("alkaros_bkp002_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }

    /// <summary>
    /// Builds a maintenance connection string (Database=postgres) from the
    /// same ALKAROS_TEST_PG_* environment variables the fixture itself
    /// reads, for <see cref="NpgsqlIsolatedRestoreDatabaseFactory"/> to
    /// CREATE/DROP its own throwaway scratch databases against — entirely
    /// separate from this fixture's own database.
    /// </summary>
    public static string MaintenanceConnectionString()
    {
        var host = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_PORT") ?? "5432";
        var user = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_USER") ?? "postgres";
        var password = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_PASSWORD");

        var connectionString = $"Host={host};Port={port};Username={user};Database=postgres";
        return string.IsNullOrEmpty(password) ? connectionString : connectionString + $";Password={password}";
    }
}
