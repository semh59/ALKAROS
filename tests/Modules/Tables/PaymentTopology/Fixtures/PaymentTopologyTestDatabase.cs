using ALKAROS.TestHelpers;

namespace ALKAROS.Tables.PaymentTopology.Tests.Fixtures;

/// <summary>
/// Minimal test database for the payment-aware table topology policy
/// (V13-TBL-001): just <c>billing.bills</c> and <c>payments.payments</c> —
/// the policy only ever reads <c>payments.payments</c>, and a Bill's
/// <c>table_id</c>/<c>order_id</c> are nullable, so no table_mgmt/orders
/// schema is needed for these unit-level tests. The full
/// integration-level proof (real transfer/merge/unmerge honoring this
/// policy) lives in <c>ALKAROS.Tables.TableTransfer.Tests</c>/
/// <c>ALKAROS.Tables.TableMerge.Tests</c>.
/// </summary>
public sealed class PaymentTopologyTestDatabase : PgTestDatabase
{
    public PaymentTopologyTestDatabase()
        : base("alkaros_tbl001_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        var upFiles = Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f).ToList();

        foreach (var file in upFiles)
        {
            var sql = await File.ReadAllTextAsync(file);
            await RunAsync(DataSource, sql);
        }
    }
}
