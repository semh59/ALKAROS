using ALKAROS.TestHelpers;

namespace ALKAROS.Orders.OrderAggregate.Tests.Fixtures;

/// <summary>
/// Creates a unique test database for V1-ORD-001 and applies the catalog
/// (006), table_mgmt (010) and orders (011) migration scripts in order so the
/// FK references from orders.order_items/orders.orders resolve.
/// </summary>
public sealed class OrdersTestDatabase : PgTestDatabase
{
    public OrdersTestDatabase()
        : base("alkaros_ord001_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }
}