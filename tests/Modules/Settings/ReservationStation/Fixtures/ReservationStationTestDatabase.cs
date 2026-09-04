using ALKAROS.TestHelpers;

namespace ALKAROS.Settings.ReservationStation.Tests.Fixtures;

/// <summary>Isolated PostgreSQL database with users + settings migrations (V1-SET-003).</summary>
public sealed class ReservationStationTestDatabase : PgTestDatabase
{
    public ReservationStationTestDatabase()
        : base("alkaros_set003_")
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
