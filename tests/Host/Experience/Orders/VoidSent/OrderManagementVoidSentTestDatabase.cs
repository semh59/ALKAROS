using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.TestHelpers;

namespace ALKAROS.Host.Experience.Orders.VoidSent.Tests;

/// <summary>
/// Isolated database for `POST .../orders/{orderId}/items/{itemId}/void-sent`
/// (V1-IAM-027): the void-endpoint schema (identity/catalog/audit/tables/
/// orders, granular permissions, the full grant-flow schema) plus Kitchen
/// tickets and Billing bills, so a seeded ticket item and bill line can be
/// proven to actually get cancelled / converted to waste.
/// </summary>
public sealed class OrderManagementVoidSentTestDatabase : PgTestDatabase
{
    public OrderManagementVoidSentTestDatabase()
        : base("alkaros_iam027_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }

    public async Task<(Guid UserId, string Cookie)> SeedCashierSessionAsync(
        Guid terminalId, string roleCode, params string[] permissionCodes)
    {
        var userId = Guid.NewGuid();
        var suffix = userId.ToString("N");
        var (raw, hash) = DeviceSessionToken.Create();

        await ExecuteAsync(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'not-used', 'VoidSent API Test', true);
            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES (@session_id, @user_id, @device_id, @token_hash, now(), now() + interval '1 hour');
            """,
            ("user_id", userId),
            ("username", "iam027-api-" + suffix),
            ("session_id", Guid.NewGuid()),
            ("device_id", $"cashier:{terminalId:D}"),
            ("token_hash", hash));

        var roleId = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO identity.roles (role_id, code, name) VALUES (@role_id, @role_code, 'VoidSent API Test Role');",
            ("role_id", roleId),
            ("role_code", roleCode + "-" + suffix));
        await ExecuteAsync(
            "INSERT INTO identity.user_roles (user_role_id, user_id, role_id) VALUES (@id, @user_id, @role_id);",
            ("id", Guid.NewGuid()),
            ("user_id", userId),
            ("role_id", roleId));

        foreach (var code in permissionCodes)
        {
            await ExecuteAsync(
                """
                INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
                SELECT @id, @role_id, permission_id FROM identity.permissions WHERE code = @code;
                """,
                ("id", Guid.NewGuid()),
                ("role_id", roleId),
                ("code", code));
        }

        return (userId, $"{DualScreenApplication.CashierCookieName}={raw}");
    }

    /// <summary>Seeds a catalog product and an Active order with a single Active item (unit price 100, tax 10% -> gross 110) at the given kitchen state.</summary>
    public async Task<(Guid OrderId, Guid ItemId, Guid ProductId, OrderItem Item)> SeedActiveOrderWithOneItemAsync(KitchenState kitchenState)
    {
        var productId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active)
            VALUES (@product_id, @sku, 'VoidSent Test Product', 1, 1, true);
            """,
            ("product_id", productId),
            ("sku", "iam027-" + productId.ToString("N")[..8]));

        var orderItem = new OrderItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            productId,
            "VoidSent Test Product",
            quantity: 1,
            unitPrice: 100m,
            taxRate: 10m,
            status: OrderItemState.Active,
            kitchenState: kitchenState);

        var order = new Order(
            Guid.NewGuid(),
            OrderSource.Cashier,
            "IAM-027-" + orderItem.Id.ToString("N")[..8],
            new[] { orderItem },
            status: OrderState.Submitted);

        var repository = new PostgresOrderRepository(DataSource);
        await repository.AddAsync(order);

        return (order.Id, orderItem.Id, productId, order.Items[0]);
    }

    /// <summary>Seeds a single-item kitchen ticket at the given ticket-item status, matching the given order item.</summary>
    public async Task SeedKitchenTicketAsync(Guid orderId, Guid orderItemId, Guid productId, KitchenTicketItemState status)
    {
        var ticketId = Guid.NewGuid();
        var ticketItem = new KitchenTicketItem(
            Guid.NewGuid(), ticketId, orderItemId, productId, "VoidSent Test Product", quantity: 1,
            status: status);
        var ticket = new KitchenTicket(
            ticketId, orderId, "KT-" + ticketId.ToString("N")[..8], "hot-line",
            new[] { ticketItem },
            status: status == KitchenTicketItemState.Queued ? KitchenTicketState.Queued : KitchenTicketState.Preparing);

        var repository = new PostgresKitchenTicketRepository(DataSource);
        await repository.AddAsync(ticket);
    }

    /// <summary>Seeds a Bill with a single Sale line for the given order item, at the given status (default Open).</summary>
    public async Task<Guid> SeedOpenBillAsync(Guid orderId, OrderItem orderItem, BillState status = BillState.Open)
    {
        var billId = Guid.NewGuid();
        var billItem = BillItem.FromOrderItem(billId, orderItem);
        var bill = new Bill(
            billId, "BILL-" + billId.ToString("N")[..8], new[] { billItem },
            orderId: orderId, status: status);

        var repository = new PostgresBillRepository(DataSource);
        await repository.AddAsync(bill);
        return billId;
    }

    /// <summary>Reloads the order and returns the given item's current Status/KitchenState — for asserting nothing was mutated after a rejected void.</summary>
    public async Task<(OrderItemState Status, KitchenState KitchenState)> ReloadItemStateAsync(Guid orderId, Guid itemId)
    {
        var repository = new PostgresOrderRepository(DataSource);
        var order = await repository.GetByIdAsync(orderId) ?? throw new InvalidOperationException("Order not found.");
        var item = order.Items.First(i => i.Id == itemId);
        return (item.Status, item.KitchenState);
    }
}
