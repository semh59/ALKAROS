using ALKAROS.TestHelpers;

namespace ALKAROS.Tables.Reservations.Tests.Fixtures;

/// <summary>
/// Sets up an isolated test PostgreSQL database applying all prerequisite migrations up to 025-table-reservations.
/// </summary>
public sealed class TableReservationTestDatabase : PgTestDatabase
{
    public TableReservationTestDatabase()
        : base("alkaros_tbl004_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        var upFiles = Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f).ToList();

        foreach (var file in upFiles)
        {
            var sql = await File.ReadAllTextAsync(file);
            await RunAsync(DataSource, sql);
        }
    }
}
