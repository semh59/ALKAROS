using ALKAROS.Catalog.ProductCatalog;
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
using ALKAROS.Measurements;
using ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.TestHelpers;

namespace ALKAROS.Host.Experience.OnlineOrdering.Tests;

/// <summary>
/// The schema slice online order intake touches: catalog, orders, kitchen tickets, inventory with
/// portion reservations, the Yemeksepeti mapping/inbox/processing tables — the same base the
/// Orders.Confirmation harness uses, plus the Online Ordering migrations.
/// </summary>
public sealed class OnlineOrderingTestDatabase : PgTestDatabase
{
    public OnlineOrderingTestDatabase() : base("alkaros_ysp_online_")
    {
        // The kitchen dispatcher resolves its station lazily, the first time an order is fired.
        Environment.SetEnvironmentVariable("ALKAROS_KITCHEN_STATION_ID", "grill-1");
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f, StringComparer.Ordinal))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }

    public async Task RunFixtureAsync(string file) =>
        await RunAsync(DataSource, await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", file)));

    /// <summary>A signed-in staff session on <paramref name="terminalId"/> holding exactly <paramref name="permissionCodes"/>; returns its cookie.</summary>
    public async Task<string> SeedStaffSessionAsync(Guid terminalId, params string[] permissionCodes)
    {
        var userId = Guid.NewGuid();
        var suffix = userId.ToString("N");
        var (raw, hash) = DeviceSessionToken.Create();
        await ExecAsync(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'not-used', 'Online Ordering Test', true);
            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES (@session_id, @user_id, @device_id, @token_hash, now(), now() + interval '1 hour');
            """,
            ("user_id", userId), ("username", "online-" + suffix), ("session_id", Guid.NewGuid()),
            ("device_id", $"cashier:{terminalId:D}"), ("token_hash", hash));

        var roleId = Guid.NewGuid();
        await ExecAsync("INSERT INTO identity.roles (role_id, code, name) VALUES (@role_id, @code, 'Online Ordering Test Role');",
            ("role_id", roleId), ("code", "online-" + suffix));
        await ExecAsync("INSERT INTO identity.user_roles (user_role_id, user_id, role_id) VALUES (@id, @user_id, @role_id);",
            ("id", Guid.NewGuid()), ("user_id", userId), ("role_id", roleId));
        foreach (var code in permissionCodes)
        {
            await ExecAsync(
                """
                INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
                SELECT @id, @role_id, permission_id FROM identity.permissions WHERE code = @code;
                """,
                ("id", Guid.NewGuid()), ("role_id", roleId), ("code", code));
        }

        return $"{DualScreenApplication.CashierCookieName}={raw}";
    }

    /// <summary>A menu holding one active, priced product; returns the menu and the product's catalog SKU.</summary>
    public async Task<(Guid MenuId, Guid ProductId, string CatalogSku)> SeedPublishableMenuAsync(decimal price)
    {
        var menuId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var sku = "PUB-" + productId.ToString("N")[..8];
        await ExecAsync(
            """
            INSERT INTO menu.menus (menu_id, code, name) VALUES (@menu, @code, 'Online Menü');
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active, current_price)
            VALUES (@product, @sku, 'Mercimek Çorbası', 1, 1, true, @price);
            INSERT INTO menu.menu_items (menu_item_id, menu_id, product_id) VALUES (@item, @menu, @product);
            """,
            ("menu", menuId), ("code", "OM-" + menuId.ToString("N")[..8]), ("product", productId), ("sku", sku),
            ("price", price), ("item", Guid.NewGuid()));
        return (menuId, productId, sku);
    }

    /// <summary>A QR order waiting for staff confirmation at a Reserved table, as QrOrderSubmittedConsumer leaves it.</summary>
    public async Task<(Guid OrderId, string TableNumber)> SeedQrPendingOrderAsync()
    {
        var tableId = Guid.NewGuid();
        var tableNumber = "QR-" + tableId.ToString("N")[..6];
        await ExecAsync(
            "INSERT INTO table_mgmt.tables (table_id, table_number, capacity, active, current_status) VALUES (@id, @number, 4, true, 'Reserved');",
            ("id", tableId), ("number", tableNumber));
        var productId = Guid.NewGuid();
        await ExecAsync(
            "INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active) VALUES (@id, @sku, 'Çay', 1, 1, true);",
            ("id", productId), ("sku", "QRP-" + productId.ToString("N")[..8]));
        var orderId = Guid.NewGuid();
        var item = new OrderItem(Guid.NewGuid(), orderId, productId, "Çay", quantity: 2, unitPrice: 15m, taxRate: 10m,
            status: OrderItemState.Active, kitchenState: KitchenState.Sent);
        await new PostgresOrderRepository(DataSource).AddAsync(new Order(
            orderId, OrderSource.Qr, "QR-" + orderId.ToString("N")[..8], [item], tableId: tableId,
            sourceReferenceId: Guid.NewGuid(), status: OrderState.PendingConfirmation, confirmationStatus: ConfirmationStatus.Pending));
        await ExecAsync("UPDATE table_mgmt.tables SET current_order_id = @order WHERE table_id = @table;", ("order", orderId), ("table", tableId));
        return (orderId, tableNumber);
    }

    public async Task<long> RowVersionAsync(Guid orderId)
    {
        await using var command = DataSource.CreateCommand("SELECT row_version FROM orders.orders WHERE order_id = @id;");
        command.Parameters.AddWithValue("id", orderId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    public async Task<long> CountPublicationsAsync()
    {
        await using var command = DataSource.CreateCommand("SELECT count(*) FROM online_ordering.catalog_publications;");
        return (long)(await command.ExecuteScalarAsync())!;
    }

    public async Task<long> CountAllAsync()
    {
        await using var command = DataSource.CreateCommand("SELECT count(*) FROM online_ordering.yemeksepeti_webhook_inbox;");
        return (long)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>An active, 10%-taxed product mapped from a fresh SKU, with <paramref name="onHand"/> portions in stock.</summary>
    public async Task<(Guid ProductId, string Sku)> SeedSellableProductAsync(decimal onHand, string name = "Lahmacun")
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var taxProfileId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var stockItemId = Guid.NewGuid();
        await ExecAsync(
            """
            INSERT INTO catalog.tax_profiles (tax_profile_id, code, name, vat_rate) VALUES (@tax, @tax_code, 'KDV %10', 10);
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active, tax_profile_id)
            VALUES (@product, @product_sku, @name, 1, 1, true, @tax);
            INSERT INTO inventory.stock_locations (id, code, name, location_type) VALUES (@location, @location_code, 'Online Test Pass', 'Counter');
            INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, default_location_id)
            VALUES (@stock_item, @stock_code, 'Online Test Portion', 'Portion', 'adet', @location);
            INSERT INTO inventory.product_stock_mappings (product_id, stock_item_id, quantity_multiplier) VALUES (@product, @stock_item, 1.0);
            INSERT INTO inventory.stock_balances (stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity, reserved_quantity, available_quantity)
            VALUES (@balance, @stock_item, @location, @on_hand, 0, @on_hand);
            """,
            ("tax", taxProfileId), ("tax_code", "KDV-" + suffix), ("product", productId), ("product_sku", "ON-" + suffix),
            ("name", name), ("location", locationId), ("location_code", "ONL-" + suffix), ("stock_item", stockItemId),
            ("stock_code", "ONS-" + suffix), ("balance", Guid.NewGuid()), ("on_hand", onHand));

        var sku = "ys-" + suffix;
        await new PostgresYemeksepetiProductMappingService(
                DataSource,
                new PostgresProductRepository(DataSource),
                new PostgresProductModifierGroupRepository(DataSource),
                new PostgresModifierGroupRepository(DataSource))
            .MapAsync(sku, productId, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Guid.NewGuid());
        return (productId, sku);
    }

    /// <summary>Another channel's order holds one portion of <paramref name="productId"/> through the real arbiter.</summary>
    public async Task<CrossChannelReservationOutcome> HoldElsewhereAsync(Guid productId)
    {
        var items = new PostgresStockItemRepository(DataSource);
        var locations = new PostgresStockLocationRepository(DataSource);
        var reservations = new PostgresPortionReservationRepository(DataSource);
        var balances = new PostgresStockBalanceRepository(DataSource);
        var arbiter = new PostgresCrossChannelPortionArbiter(
            DataSource,
            balances,
            new PortionCancellationDecisionService(
                reservations,
                new PortionReservationLifecycleService(reservations, items, locations),
                new ReservationBalanceProjector(new PostgresReservationBalanceRepository(DataSource)),
                new WasteRecordingService(
                    new PostgresInventoryTransactionRunner(DataSource), new PostgresWasteRecordRepository(DataSource),
                    new PostgresStockMovementRepository(DataSource), items, locations, balances, new UnitConverter()),
                new PostgresKitchenItemStateProvider(DataSource)));

        await using var connection = await DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var result = await arbiter.ReserveAsync(
            new CrossChannelReservationRequest(
                ReservationChannel.Qr, "qr-" + Guid.NewGuid().ToString("N"), Guid.NewGuid(), Guid.NewGuid(),
                new[] { new CrossChannelReservationLine(Guid.NewGuid(), productId, 1m) }),
            connection, transaction);
        await transaction.CommitAsync();
        return result.Outcome;
    }

    public async Task<IReadOnlyList<(Guid OrderId, string Status, string OrderNumber, string? Notes)>> OnlineOrdersAsync(string externalOrderId)
    {
        await using var command = DataSource.CreateCommand(
            "SELECT order_id, status, order_number, notes FROM orders.orders WHERE source = 'Online' AND source_external_id = @id;");
        command.Parameters.AddWithValue("id", externalOrderId);
        var rows = new List<(Guid, string, string, string?)>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add((reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
        return rows;
    }

    public async Task<IReadOnlyList<(string Status, string? Outcome, Guid? OrderId, string? Detail, int Attempts)>> InboxAsync(string externalOrderId)
    {
        await using var command = DataSource.CreateCommand(
            """
            SELECT provider_status, processing_outcome, order_id, outcome_detail::text, processing_attempts
            FROM online_ordering.yemeksepeti_webhook_inbox
            WHERE external_order_id = @id
            ORDER BY received_at, inbox_id;
            """);
        command.Parameters.AddWithValue("id", externalOrderId);
        var rows = new List<(string, string?, Guid?, string?, int)>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetInt32(4)));
        }
        return rows;
    }

    /// <summary>V12-RMD-007: an order's notes.</summary>
    public async Task<string?> OrderNotesAsync(Guid orderId)
    {
        await using var command = DataSource.CreateCommand("SELECT notes FROM orders.orders WHERE order_id = @id;");
        command.Parameters.AddWithValue("id", orderId);
        return await command.ExecuteScalarAsync() as string;
    }

    /// <summary>V12-RMD-007: audit events of one name for one order, with their actor.</summary>
    public async Task<IReadOnlyList<Guid>> AuditActorsAsync(Guid orderId, string eventName)
    {
        await using var command = DataSource.CreateCommand(
            "SELECT actor_id FROM audit.audit_events WHERE aggregate_id = @id AND event_name = @name ORDER BY occurred_at;");
        command.Parameters.AddWithValue("id", orderId);
        command.Parameters.AddWithValue("name", eventName);
        var actors = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            actors.Add(reader.GetGuid(0));
        return actors;
    }

    /// <summary>V12-RMD-004: makes every waiting retry due now.</summary>
    public Task ExpireRetryWaitsAsync() =>
        ExecAsync("UPDATE online_ordering.yemeksepeti_webhook_inbox SET next_attempt_at = now() - interval '1 second' WHERE processed_at IS NULL AND next_attempt_at IS NOT NULL;");

    /// <summary>V12-RMD-004: seconds until the event's next attempt is due (negative when due).</summary>
    public async Task<double> SecondsUntilNextAttemptAsync(string externalOrderId)
    {
        await using var command = DataSource.CreateCommand(
            "SELECT EXTRACT(EPOCH FROM next_attempt_at - now())::float8 FROM online_ordering.yemeksepeti_webhook_inbox WHERE external_order_id = @id;");
        command.Parameters.AddWithValue("id", externalOrderId);
        return (double)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>V12-RMD-004: the notes of every item of an order.</summary>
    public async Task<IReadOnlyList<string?>> OrderItemNotesAsync(Guid orderId)
    {
        await using var command = DataSource.CreateCommand("SELECT notes FROM orders.order_items WHERE order_id = @id ORDER BY created_at, order_item_id;");
        command.Parameters.AddWithValue("id", orderId);
        var notes = new List<string?>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            notes.Add(reader.IsDBNull(0) ? null : reader.GetString(0));
        return notes;
    }

    /// <summary>V12-RMD-004: stock arrives for a product's single mapped stock item.</summary>
    public Task AddOnHandAsync(Guid productId, decimal quantity) =>
        ExecAsync(
            """
            UPDATE inventory.stock_balances b
            SET on_hand_quantity = on_hand_quantity + @quantity, available_quantity = available_quantity + @quantity,
                row_version = row_version + 1
            FROM inventory.product_stock_mappings m
            WHERE m.product_id = @product AND b.stock_item_id = m.stock_item_id;
            """,
            ("product", productId), ("quantity", quantity));

    public async Task<IReadOnlyList<(string Status, string Channel)>> HoldsAsync(Guid orderId)
    {
        await using var command = DataSource.CreateCommand(
            "SELECT status, metadata->>'channel' FROM inventory.portion_reservations WHERE order_id = @id;");
        command.Parameters.AddWithValue("id", orderId);
        var rows = new List<(string, string)>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add((reader.GetString(0), reader.GetString(1)));
        return rows;
    }

    public async Task<long> KitchenTicketItemCountAsync(Guid orderId)
    {
        await using var command = DataSource.CreateCommand(
            """
            SELECT count(*) FROM kitchen.kitchen_ticket_items i
            JOIN kitchen.kitchen_tickets t ON t.id = i.ticket_id
            WHERE t.order_id = @id;
            """);
        command.Parameters.AddWithValue("id", orderId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    public async Task<decimal> AvailableAsync(Guid productId)
    {
        await using var command = DataSource.CreateCommand(
            """
            SELECT b.available_quantity FROM inventory.product_stock_mappings m
            JOIN inventory.stock_balances b ON b.stock_item_id = m.stock_item_id
            WHERE m.product_id = @id;
            """);
        command.Parameters.AddWithValue("id", productId);
        return (decimal)(await command.ExecuteScalarAsync())!;
    }

    public async Task<IReadOnlyList<string>> OutboundStatusUpdatesAsync(string externalOrderId)
    {
        await using var command = DataSource.CreateCommand(
            """
            SELECT convert_from(payload_envelope, 'UTF8')
            FROM outbox_messages
            WHERE event_type = 'online-ordering.yemeksepeti.status-update-requested.v1'
              AND convert_from(payload_envelope, 'UTF8')::jsonb->>'externalOrderId' = @id
            ORDER BY created_at, id;
            """);
        command.Parameters.AddWithValue("id", externalOrderId);
        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add(reader.GetString(0));
        return rows;
    }

    public Task MarkKitchenPreparingAsync(Guid orderId) =>
        ExecAsync(
            """
            UPDATE kitchen.kitchen_ticket_items SET status = 'Preparing'
            WHERE ticket_id IN (SELECT id FROM kitchen.kitchen_tickets WHERE order_id = @id);
            """,
            ("id", orderId));

    public async Task<decimal> OnHandAsync(Guid productId)
    {
        await using var command = DataSource.CreateCommand(
            """
            SELECT b.on_hand_quantity FROM inventory.product_stock_mappings m
            JOIN inventory.stock_balances b ON b.stock_item_id = m.stock_item_id
            WHERE m.product_id = @id;
            """);
        command.Parameters.AddWithValue("id", productId);
        return (decimal)(await command.ExecuteScalarAsync())!;
    }

    public async Task CorruptEnvelopeAsync(string externalOrderId)
    {
        await ExecAsync(
            "UPDATE online_ordering.yemeksepeti_webhook_inbox SET payload_envelope = '\\x00'::bytea WHERE external_order_id = @id;",
            ("id", externalOrderId));
    }

    /// <summary>V12-ONL-006: a single text value (or null).</summary>
    public async Task<string?> ScalarTextAsync(string sql)
    {
        await using var command = DataSource.CreateCommand(sql);
        return await command.ExecuteScalarAsync() as string;
    }

    public async Task ExecAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = DataSource.CreateCommand(sql);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }
}
