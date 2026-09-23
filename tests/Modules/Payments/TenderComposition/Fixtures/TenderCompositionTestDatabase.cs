using ALKAROS.TestHelpers;

namespace ALKAROS.Payments.TenderComposition.Tests.Fixtures;

/// <summary>
/// Creates a unique test database for V13-PAY-003 and applies the same
/// catalog/tables/orders/billing/payments/cash-sessions/allocations/
/// cash-transactions migration chain as V13-CSH-003's own test database —
/// the Cash bridge exercises the exact same write set as
/// <c>CashTenderHandler</c> itself.
/// </summary>
public sealed class TenderCompositionTestDatabase : PgTestDatabase
{
    public TenderCompositionTestDatabase()
        : base("alkaros_pay003_")
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
