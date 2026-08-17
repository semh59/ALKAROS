namespace ALKAROS.Kitchen.Routing.Tests;

using System.Globalization;
using FluentAssertions;
using Npgsql;
using Xunit;

public sealed class PostgresRoutingMigrationTests : IAsyncLifetime
{
    private readonly KitchenRoutingTestDatabase _db = new();

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
        var upSql = await File.ReadAllTextAsync(Path.Combine(sqlDirectory, "016-printer-routing.up.sql"));
        var downSql = await File.ReadAllTextAsync(Path.Combine(sqlDirectory, "016-printer-routing.down.sql"));

        // Verify tables exist after up migration (which ran during InitializeAsync)
        var tablesExistBeforeDown = await CheckTablesExistAsync();
        tablesExistBeforeDown.Should().BeTrue();

        // Run DOWN migration
        await _db.ExecuteSqlAsync(downSql);

        var tablesExistAfterDown = await CheckTablesExistAsync();
        tablesExistAfterDown.Should().BeFalse();

        // Re-apply UP migration
        await _db.ExecuteSqlAsync(upSql);

        var tablesExistAfterReapply = await CheckTablesExistAsync();
        tablesExistAfterReapply.Should().BeTrue();
    }

    private async Task<bool> CheckTablesExistAsync()
    {
        await using var connection = await _db.DataSource.OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT count(*)
            FROM information_schema.tables
            WHERE table_schema = 'kitchen' AND table_name IN ('printers', 'printer_routes');
            """;
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        return count == 2;
    }
}
