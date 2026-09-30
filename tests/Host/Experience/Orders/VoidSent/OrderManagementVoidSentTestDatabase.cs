using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Inventory.MovementLedger;
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

    /// <summary>
    /// V1-RMD-111: assigns the REAL, canonically-coded 'waiter' role seeded
    /// by migration 043 — not a per-test throwaway role with a uniquifying
    /// suffix like <see cref="SeedCashierSessionAsync"/> below. The own-check
    /// guard in AuthorizationGrantService keys off the literal role code
    /// "waiter" (ApplicationPermissions.RoleWaiter), so an own-check
    /// regression test needs the genuine row.
    /// </summary>
    public async Task<(Guid UserId, string Cookie)> SeedRealWaiterSessionAsync(Guid terminalId)
    {
        var userId = Guid.NewGuid();
        var suffix = userId.ToString("N");
        var (raw, hash) = DeviceSessionToken.Create();

        await ExecuteAsync(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'not-used', 'VoidSent API Test Waiter', true);
            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES (@session_id, @user_id, @device_id, @token_hash, now(), now() + interval '1 hour');
            INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
            SELECT @user_role_id, @user_id, role_id FROM identity.roles WHERE code = 'waiter';
            """,
            ("user_id", userId),
            ("username", "iam027-waiter-" + suffix),
            ("session_id", Guid.NewGuid()),
            ("device_id", $"cashier:{terminalId:D}"),
            ("token_hash", hash),
            ("user_role_id", Guid.NewGuid()));

        return (userId, $"{DualScreenApplication.CashierCookieName}={raw}");
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

    /// <summary>Seeds a catalog product and an Active order with a single Active item (unit price 100, tax-inclusive 10% -> gross 100) at the given kitchen state.</summary>
    public Task<(Guid OrderId, Guid ItemId, Guid ProductId, OrderItem Item)> SeedActiveOrderWithOneItemAsync(KitchenState kitchenState)
        => SeedActiveOrderWithOneItemAsync(kitchenState, servingUserId: null);

    /// <summary>
    /// V1-RMD-111: same seed, but the order is attributed to
    /// <paramref name="servingUserId"/> — needed to exercise the own-check
    /// guard, which reads Order.ServingUserId end to end from here through
    /// the void-sent endpoint.
    /// </summary>
    public async Task<(Guid OrderId, Guid ItemId, Guid ProductId, OrderItem Item)> SeedActiveOrderWithOneItemAsync(
        KitchenState kitchenState, Guid? servingUserId)
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
            status: OrderState.Submitted,
            servingUserId: servingUserId);

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

    /// <summary>
    /// V1-RMD-143 follow-up (2026-09-09): seeds a stock item/location/balance
    /// already decremented by <paramref name="consumedQuantity"/> and the
    /// real Consumption movement OrderStockConsumptionService would have
    /// recorded for this exact order item at Accept time (`sourceReferenceId
    /// = orderItemId`) — the same movement SentItemVoidStore's own restore
    /// step looks up and reverses.
    /// </summary>
    public async Task<(Guid StockItemId, Guid LocationId)> SeedConsumedStockForItemAsync(
        Guid orderItemId, decimal onHandAfterConsumption, decimal consumedQuantity)
    {
        var locationId = Guid.NewGuid();
        var stockItemId = Guid.NewGuid();
        var suffix = stockItemId.ToString("N")[..8];
        await ExecuteAsync(
            """
            INSERT INTO inventory.stock_locations (id, code, name, location_type)
            VALUES (@location_id, @location_code, 'VoidSent Test Location', 'Counter');
            INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, default_location_id)
            VALUES (@stock_item_id, @stock_item_code, 'VoidSent Test Stock Item', 'Portion', 'adet', @location_id);
            INSERT INTO inventory.stock_balances (stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity, reserved_quantity, available_quantity)
            VALUES (@balance_id, @stock_item_id, @location_id, @on_hand, 0, @on_hand);
            """,
            ("location_id", locationId),
            ("location_code", "IAM027-" + suffix),
            ("stock_item_id", stockItemId),
            ("stock_item_code", "IAM027-" + suffix),
            ("balance_id", Guid.NewGuid()),
            ("on_hand", onHandAfterConsumption));

        var movement = new StockMovement(
            id: Guid.NewGuid(),
            stockItemId: stockItemId,
            stockLocationId: locationId,
            movementType: StockMovementType.Consumption,
            direction: MovementDirection.Out,
            quantity: consumedQuantity,
            unitCode: "adet",
            sourceType: StockMovementSourceType.Order,
            sourceReferenceId: orderItemId,
            reason: "Test seed: order item accepted");
        var repository = new PostgresStockMovementRepository(DataSource);
        await repository.AppendAsync(movement);

        return (stockItemId, locationId);
    }

    /// <summary>V1-RMD-143 follow-up: the real on-hand quantity for one stock item — for asserting a reversal actually restored it.</summary>
    public async Task<decimal> GetOnHandQuantityAsync(Guid stockItemId)
    {
        await using var command = DataSource.CreateCommand(
            "SELECT on_hand_quantity FROM inventory.stock_balances WHERE stock_item_id = @stock_item_id;");
        command.Parameters.AddWithValue("stock_item_id", stockItemId);
        var result = await command.ExecuteScalarAsync();
        return result is decimal value ? value : 0m;
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
