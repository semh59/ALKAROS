using ALKAROS.Catalog.ProductCatalog;
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

    public async Task CorruptEnvelopeAsync(string externalOrderId)
    {
        await ExecAsync(
            "UPDATE online_ordering.yemeksepeti_webhook_inbox SET payload_envelope = '\\x00'::bytea WHERE external_order_id = @id;",
            ("id", externalOrderId));
    }

    private async Task ExecAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = DataSource.CreateCommand(sql);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }
}
