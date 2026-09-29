using ALKAROS.TestHelpers;

namespace ALKAROS.Invoicing.SourceSelection.Tests.Fixtures;

/// <summary>Applies account_transactions (161, the ledger the selection reads) then the invoicing tables (167).</summary>
public sealed class SourceSelectionTestDatabase : PgTestDatabase
{
    public SourceSelectionTestDatabase()
        : base("alkaros_inv001_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }

    /// <summary>A ledger row exactly as the account ledger stores it; <paramref name="occurredAt"/> is chosen by the test.</summary>
    public async Task<Guid> RecordAsync(Guid customerId, string type, decimal amount, DateTimeOffset occurredAt)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO customer_account.account_transactions
                (id, customer_id, transaction_type, amount, source_reference_type, source_reference_id, occurred_at)
            VALUES (@id, @customer_id, @type, @amount, 'Test', @id, @occurred_at);
            """,
            ("id", id),
            ("customer_id", customerId),
            ("type", type),
            ("amount", amount),
            ("occurred_at", occurredAt));
        return id;
    }
}
