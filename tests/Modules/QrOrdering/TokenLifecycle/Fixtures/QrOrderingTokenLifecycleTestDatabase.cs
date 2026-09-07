using ALKAROS.TestHelpers;
using NpgsqlTypes;

namespace ALKAROS.QrOrdering.TokenLifecycle.Tests.Fixtures;

/// <summary>
/// A table_mgmt + qr_ordering test database for V12-QRS-001: table_tokens
/// FK-references table_mgmt.tables, so both migrations are applied.
/// </summary>
public sealed class QrOrderingTokenLifecycleTestDatabase : PgTestDatabase
{
    public QrOrderingTokenLifecycleTestDatabase()
        : base("alkaros_qrs001_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }

    public async Task<Guid> SeedTableAsync()
    {
        var zoneId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO table_mgmt.zones (zone_id, code, name) VALUES (@zone_id, @zone_code, 'Main Floor');
            INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, current_status)
            VALUES (@table_id, @zone_id, @table_number, 4, 'Available');
            """,
            ("zone_id", zoneId),
            ("zone_code", "ZONE-" + zoneId.ToString("N")[..8]),
            ("table_id", tableId),
            ("table_number", "T-" + tableId.ToString("N")[..6]));

        return tableId;
    }

    public async Task<long> ActiveTokenCountAsync(Guid tableId)
    {
        await using var cmd = DataSource.CreateCommand(
            "SELECT count(*) FROM qr_ordering.table_tokens WHERE table_id = @table_id AND revoked_at IS NULL;");
        cmd.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
        return (long)(await cmd.ExecuteScalarAsync())!;
    }
}
