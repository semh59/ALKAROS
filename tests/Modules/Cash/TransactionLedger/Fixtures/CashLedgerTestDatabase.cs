using ALKAROS.TestHelpers;

namespace ALKAROS.Cash.TransactionLedger.Tests.Fixtures;

/// <summary>
/// Creates a unique test database for V13-CSH-002 and applies catalog,
/// tables, orders, billing, payments (120, 121), cash_sessions (122) and
/// cash_transactions (124) migrations in order — a CashTransaction's
/// related_payment_id FK requires a real Payment, and its cash_session_id
/// FK requires a real CashSession.
/// </summary>
public sealed class CashLedgerTestDatabase : PgTestDatabase
{
    public CashLedgerTestDatabase()
        : base("alkaros_csh002_")
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
