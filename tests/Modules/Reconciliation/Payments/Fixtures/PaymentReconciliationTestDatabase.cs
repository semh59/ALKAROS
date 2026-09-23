using ALKAROS.TestHelpers;

namespace ALKAROS.Reconciliation.Payments.Tests.Fixtures;

/// <summary>
/// Test database for V13-REC-001: applies catalog/tables/orders/billing,
/// the reconciliation case foundation (030), payments (120/121),
/// cash sessions (122/124/130), payment allocations (123) and card
/// settlement attempts (140) — everything the four real source pairs
/// (<see cref="PaymentUnknownSourcePair"/>, <see cref="ApprovedWithoutAllocationSourcePair"/>,
/// <see cref="CardSettlementAllocationMismatchSourcePair"/>,
/// <see cref="CashSessionDifferenceSourcePair"/>) read directly by plain SQL.
/// </summary>
public sealed class PaymentReconciliationTestDatabase : PgTestDatabase
{
    public PaymentReconciliationTestDatabase()
        : base("alkaros_rec_pay_")
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
