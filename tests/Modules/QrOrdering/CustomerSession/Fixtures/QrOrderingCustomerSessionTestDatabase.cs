using ALKAROS.TestHelpers;

namespace ALKAROS.QrOrdering.CustomerSession.Tests.Fixtures;

/// <summary>
/// A table_mgmt + qr_ordering test database for V12-QRS-003: customer_sessions
/// does not itself FK-reference table_tokens, but
/// <see cref="CustomerSessionService.IssueAsync"/> exchanges a real table
/// token (V12-QRS-001), so both migrations are applied alongside this one.
/// </summary>
public sealed class QrOrderingCustomerSessionTestDatabase : PgTestDatabase
{
    public QrOrderingCustomerSessionTestDatabase()
        : base("alkaros_qrs003_")
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
}
