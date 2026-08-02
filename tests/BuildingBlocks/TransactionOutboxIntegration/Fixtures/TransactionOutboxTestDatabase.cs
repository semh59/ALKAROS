using ALKAROS.TestHelpers;

namespace ALKAROS.TransactionOutboxIntegration.Tests.Fixtures;

/// <summary>
/// Creates a unique test database for V1-FND-006 transaction/outbox
/// integration tests using the V1-FND-002 outbox_messages table.
/// </summary>
public sealed class TransactionOutboxTestDatabase : PgTestDatabase
{
    public TransactionOutboxTestDatabase()
        : base("alkaros_fnd006_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }

    /// <summary>
    /// Truncates the outbox_messages table.
    /// </summary>
    public async Task ResetTablesAsync()
        => await ExecuteAsync("TRUNCATE TABLE outbox_messages RESTART IDENTITY CASCADE;");
}