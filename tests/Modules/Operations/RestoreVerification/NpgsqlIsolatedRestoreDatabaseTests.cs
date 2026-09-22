using ALKAROS.Operations.RestoreVerification.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace ALKAROS.Operations.RestoreVerification.Tests;

/// <summary>
/// Regression coverage for the DisposeAsync exception-masking bug found in
/// V15-BKP-002's post-Done audit: a `DROP DATABASE ... WITH (FORCE)` that
/// itself fails during disposal must never replace an original exception
/// that is already unwinding through the caller's `await using` block.
/// </summary>
public sealed class NpgsqlIsolatedRestoreDatabaseTests : IAsyncLifetime
{
    private string _scratchDatabaseName = null!;
    private readonly string _goodMaintenanceConnectionString = RestoreVerificationTestDatabase.MaintenanceConnectionString();

    public async Task InitializeAsync()
    {
        _scratchDatabaseName = "alkaros_dispose_mask_test_" + Guid.NewGuid().ToString("N")[..16];
        await using var maintenance = new NpgsqlDataSourceBuilder(_goodMaintenanceConnectionString).Build();
        await using var create = maintenance.CreateCommand($"CREATE DATABASE \"{_scratchDatabaseName}\";");
        await create.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        // The database under test is disposed with a deliberately broken
        // maintenance connection string during the test itself, so its own
        // DROP never actually runs against real Postgres — clean it up for
        // real here with the good connection string.
        await using var maintenance = new NpgsqlDataSourceBuilder(_goodMaintenanceConnectionString).Build();
        await using var drop = maintenance.CreateCommand($"DROP DATABASE IF EXISTS \"{_scratchDatabaseName}\" WITH (FORCE);");
        await drop.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task DisposeAsyncSwallowsDropFailureAndPreservesOriginalException()
    {
        var scopedBuilder = new NpgsqlConnectionStringBuilder(_goodMaintenanceConnectionString) { Database = _scratchDatabaseName };
        var dataSource = new NpgsqlDataSourceBuilder(scopedBuilder.ConnectionString).Build();

        // A maintenance connection string that can never succeed (port 1 is
        // reserved and nothing listens there), so DisposeAsync's own
        // `DROP DATABASE` attempt is guaranteed to throw.
        var brokenMaintenanceConnectionString = "Host=127.0.0.1;Port=1;Username=postgres;Database=postgres;Timeout=1";
        var database = new NpgsqlIsolatedRestoreDatabase(_scratchDatabaseName, dataSource, brokenMaintenanceConnectionString);

        var thrown = await Record.ExceptionAsync(async () =>
        {
            await using (database)
            {
                throw new RestoreIntegrityCheckFailedException("dispose-masking-probe", "test-artifact-id");
            }
        });

        Assert.IsType<RestoreIntegrityCheckFailedException>(thrown);
        Assert.Contains("dispose-masking-probe", thrown!.Message);
    }
}
