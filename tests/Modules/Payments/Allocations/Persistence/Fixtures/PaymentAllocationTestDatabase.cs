using ALKAROS.TestHelpers;

namespace ALKAROS.Payments.Allocations.Persistence.Tests.Fixtures;

/// <summary>
/// Creates a unique test database for V13-ALC-001 and applies catalog (006),
/// table_mgmt (010), orders (011, 056, 102, 104, 105), billing (019), the
/// nonnegative-line-amounts constraint (036), payments (120, 121) and
/// payment_allocations (123) migrations in order — the same transitive
/// chain PaymentAggregate's own tests need, since an allocation's bill_id/
/// payment_id FKs require a real Bill and a real Payment.
/// </summary>
public sealed class PaymentAllocationTestDatabase : PgTestDatabase
{
    public PaymentAllocationTestDatabase()
        : base("alkaros_alc001_")
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
