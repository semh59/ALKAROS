using ALKAROS.TestHelpers;

namespace ALKAROS.Payments.CardSettlement.Tests.Fixtures;

/// <summary>
/// Creates a unique test database for V13-PAY-004 and applies outbox (003),
/// catalog, tables, orders, billing, payments (120, 121), payment_allocations
/// (123) and card_settlement_attempts (140) migrations in order — a card
/// settlement writes Payment, PaymentAllocation, an outbox row, and its own
/// attempt row, each with real FKs into Bill/Payment.
/// </summary>
public sealed class CardSettlementTestDatabase : PgTestDatabase
{
    public CardSettlementTestDatabase()
        : base("alkaros_pay004_")
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
