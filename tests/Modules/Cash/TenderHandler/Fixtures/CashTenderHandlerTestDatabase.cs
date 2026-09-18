using ALKAROS.TestHelpers;

namespace ALKAROS.Cash.TenderHandler.Tests.Fixtures;

/// <summary>
/// Creates a unique test database for V13-CSH-003 and applies catalog,
/// tables, orders, billing, payments (120, 121), cash_sessions (122),
/// payment_allocations (123) and cash_transactions (124) migrations in
/// order — a cash tender writes all three of Payment, PaymentAllocation
/// and CashTransaction, each with real FKs into Bill/CashSession.
/// </summary>
public sealed class CashTenderHandlerTestDatabase : PgTestDatabase
{
    public CashTenderHandlerTestDatabase()
        : base("alkaros_csh003_")
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
