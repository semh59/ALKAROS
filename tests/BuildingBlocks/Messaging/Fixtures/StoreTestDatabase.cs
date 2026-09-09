using ALKAROS.TestHelpers;

namespace ALKAROS.Messaging.Tests.Fixtures;

/// <summary>
/// Creates a unique test database for V1-FND-002's outbox_messages table.
/// V1-RMD-134: still applies the full V1-FND-002/V1-FND-019 migration set
/// (idempotency_keys, inbox_messages included) because 033-message-lease-generation
/// ALTERs inbox_messages directly and therefore requires it to already
/// exist — this project no longer tests either table, but the schema
/// itself was intentionally left untouched (a separate, higher-risk
/// decision from deleting the dead C# surface above it).
/// </summary>
public sealed class StoreTestDatabase : PgTestDatabase
{
    public StoreTestDatabase()
        : base("alkaros_fnd002_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }

    /// <summary>Truncates outbox_messages and resets its identity sequence.</summary>
    public async Task ResetTablesAsync()
        => await ExecuteAsync("TRUNCATE TABLE outbox_messages RESTART IDENTITY CASCADE;");

    /// <summary>
    /// Forces <paramref name="id"/> in <paramref name="table"/> to be
    /// immediately retryable.
    /// </summary>
    public async Task ForceRetryDueAsync(string table, Guid id)
        => await ExecuteAsync(
            $"UPDATE {table} SET next_retry_at = now() - interval '1 second' WHERE id = @id;",
            ("id", id));
}
