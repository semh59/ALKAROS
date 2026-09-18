using ALKAROS.TestHelpers;

namespace ALKAROS.Payments.Allocations.RefundIntents.Tests.Fixtures;

/// <summary>
/// Creates a unique test database for V13-ALC-003 and applies catalog,
/// tables, orders, billing, payments (120, 121), payment_allocations (123)
/// and refund_intents (126) migrations in order — a refund intent's
/// payment_id/payment_allocation_id FKs require a real Payment and a real
/// PaymentAllocation.
/// </summary>
public sealed class RefundIntentTestDatabase : PgTestDatabase
{
    public RefundIntentTestDatabase()
        : base("alkaros_alc003_")
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
