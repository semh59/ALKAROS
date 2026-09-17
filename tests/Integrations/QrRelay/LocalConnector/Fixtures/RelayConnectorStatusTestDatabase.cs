using ALKAROS.TestHelpers;

namespace ALKAROS.QrRelay.LocalConnector.Tests.Fixtures;

/// <summary>
/// Creates a unique test database for V12-QRT-005 and applies only the
/// relay_connector_status migration (125) — RelayConnectorStatusPublisher
/// touches no other table.
/// </summary>
public sealed class RelayConnectorStatusTestDatabase : PgTestDatabase
{
    public RelayConnectorStatusTestDatabase()
        : base("alkaros_qrt005_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
        {
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
        }
    }
}
