using ALKAROS.TestHelpers;

namespace ALKAROS.Billing.PaymentClosure.Tests.Fixtures;

/// <summary>
/// Creates a unique test database for V13-ALC-002 and applies catalog,
/// tables, orders, billing, payments (120, 121) and payment_allocations
/// (123) migrations in order — a projection rebuild reads real Bill,
/// Payment and PaymentAllocation rows.
/// </summary>
public sealed class BillPaymentClosureTestDatabase : PgTestDatabase
{
    public BillPaymentClosureTestDatabase()
        : base("alkaros_alc002_")
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
