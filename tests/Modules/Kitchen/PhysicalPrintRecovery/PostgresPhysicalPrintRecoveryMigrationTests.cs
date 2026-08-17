namespace ALKAROS.Kitchen.PhysicalPrintRecovery.Tests;

using System.Globalization;
using FluentAssertions;
using Xunit;

public sealed class PostgresPhysicalPrintRecoveryMigrationTests : IAsyncLifetime
{
    private readonly KitchenPhysicalPrintRecoveryTestDatabase _db = new();

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task UpAndDownMigrationsApplyAndRevertCleanly()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        var upSql = await File.ReadAllTextAsync(Path.Combine(sqlDirectory, "018-physical-print-recovery.up.sql"));
        var downSql = await File.ReadAllTextAsync(Path.Combine(sqlDirectory, "018-physical-print-recovery.down.sql"));

        // Verify table exists after up migration (run during InitializeAsync)
        var tableExistsBeforeDown = await CheckTableExistsAsync();
        tableExistsBeforeDown.Should().BeTrue();

        // Run DOWN migration
        await _db.ExecuteSqlAsync(downSql);

        var tableExistsAfterDown = await CheckTableExistsAsync();
        tableExistsAfterDown.Should().BeFalse();

        // Re-apply UP migration
        await _db.ExecuteSqlAsync(upSql);

        var tableExistsAfterReapply = await CheckTableExistsAsync();
        tableExistsAfterReapply.Should().BeTrue();
    }

    private async Task<bool> CheckTableExistsAsync()
    {
        await using var connection = await _db.DataSource.OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT count(*)
            FROM information_schema.tables
            WHERE table_schema = 'kitchen' AND table_name = 'physical_print_deliveries';
            """;
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        return count == 1;
    }
}
