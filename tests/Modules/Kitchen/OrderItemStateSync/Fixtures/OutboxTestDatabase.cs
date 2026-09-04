using ALKAROS.TestHelpers;

namespace ALKAROS.Kitchen.OrderItemStateSync.Tests.Fixtures;

/// <summary>Isolated database with just the outbox/inbox tables (V1-KIT-005).</summary>
public sealed class OutboxTestDatabase : PgTestDatabase
{
    public OutboxTestDatabase()
        : base("alkaros_kit005_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }
}
