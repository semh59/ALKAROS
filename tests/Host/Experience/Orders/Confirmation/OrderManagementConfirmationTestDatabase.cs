using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.TestHelpers;

namespace ALKAROS.Host.Experience.Orders.Confirmation.Tests;

/// <summary>
/// Isolated database for `POST .../orders/{orderId}/accept|reject` (V1-RMD-137):
/// the same schema slice VoidSent uses (identity/catalog/audit/tables/orders,
/// granular permissions) plus Kitchen tickets and Billing bills, so a seeded
/// PendingConfirmation order's table release and kitchen-ticket cancellation
/// can be proven against real Postgres.
/// </summary>
public sealed class OrderManagementConfirmationTestDatabase : PgTestDatabase
{
    public OrderManagementConfirmationTestDatabase()
        : base("alkaros_rmd137_")
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
            VALUES (@user_id, @username, 'not-used', 'Confirmation API Test', true);
            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES (@session_id, @user_id, @device_id, @token_hash, now(), now() + interval '1 hour');
            """,
            ("user_id", userId),
            ("username", "rmd137-api-" + suffix),
            ("session_id", Guid.NewGuid()),
            ("device_id", $"cashier:{terminalId:D}"),
            ("token_hash", hash));

        var roleId = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO identity.roles (role_id, code, name) VALUES (@role_id, @role_code, 'Confirmation API Test Role');",
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

    /// <summary>
    /// Seeds a table (Reserved, no prior order), a catalog product, and a
    /// PendingConfirmation order for that table, then points the table's
    /// current_order_id at it — the exact state NfcOrderingStore leaves an
    /// age-restricted self-check-in order in.
    /// </summary>
    public async Task<(Guid OrderId, Guid TableId, Guid ProductId)> SeedPendingConfirmationOrderAsync()
    {
        var tableId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO table_mgmt.tables (table_id, table_number, capacity, active, current_status)
            VALUES (@table_id, @table_number, 4, true, 'Reserved');
            """,
            ("table_id", tableId),
            ("table_number", "RMD137-" + tableId.ToString("N")[..8]));

        var productId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active, is_age_restricted)
            VALUES (@product_id, @sku, 'Confirmation Test Product', 1, 1, true, true);
            """,
            ("product_id", productId),
            ("sku", "rmd137-" + productId.ToString("N")[..8]));

        var orderItem = new OrderItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            productId,
            "Confirmation Test Product",
            quantity: 1,
            unitPrice: 120m,
            taxRate: 10m,
            status: OrderItemState.Active,
            kitchenState: KitchenState.Sent);

        // Uses OrderSource.Waiter (not .Nfc) so this test database doesn't
        // need the V12-NFC-001 orders_source_check-widening migration too —
        // this store's Accept/Reject actions are channel-agnostic, so which
        // source the seeded order carries makes no difference to what's
        // under test here.
        var order = new Order(
            Guid.NewGuid(),
            OrderSource.Waiter,
            "RMD137-" + orderItem.Id.ToString("N")[..8],
            new[] { orderItem },
            tableId: tableId,
            status: OrderState.PendingConfirmation,
            confirmationStatus: ConfirmationStatus.Pending);

        var repository = new PostgresOrderRepository(DataSource);
        await repository.AddAsync(order);

        await ExecuteAsync(
            "UPDATE table_mgmt.tables SET current_order_id = @order_id WHERE table_id = @table_id;",
            ("order_id", order.Id),
            ("table_id", tableId));

        return (order.Id, tableId, productId);
    }

    /// <summary>Seeds a single-item kitchen ticket (Preparing) matching the order's own item, mirroring what NfcOrderingStore's immediate dispatch already created.</summary>
    public async Task SeedKitchenTicketAsync(Guid orderId, Guid orderItemId, Guid productId)
    {
        var ticketId = Guid.NewGuid();
        var ticketItem = new KitchenTicketItem(
            Guid.NewGuid(), ticketId, orderItemId, productId, "Confirmation Test Product", quantity: 1,
            status: KitchenTicketItemState.Preparing, isAgeRestricted: true);
        var ticket = new KitchenTicket(
            ticketId, orderId, "KT-" + ticketId.ToString("N")[..8], "hot-line",
            new[] { ticketItem }, status: KitchenTicketState.Preparing);

        var repository = new PostgresKitchenTicketRepository(DataSource);
        await repository.AddAsync(ticket);
    }

    public async Task<Guid> SeedBillAsync(Guid orderId, OrderItem orderItem)
    {
        var billId = Guid.NewGuid();
        var billItem = BillItem.FromOrderItem(billId, orderItem);
        var bill = new Bill(
            billId, "BILL-" + billId.ToString("N")[..8], new[] { billItem }, orderId: orderId);

        var repository = new PostgresBillRepository(DataSource);
        await repository.AddAsync(bill);
        return billId;
    }

    public async Task<(string Status, Guid? CurrentOrderId)> GetTableStateAsync(Guid tableId)
    {
        await using var command = DataSource.CreateCommand(
            "SELECT current_status, current_order_id FROM table_mgmt.tables WHERE table_id = @table_id;");
        command.Parameters.AddWithValue("table_id", tableId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new InvalidOperationException("Table not found.");
        return (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetGuid(1));
    }

    public async Task<IReadOnlyList<string>> GetKitchenTicketItemStatusesAsync(Guid orderId)
    {
        var repository = new PostgresKitchenTicketRepository(DataSource);
        var tickets = await repository.GetByOrderIdAsync(orderId);
        return tickets.SelectMany(t => t.Items).Select(i => i.Status.ToString()).ToArray();
    }

    public async Task<Order> ReloadOrderAsync(Guid orderId)
    {
        var repository = new PostgresOrderRepository(DataSource);
        return await repository.GetByIdAsync(orderId) ?? throw new InvalidOperationException("Order not found.");
    }
}
