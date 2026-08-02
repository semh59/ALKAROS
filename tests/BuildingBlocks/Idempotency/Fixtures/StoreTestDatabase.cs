using ALKAROS.TestHelpers;

namespace ALKAROS.Idempotency.Tests.Fixtures;

/// <summary>
/// Creates a unique test database for V1-FND-002 infrastructure tables.
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

    /// <summary>
    /// Truncates all V1-FND-002 tables and resets their identity sequences.
    /// </summary>
    public async Task ResetTablesAsync()
        => await ExecuteAsync(
            "TRUNCATE TABLE inbox_messages, outbox_messages, idempotency_keys RESTART IDENTITY CASCADE;");

    /// <summary>
    /// Forces <paramref name="id"/> in <paramref name="table"/> to be
    /// immediately retryable.
    /// </summary>
    public async Task ForceRetryDueAsync(string table, Guid id)
        => await ExecuteAsync(
            $"UPDATE {table} SET next_retry_at = now() - interval '1 second' WHERE id = @id;",
            ("id", id));

    /// <summary>
    /// Forces <paramref name="operationId"/> in <paramref name="table"/> to
    /// be expired immediately.
    /// </summary>
    public async Task ForceExpiredAsync(string table, string operationId)
        => await ExecuteAsync(
            $"UPDATE {table} SET expires_at = now() - interval '1 second' WHERE operation_id = @operation_id;",
            ("operation_id", operationId));
}