using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.TestHelpers;

namespace ALKAROS.Host.Experience.Orders.Void.Tests;

/// <summary>
/// Isolated database with users, identity/authorization, catalog, audit,
/// tables, orders and the granular permission catalog — everything
/// `POST .../orders/{orderId}/items/{itemId}/void` touches (V1-ORD-005).
/// </summary>
public sealed class OrderManagementVoidTestDatabase : PgTestDatabase
{
    public OrderManagementVoidTestDatabase()
        : base("alkaros_ord005_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }

    /// <summary>Seeds a cashier device session whose user holds orders.create.</summary>
    public Task<string> SeedCashierSessionWithOrdersCreateAsync(Guid terminalId)
        => SeedCashierSessionAsync(terminalId, grantOrdersCreate: true);

    /// <summary>Seeds a cashier device session with no permission grants at all.</summary>
    public Task<string> SeedCashierSessionWithoutOrdersCreateAsync(Guid terminalId)
        => SeedCashierSessionAsync(terminalId, grantOrdersCreate: false);

    private async Task<string> SeedCashierSessionAsync(Guid terminalId, bool grantOrdersCreate)
    {
        var userId = Guid.NewGuid();
        var suffix = userId.ToString("N");
        var (raw, hash) = DeviceSessionToken.Create();

        await ExecuteAsync(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'not-used', 'Order Void API Test', true);
            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES (@session_id, @user_id, @device_id, @token_hash, now(), now() + interval '1 hour');
            """,
            ("user_id", userId),
            ("username", "ord005-api-" + suffix),
            ("session_id", Guid.NewGuid()),
            ("device_id", $"cashier:{terminalId:D}"),
            ("token_hash", hash));

        if (grantOrdersCreate)
        {
            var roleId = Guid.NewGuid();
            await ExecuteAsync(
                """
                INSERT INTO identity.roles (role_id, code, name) VALUES (@role_id, @role_code, 'Order Void API Test Role');
                INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
                SELECT @role_permission_id, @role_id, permission_id FROM identity.permissions WHERE code = 'orders.create';
                INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
                VALUES (@user_role_id, @user_id, @role_id);
                """,
                ("role_id", roleId),
                ("role_code", "ord005-api-role-" + suffix),
                ("role_permission_id", Guid.NewGuid()),
                ("user_role_id", Guid.NewGuid()),
                ("user_id", userId));
        }

        return $"{DualScreenApplication.CashierCookieName}={raw}";
    }

    /// <summary>
    /// Seeds a catalog product and an Active order with a single Active
    /// item, at row version 1. When <paramref name="sent"/> is true the item
    /// is already marked as sent to the kitchen (KitchenState.Preparing) so
    /// the pre-send void guard rejects it.
    /// </summary>
    public async Task<(Guid OrderId, Guid ItemId)> SeedActiveOrderWithOneItemAsync(bool sent = false)
    {
        var productId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active)
            VALUES (@product_id, @sku, 'Void Test Product', 1, 1, true);
            """,
            ("product_id", productId),
            ("sku", "ord005-" + productId.ToString("N")[..8]));

        var orderItem = new OrderItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            productId,
            "Void Test Product",
            quantity: 1,
            unitPrice: 100m,
            taxRate: 10m,
            status: OrderItemState.Active,
            kitchenState: sent ? KitchenState.Preparing : KitchenState.NotSent);

        var order = new Order(
            Guid.NewGuid(),
            OrderSource.Cashier,
            "ORD-005-" + orderItem.Id.ToString("N")[..8],
            new[] { orderItem },
            status: OrderState.Submitted);

        var repository = new PostgresOrderRepository(DataSource);
        await repository.AddAsync(order);

        return (order.Id, orderItem.Id);
    }
}
