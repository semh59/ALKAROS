using ALKAROS.TestHelpers;

namespace ALKAROS.Payments.PaymentAggregate.Tests.Fixtures;

/// <summary>
/// Creates a unique test database for V13-PAY-001 and applies catalog (006),
/// table_mgmt (010), orders (011, 056, 102, 104, 105), billing (019),
/// the nonnegative-line-amounts constraint (036) and payments (120)
/// migrations in order — the same transitive chain Billing's own
/// BillFoundation tests need, since a Payment's bill_id FK requires a real
/// Bill, which itself requires a real Order.
/// </summary>
public sealed class PaymentsTestDatabase : PgTestDatabase
{
    public PaymentsTestDatabase()
        : base("alkaros_pay001_")
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
