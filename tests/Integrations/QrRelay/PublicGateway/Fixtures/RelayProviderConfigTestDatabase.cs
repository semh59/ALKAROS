using ALKAROS.TestHelpers;

namespace ALKAROS.QrRelay.PublicGateway.Tests.Fixtures;

public sealed class RelayProviderConfigTestDatabase : PgTestDatabase
{
    public RelayProviderConfigTestDatabase()
        : base("alkaros_qrt001_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }
}
