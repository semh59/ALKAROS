using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.CrossChannelReservation;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.PortionReservations.CancellationEffects;
using ALKAROS.Inventory.PortionReservations.Lifecycle;
using ALKAROS.Inventory.ReservationBalanceProjection;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Inventory.Transactions;
using ALKAROS.Inventory.WasteRecording;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.Measurements;
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
        // V1-RMD-143: OrderManagementStore's own IOrderSubmissionDispatcher
        // factory (OrderManagementEndpoints.AddOrderManagementExperience)
        // requires this the moment anything constructs OrderManagementStore
        // at all — this file's own GET /{orderId} tests are the first ones
        // in this harness to actually exercise that path (accept/reject go
        // through PendingOrderConfirmationStore instead, which never
        // touches OrderManagementStore), same reasoning as
        // NfcOrderingTestDatabase's own constructor.
        Environment.SetEnvironmentVariable("ALKAROS_KITCHEN_STATION_ID", "grill-1");
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
    public async Task<(Guid OrderId, Guid TableId, Guid ProductId)> SeedPendingConfirmationOrderAsync(
        bool seedStockMapping = true, decimal stockOnHandQuantity = 100m)
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

        // V1-RMD-143: OrderStockConsumptionService refuses Accept outright
        // for a product with no stock mapping at all (Semih's own decision)
        // — every test that expects a real, successful Accept needs one,
        // hence the default true; a test of that refusal itself passes false.
        if (seedStockMapping)
            await SeedStockMappingWithBalanceAsync(productId, stockOnHandQuantity);

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

    /// <summary>
    /// V1-RMD-143: a PendingConfirmation order with one Active item (mapped,
    /// funded) and one already-Cancelled item whose product has NO stock
    /// mapping at all — reproducing what a waiter's void
    /// (ItemExceptionHandler.VoidItemAsync, ungated on order.Status) can
    /// leave behind before Accept ever runs. If OrderStockConsumptionService
    /// ever iterated the cancelled item too, this order could never Accept
    /// (PRODUCT_STOCK_NOT_CONFIGURED) even though the customer no longer has
    /// that item at all.
    /// </summary>
    public async Task<(Guid OrderId, Guid ActiveProductId, Guid CancelledProductId)> SeedPendingConfirmationOrderWithACancelledItemAsync(
        decimal stockOnHandQuantity = 10m)
    {
        var tableId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO table_mgmt.tables (table_id, table_number, capacity, active, current_status)
            VALUES (@table_id, @table_number, 4, true, 'Reserved');
            """,
            ("table_id", tableId),
            ("table_number", "RMD143V-" + tableId.ToString("N")[..8]));

        var activeProductId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active)
            VALUES (@product_id, @sku, 'Confirmation Active Product', 1, 1, true);
            """,
            ("product_id", activeProductId),
            ("sku", "rmd143v-active-" + activeProductId.ToString("N")[..8]));
        await SeedStockMappingWithBalanceAsync(activeProductId, stockOnHandQuantity);

        var cancelledProductId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active)
            VALUES (@product_id, @sku, 'Confirmation Cancelled Product', 1, 1, true);
            """,
            ("product_id", cancelledProductId),
            ("sku", "rmd143v-cancelled-" + cancelledProductId.ToString("N")[..8]));
        // Deliberately no stock mapping for this one — the point of the test.

        var activeItem = new OrderItem(
            Guid.NewGuid(), Guid.NewGuid(), activeProductId, "Confirmation Active Product",
            quantity: 1, unitPrice: 120m, taxRate: 10m,
            status: OrderItemState.Active, kitchenState: KitchenState.Sent);
        var cancelledItem = new OrderItem(
            Guid.NewGuid(), Guid.NewGuid(), cancelledProductId, "Confirmation Cancelled Product",
            quantity: 1, unitPrice: 80m, taxRate: 10m,
            status: OrderItemState.Cancelled, kitchenState: KitchenState.Cancelled);

        var order = new Order(
            Guid.NewGuid(),
            OrderSource.Waiter,
            "RMD143V-" + activeItem.Id.ToString("N")[..8],
            new[] { activeItem, cancelledItem },
            tableId: tableId,
            status: OrderState.PendingConfirmation,
            confirmationStatus: ConfirmationStatus.Pending);

        var repository = new PostgresOrderRepository(DataSource);
        await repository.AddAsync(order);

        await ExecuteAsync(
            "UPDATE table_mgmt.tables SET current_order_id = @order_id WHERE table_id = @table_id;",
            ("order_id", order.Id),
            ("table_id", tableId));

        return (order.Id, activeProductId, cancelledProductId);
    }

    /// <summary>V1-RMD-143: a stock location + item + product mapping + real on-hand balance, so Accept's own stock consumption succeeds.</summary>
    public async Task SeedStockMappingWithBalanceAsync(Guid productId, decimal onHandQuantity)
    {
        var locationId = Guid.NewGuid();
        var stockItemId = Guid.NewGuid();
        var suffix = stockItemId.ToString("N")[..8];
        await ExecuteAsync(
            """
            INSERT INTO inventory.stock_locations (id, code, name, location_type)
            VALUES (@location_id, @location_code, 'Confirmation Test Location', 'Counter');
            INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, default_location_id)
            VALUES (@stock_item_id, @stock_item_code, 'Confirmation Test Stock Item', 'Portion', 'adet', @location_id);
            INSERT INTO inventory.product_stock_mappings (product_id, stock_item_id, quantity_multiplier)
            VALUES (@product_id, @stock_item_id, 1.0);
            INSERT INTO inventory.stock_balances (stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity, reserved_quantity, available_quantity)
            VALUES (@balance_id, @stock_item_id, @location_id, @on_hand, 0, @on_hand);
            """,
            ("location_id", locationId),
            ("location_code", "RMD143-" + suffix),
            ("stock_item_id", stockItemId),
            ("stock_item_code", "RMD143-" + suffix),
            ("product_id", productId),
            ("balance_id", Guid.NewGuid()),
            ("on_hand", onHandQuantity));
    }

    /// <summary>
    /// V1-RMD-143 (2026-09-09 deep review): a PendingConfirmation order for
    /// a product whose BOM is TWO stock items — one abundantly funded, the
    /// other with none at all — proving OrderStockConsumptionService's own
    /// documented claim that a multi-mapping product's consumption is
    /// all-or-nothing within one transaction: if the second mapping is
    /// insufficient, the FIRST mapping's already-applied delta (same
    /// transaction, not yet committed) must roll back too, not just refuse
    /// the second one.
    /// </summary>
    public async Task<(Guid OrderId, Guid ProductId, Guid FundedStockItemId, Guid EmptyStockItemId)> SeedPendingConfirmationOrderWithATwoIngredientBomAsync()
    {
        var tableId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO table_mgmt.tables (table_id, table_number, capacity, active, current_status)
            VALUES (@table_id, @table_number, 4, true, 'Reserved');
            """,
            ("table_id", tableId),
            ("table_number", "RMD143B-" + tableId.ToString("N")[..8]));

        var productId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active)
            VALUES (@product_id, @sku, 'Confirmation Two-Ingredient Product', 1, 1, true);
            """,
            ("product_id", productId),
            ("sku", "rmd143b-" + productId.ToString("N")[..8]));

        var locationId = Guid.NewGuid();
        var fundedStockItemId = Guid.NewGuid();
        var emptyStockItemId = Guid.NewGuid();
        var suffix = productId.ToString("N")[..8];
        await ExecuteAsync(
            """
            INSERT INTO inventory.stock_locations (id, code, name, location_type)
            VALUES (@location_id, @location_code, 'Confirmation Bom Test Location', 'Counter');
            INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, default_location_id)
            VALUES (@funded_id, @funded_code, 'Confirmation Bom Funded Item', 'RawMaterial', 'adet', @location_id);
            INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, default_location_id)
            VALUES (@empty_id, @empty_code, 'Confirmation Bom Empty Item', 'RawMaterial', 'adet', @location_id);
            INSERT INTO inventory.product_stock_mappings (product_id, stock_item_id, quantity_multiplier)
            VALUES (@product_id, @funded_id, 1.0), (@product_id, @empty_id, 1.0);
            INSERT INTO inventory.stock_balances (stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity, reserved_quantity, available_quantity)
            VALUES
                (@funded_balance_id, @funded_id, @location_id, 10, 0, 10),
                (@empty_balance_id, @empty_id, @location_id, 0, 0, 0);
            """,
            ("location_id", locationId),
            ("location_code", "RMD143B-" + suffix),
            ("funded_id", fundedStockItemId),
            ("funded_code", "RMD143B-FUNDED-" + suffix),
            ("empty_id", emptyStockItemId),
            ("empty_code", "RMD143B-EMPTY-" + suffix),
            ("product_id", productId),
            ("funded_balance_id", Guid.NewGuid()),
            ("empty_balance_id", Guid.NewGuid()));

        var item = new OrderItem(
            Guid.NewGuid(), Guid.NewGuid(), productId, "Confirmation Two-Ingredient Product",
            quantity: 1, unitPrice: 150m, taxRate: 10m,
            status: OrderItemState.Active, kitchenState: KitchenState.Sent);
        var order = new Order(
            Guid.NewGuid(),
            OrderSource.Waiter,
            "RMD143B-" + item.Id.ToString("N")[..8],
            new[] { item },
            tableId: tableId,
            status: OrderState.PendingConfirmation,
            confirmationStatus: ConfirmationStatus.Pending);

        var repository = new PostgresOrderRepository(DataSource);
        await repository.AddAsync(order);

        await ExecuteAsync(
            "UPDATE table_mgmt.tables SET current_order_id = @order_id WHERE table_id = @table_id;",
            ("order_id", order.Id),
            ("table_id", tableId));

        return (order.Id, productId, fundedStockItemId, emptyStockItemId);
    }

    /// <summary>
    /// V1-RMD-143 (2026-09-09 deep review): a PendingConfirmation order
    /// whose only item is already Complimentary (a manager can comp an
    /// item before Accept too — ItemExceptionHandler.ApplyComplimentaryAsync
    /// is likewise never gated on order.Status). Complimentary items are
    /// still really prepared and served for free, so unlike a Cancelled
    /// line they must still consume stock at Accept.
    /// </summary>
    public async Task<(Guid OrderId, Guid ProductId)> SeedPendingConfirmationOrderWithAComplimentaryItemAsync(decimal stockOnHandQuantity = 10m)
    {
        var tableId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO table_mgmt.tables (table_id, table_number, capacity, active, current_status)
            VALUES (@table_id, @table_number, 4, true, 'Reserved');
            """,
            ("table_id", tableId),
            ("table_number", "RMD143C-" + tableId.ToString("N")[..8]));

        var productId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active)
            VALUES (@product_id, @sku, 'Confirmation Complimentary Product', 1, 1, true);
            """,
            ("product_id", productId),
            ("sku", "rmd143c-" + productId.ToString("N")[..8]));
        await SeedStockMappingWithBalanceAsync(productId, stockOnHandQuantity);

        var item = new OrderItem(
            Guid.NewGuid(), Guid.NewGuid(), productId, "Confirmation Complimentary Product",
            quantity: 1, unitPrice: 90m, taxRate: 10m,
            status: OrderItemState.Complimentary, kitchenState: KitchenState.Sent,
            netAmount: 0m, taxAmount: 0m, grossAmount: 0m);
        var order = new Order(
            Guid.NewGuid(),
            OrderSource.Waiter,
            "RMD143C-" + item.Id.ToString("N")[..8],
            new[] { item },
            tableId: tableId,
            status: OrderState.PendingConfirmation,
            confirmationStatus: ConfirmationStatus.Pending);

        var repository = new PostgresOrderRepository(DataSource);
        await repository.AddAsync(order);

        await ExecuteAsync(
            "UPDATE table_mgmt.tables SET current_order_id = @order_id WHERE table_id = @table_id;",
            ("order_id", order.Id),
            ("table_id", tableId));

        return (order.Id, productId);
    }

    /// <summary>
    /// V12-QRO-002: a QR-sourced PendingConfirmation order whose updated_at
    /// is backdated by <paramref name="age"/> — for QrOrderExpiryHostedService's
    /// own tests. OrderSource.Qr is used deliberately (unlike
    /// SeedPendingConfirmationOrderAsync's channel-agnostic OrderSource.Waiter
    /// above) since the expiry query filters on source = 'Qr'.
    /// </summary>
    public async Task<(Guid OrderId, Guid TableId)> SeedOverdueQrPendingOrderAsync(TimeSpan age)
    {
        var tableId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO table_mgmt.tables (table_id, table_number, capacity, active, current_status)
            VALUES (@table_id, @table_number, 4, true, 'Reserved');
            """,
            ("table_id", tableId),
            ("table_number", "QRO002-" + tableId.ToString("N")[..8]));

        var productId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active)
            VALUES (@product_id, @sku, 'Expiry Test Product', 1, 1, true);
            """,
            ("product_id", productId),
            ("sku", "qro002-" + productId.ToString("N")[..8]));

        var orderItem = new OrderItem(
            Guid.NewGuid(), Guid.NewGuid(), productId, "Expiry Test Product",
            quantity: 1, unitPrice: 90m, taxRate: 10m,
            status: OrderItemState.Active, kitchenState: KitchenState.Sent);

        var order = new Order(
            Guid.NewGuid(),
            OrderSource.Qr,
            "QRO002-" + orderItem.Id.ToString("N")[..8],
            new[] { orderItem },
            tableId: tableId,
            status: OrderState.PendingConfirmation,
            confirmationStatus: ConfirmationStatus.Pending);

        var repository = new PostgresOrderRepository(DataSource);
        await repository.AddAsync(order);

        await ExecuteAsync(
            """
            UPDATE table_mgmt.tables SET current_order_id = @order_id WHERE table_id = @table_id;
            UPDATE orders.orders SET updated_at = now() - @age WHERE order_id = @order_id;
            """,
            ("order_id", order.Id),
            ("table_id", tableId),
            ("age", age));

        return (order.Id, tableId);
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

    /// <summary>V1-RMD-143: the on-hand quantity for a product's own (single, test-seeded) stock mapping — null if the product has none.</summary>
    public async Task<decimal?> GetOnHandQuantityForProductAsync(Guid productId)
    {
        await using var command = DataSource.CreateCommand(
            """
            SELECT b.on_hand_quantity
            FROM inventory.product_stock_mappings m
            JOIN inventory.stock_balances b ON b.stock_item_id = m.stock_item_id
            WHERE m.product_id = @product_id;
            """);
        command.Parameters.AddWithValue("product_id", productId);
        var result = await command.ExecuteScalarAsync();
        return result is decimal value ? value : null;
    }

    /// <summary>V1-RMD-143: on-hand quantity for one specific stock item — for a product with more than one mapping, where the by-product lookup above is ambiguous.</summary>
    public async Task<decimal> GetOnHandQuantityForStockItemAsync(Guid stockItemId)
    {
        await using var command = DataSource.CreateCommand(
            "SELECT on_hand_quantity FROM inventory.stock_balances WHERE stock_item_id = @stock_item_id;");
        command.Parameters.AddWithValue("stock_item_id", stockItemId);
        var result = await command.ExecuteScalarAsync();
        return result is decimal value ? value : 0m;
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

    /// <summary>
    /// V12-STK-001: holds one unit of <paramref name="productId"/> for <paramref name="orderId"/>'s
    /// <paramref name="orderItemId"/> through the real cross-channel arbiter, committed.
    /// </summary>
    public async Task<CrossChannelReservationOutcome> HoldAsync(Guid orderId, Guid orderItemId, Guid productId)
    {
        await using var connection = await DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var result = await CreateArbiter().ReserveAsync(
            new CrossChannelReservationRequest(
                ReservationChannel.Online, "provider-" + orderId.ToString("N"), orderId, Guid.NewGuid(),
                new[] { new CrossChannelReservationLine(orderItemId, productId, 1m) }),
            connection,
            transaction);
        await transaction.CommitAsync();
        return result.Outcome;
    }

    /// <summary>The real cross-channel arbiter (V12-STK-001) over this database, with its V11-RSV-003 compensation chain.</summary>
    public PostgresCrossChannelPortionArbiter CreateArbiter()
    {
        var items = new PostgresStockItemRepository(DataSource);
        var locations = new PostgresStockLocationRepository(DataSource);
        var reservations = new PostgresPortionReservationRepository(DataSource);
        var balances = new PostgresStockBalanceRepository(DataSource);
        return new PostgresCrossChannelPortionArbiter(
            DataSource,
            balances,
            new PortionCancellationDecisionService(
                reservations,
                new PortionReservationLifecycleService(reservations, items, locations),
                new ReservationBalanceProjector(new PostgresReservationBalanceRepository(DataSource)),
                new WasteRecordingService(
                    new PostgresInventoryTransactionRunner(DataSource),
                    new PostgresWasteRecordRepository(DataSource),
                    new PostgresStockMovementRepository(DataSource),
                    items,
                    locations,
                    balances,
                    new UnitConverter()),
                new PostgresKitchenItemStateProvider(DataSource)));
    }

    /// <summary>
    /// V12-QRO-003: a QR-sourced PendingConfirmation order (one item, quantity 1) holding its table as
    /// Reserved, as QrOrderSubmittedConsumer leaves it; its product is mapped to stock with
    /// <paramref name="stockOnHandQuantity"/> on hand unless <paramref name="seedStockMapping"/> is false.
    /// </summary>
    public async Task<(Guid OrderId, Guid TableId, Guid ProductId, Guid SubmissionId)> SeedQrPendingOrderAsync(
        decimal stockOnHandQuantity, bool seedStockMapping = true)
    {
        var tableId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO table_mgmt.tables (table_id, table_number, capacity, active, current_status)
            VALUES (@table_id, @table_number, 4, true, 'Reserved');
            """,
            ("table_id", tableId),
            ("table_number", "QRO003-" + tableId.ToString("N")[..8]));

        var productId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active)
            VALUES (@product_id, @sku, 'Son Porsiyon Mantı', 1, 1, true);
            """,
            ("product_id", productId),
            ("sku", "qro003-" + productId.ToString("N")[..8]));
        if (seedStockMapping)
            await SeedStockMappingWithBalanceAsync(productId, stockOnHandQuantity);

        var orderItem = new OrderItem(
            Guid.NewGuid(), Guid.NewGuid(), productId, "Son Porsiyon Mantı",
            quantity: 1, unitPrice: 180m, taxRate: 10m,
            status: OrderItemState.Active, kitchenState: KitchenState.Sent);
        var submissionId = Guid.NewGuid();
        var order = new Order(
            Guid.NewGuid(),
            OrderSource.Qr,
            "QRO003-" + orderItem.Id.ToString("N")[..8],
            new[] { orderItem },
            tableId: tableId,
            sourceReferenceId: submissionId,
            status: OrderState.PendingConfirmation,
            confirmationStatus: ConfirmationStatus.Pending);
        await new PostgresOrderRepository(DataSource).AddAsync(order);

        await ExecuteAsync(
            "UPDATE table_mgmt.tables SET current_order_id = @order_id WHERE table_id = @table_id;",
            ("order_id", order.Id),
            ("table_id", tableId));

        return (order.Id, tableId, productId, submissionId);
    }

    /// <summary>
    /// V12-RMD-003: a pending QR order whose one item carries a modifier, the modifier's stock row sorting BEFORE
    /// the product's (so a lock taken in the hold's order and then the consumption's would invert them).
    /// </summary>
    public async Task<(Guid OrderId, (Guid Item, Guid Location) ProductStock, (Guid Item, Guid Location) ModifierStock)>
        SeedQrPendingOrderWithModifierStockAsync()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var (modifierStockId, productStockId) = first.CompareTo(second) < 0 ? (first, second) : (second, first);
        var productLocation = Guid.NewGuid();
        var modifierLocation = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var suffix = productId.ToString("N")[..8];

        await ExecuteAsync(
            """
            INSERT INTO table_mgmt.tables (table_id, table_number, capacity, active, current_status)
            VALUES (@table_id, @table_number, 4, true, 'Reserved');
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active)
            VALUES (@product_id, @sku, 'Peynirli Pide', 1, 1, true);
            INSERT INTO inventory.stock_locations (id, code, name, location_type)
            VALUES (@product_location, @product_location_code, 'Pide Tezgahı', 'Counter'),
                   (@modifier_location, @modifier_location_code, 'Peynir Dolabı', 'Counter');
            INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, default_location_id)
            VALUES (@product_stock, @product_stock_code, 'Pide Hamuru', 'Portion', 'adet', @product_location),
                   (@modifier_stock, @modifier_stock_code, 'Kaşar', 'RawMaterial', 'adet', @modifier_location);
            INSERT INTO inventory.product_stock_mappings (product_id, stock_item_id, quantity_multiplier)
            VALUES (@product_id, @product_stock, 1.0);
            INSERT INTO catalog.modifier_groups (modifier_group_id, code, name, selection_type, min_selections, max_selections, active)
            VALUES (@group_id, @group_code, 'Ekstralar', 2, 0, 3, true);
            INSERT INTO catalog.modifiers (modifier_id, modifier_group_id, code, name, price_delta, active)
            VALUES (@modifier_id, @group_id, @modifier_code, 'Ekstra kaşar', 15, true);
            INSERT INTO catalog.product_modifier_groups (product_modifier_group_id, product_id, modifier_group_id)
            VALUES (gen_random_uuid(), @product_id, @group_id);
            INSERT INTO inventory.modifier_stock_mappings (modifier_id, stock_item_id, quantity_multiplier)
            VALUES (@modifier_id, @modifier_stock, 1.0);
            INSERT INTO inventory.stock_balances (stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity, reserved_quantity, available_quantity)
            VALUES (gen_random_uuid(), @product_stock, @product_location, 5, 0, 5),
                   (gen_random_uuid(), @modifier_stock, @modifier_location, 5, 0, 5);
            """,
            ("table_id", tableId),
            ("table_number", "RMD003-" + suffix),
            ("product_id", productId),
            ("sku", "rmd003-" + suffix),
            ("product_location", productLocation),
            ("product_location_code", "RMD003P-" + suffix),
            ("modifier_location", modifierLocation),
            ("modifier_location_code", "RMD003M-" + suffix),
            ("product_stock", productStockId),
            ("product_stock_code", "RMD003PS-" + suffix),
            ("modifier_stock", modifierStockId),
            ("modifier_stock_code", "RMD003MS-" + suffix),
            ("modifier_id", modifierId),
            ("group_id", Guid.NewGuid()),
            ("group_code", "RMD003G-" + suffix),
            ("modifier_code", "RMD003MOD-" + suffix));

        var itemId = Guid.NewGuid();
        var orderItem = new OrderItem(
            itemId, Guid.NewGuid(), productId, "Peynirli Pide",
            quantity: 1, unitPrice: 150m, taxRate: 10m,
            modifiers: [new OrderItemModifier(Guid.NewGuid(), itemId, modifierId, "Ekstra kaşar")],
            status: OrderItemState.Active, kitchenState: KitchenState.Sent);
        var order = new Order(
            Guid.NewGuid(),
            OrderSource.Qr,
            "RMD003-" + suffix,
            new[] { orderItem },
            tableId: tableId,
            sourceReferenceId: Guid.NewGuid(),
            status: OrderState.PendingConfirmation,
            confirmationStatus: ConfirmationStatus.Pending);
        await new PostgresOrderRepository(DataSource).AddAsync(order);
        await ExecuteAsync(
            "UPDATE table_mgmt.tables SET current_order_id = @order_id WHERE table_id = @table_id;",
            ("order_id", order.Id),
            ("table_id", tableId));

        return (order.Id, (productStockId, productLocation), (modifierStockId, modifierLocation));
    }

    /// <summary>V12-QRO-003: (status, channel, channel reference) of every hold written for an order.</summary>
    public async Task<IReadOnlyList<(string Status, string Channel, string Reference)>> GetHoldsForOrderAsync(Guid orderId)
    {
        await using var command = DataSource.CreateCommand(
            """
            SELECT status, metadata->>'channel', metadata->>'channelOrderReference'
            FROM inventory.portion_reservations
            WHERE order_id = @order_id
            ORDER BY reserved_at;
            """);
        command.Parameters.AddWithValue("order_id", orderId);
        var holds = new List<(string, string, string)>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            holds.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        return holds;
    }

    /// <summary>V12-STK-001: (reserved quantity, statuses of every hold) for a product's single stock mapping.</summary>
    public async Task<(decimal Reserved, IReadOnlyList<string> HoldStatuses)> GetHoldStateForProductAsync(Guid productId)
    {
        await using var command = DataSource.CreateCommand(
            """
            SELECT b.reserved_quantity,
                   COALESCE(array_agg(r.status ORDER BY r.reserved_at) FILTER (WHERE r.id IS NOT NULL), '{}')
            FROM inventory.product_stock_mappings m
            JOIN inventory.stock_balances b ON b.stock_item_id = m.stock_item_id
            LEFT JOIN inventory.portion_reservations r ON r.stock_item_id = m.stock_item_id
            WHERE m.product_id = @product_id
            GROUP BY b.reserved_quantity;
            """);
        command.Parameters.AddWithValue("product_id", productId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new InvalidOperationException("No stock balance for product.");
        return (reader.GetDecimal(0), reader.GetFieldValue<string[]>(1));
    }
}
