using ALKAROS.Host.Composition;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Tests.Fixtures;
using ALKAROS.Identity.DeviceSessions;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Tests.DualScreen;

[Collection("Host database password environment")]
public sealed class DualScreenStoreTests : IAsyncLifetime
{
    private readonly TestDatabase _database = new();
    private NpgsqlDataSource? _dataSource;
    private DualScreenStore? _store;
    private Guid _productId;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        var root = FindRepositoryRoot();
        var exit = HostComposition.Run(
            new HostCompositionOptions(
                Path.Combine(root, "database", "MigrationComposition", "order.json"),
                Path.Combine(root, "database", "migrations"),
                _database.PsqlOptions),
            TextWriter.Null);
        Assert.Equal(HostExitCode.Success, exit);

        _dataSource = NpgsqlDataSource.Create(CreateConnectionString());
        _store = new DualScreenStore(_dataSource);
        _productId = Guid.NewGuid();
        await SeedProductAsync(_productId);
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
            await _dataSource.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task SequentialAddsCommit999AndRejectCumulative1000WithoutChangingRevision()
    {
        var terminalId = Guid.NewGuid();
        var order = await _store!.StartOrderAsync(terminalId, Guid.NewGuid(), CancellationToken.None);

        var accepted = await _store.AddItemAsync(
            terminalId,
            order.OrderId,
            new AddOrderItemRequest(_productId, 999m, order.Revision),
            CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _store.AddItemAsync(
            terminalId,
            order.OrderId,
            new AddOrderItemRequest(_productId, 1m, accepted.Revision),
            CancellationToken.None));

        Assert.Equal(999m, await ScalarAsync<decimal>(
            "SELECT sum(quantity) FROM orders.order_items WHERE order_id = @order_id AND product_id = @product_id;",
            ("order_id", order.OrderId),
            ("product_id", _productId)));
        Assert.Equal(accepted.Revision, await ScalarAsync<long>(
            "SELECT row_version FROM orders.orders WHERE order_id = @order_id;",
            ("order_id", order.OrderId)));
    }

    [Fact]
    public async Task ConcurrentAddsWithSameRevisionCannotCommitCumulative1000()
    {
        var terminalId = Guid.NewGuid();
        var order = await _store!.StartOrderAsync(terminalId, Guid.NewGuid(), CancellationToken.None);
        var seeded = await _store.AddItemAsync(
            terminalId,
            order.OrderId,
            new AddOrderItemRequest(_productId, 998m, order.Revision),
            CancellationToken.None);

        var first = CaptureAsync(() => _store.AddItemAsync(
            terminalId,
            order.OrderId,
            new AddOrderItemRequest(_productId, 1m, seeded.Revision),
            CancellationToken.None));
        var second = CaptureAsync(() => _store.AddItemAsync(
            terminalId,
            order.OrderId,
            new AddOrderItemRequest(_productId, 1m, seeded.Revision),
            CancellationToken.None));

        var results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result.Result is not null);
        Assert.Single(results, result => result.Exception is DualScreenConflictException);
        Assert.Equal(999m, await ScalarAsync<decimal>(
            "SELECT sum(quantity) FROM orders.order_items WHERE order_id = @order_id AND product_id = @product_id;",
            ("order_id", order.OrderId),
            ("product_id", _productId)));
        Assert.Equal(seeded.Revision + 1, await ScalarAsync<long>(
            "SELECT row_version FROM orders.orders WHERE order_id = @order_id;",
            ("order_id", order.OrderId)));
    }

    [Fact]
    public async Task AComplimentaryLineIsShownAtItsTaxInclusivePriceNotWithTaxAddedOnTop()
    {
        var terminalId = Guid.NewGuid();
        var displayId = Guid.NewGuid();
        var order = await _store!.StartOrderAsync(terminalId, displayId, CancellationToken.None);
        await _store.AddItemAsync(
            terminalId, order.OrderId, new AddOrderItemRequest(_productId, 4m, order.Revision), CancellationToken.None);
        await using (var comp = _dataSource!.CreateCommand(
            """
            UPDATE orders.order_items
            SET status = 'Complimentary', tax_rate = 10, discount_amount = 4, net_amount = 0, tax_amount = 0, gross_amount = 0
            WHERE order_id = @order_id;
            """))
        {
            comp.Parameters.AddWithValue("order_id", order.OrderId);
            await comp.ExecuteNonQueryAsync();
        }

        var snapshot = await _store.GetSnapshotAsync(displayId, terminalId, CancellationToken.None);

        Assert.Equal(4.00m, Assert.Single(snapshot.Lines).LineTotal);
    }

    [Fact]
    public async Task TableBoundOrderCommitsOrderAndTablePointersTogether()
    {
        var terminalId = Guid.NewGuid();
        var tableId = await SeedTableAsync("S-BOUND", "Available", 1);

        var order = await _store!.StartOrderAsync(
            terminalId,
            Guid.NewGuid(),
            new StartOrderRequest(tableId, 1),
            CancellationToken.None);

        Assert.Equal(tableId, await ScalarAsync<Guid>(
            "SELECT table_id FROM orders.orders WHERE order_id = @order_id;",
            ("order_id", order.OrderId)));
        Assert.Equal(order.OrderId, await ScalarAsync<Guid>(
            "SELECT current_order_id FROM table_mgmt.tables WHERE table_id = @table_id;",
            ("table_id", tableId)));
        Assert.Equal("Occupied", await ScalarAsync<string>(
            "SELECT current_status FROM table_mgmt.tables WHERE table_id = @table_id;",
            ("table_id", tableId)));
        Assert.Equal(2L, await ScalarAsync<long>(
            "SELECT row_version FROM table_mgmt.tables WHERE table_id = @table_id;",
            ("table_id", tableId)));
        Assert.Equal(order.OrderId, await ScalarAsync<Guid>(
            "SELECT active_order_id FROM customer_display.terminals WHERE terminal_id = @terminal_id;",
            ("terminal_id", terminalId)));
    }

    [Fact]
    public async Task StaleTableVersionStillSeatsAnAvailableTableButBusyTableIsRejected()
    {
        var firstTerminalId = Guid.NewGuid();
        var secondTerminalId = Guid.NewGuid();
        var tableId = await SeedTableAsync("S-CONFLICT", "Available", 1);

        // Stale ExpectedTableRowVersion (real version is 1) must NOT block a seat
        // when the table is genuinely available: the waiter's screen being one
        // version behind is not a real conflict (V1-RMD-090).
        var first = await _store!.StartOrderAsync(
            firstTerminalId,
            Guid.NewGuid(),
            new StartOrderRequest(tableId, 2),
            CancellationToken.None);
        Assert.Equal(1L, await ScalarAsync<long>(
            "SELECT count(*) FROM orders.orders WHERE table_id = @table_id;",
            ("table_id", tableId)));
        Assert.Equal("Occupied", await ScalarAsync<string>(
            "SELECT current_status FROM table_mgmt.tables WHERE table_id = @table_id;",
            ("table_id", tableId)));

        // A different terminal trying to seat the now-busy table is still a real
        // conflict and is rejected without creating a second order.
        await Assert.ThrowsAsync<DualScreenConflictException>(() => _store.StartOrderAsync(
            secondTerminalId,
            Guid.NewGuid(),
            new StartOrderRequest(tableId, 2),
            CancellationToken.None));

        Assert.Equal(1L, await ScalarAsync<long>(
            "SELECT count(*) FROM orders.orders WHERE table_id = @table_id;",
            ("table_id", tableId)));
        Assert.Equal(first.OrderId, await ScalarAsync<Guid>(
            "SELECT current_order_id FROM table_mgmt.tables WHERE table_id = @table_id;",
            ("table_id", tableId)));
        Assert.Equal(0L, await ScalarAsync<long>(
            "SELECT count(*) FROM customer_display.terminals WHERE terminal_id = @terminal_id AND active_order_id IS NOT NULL;",
            ("terminal_id", secondTerminalId)));
    }

    [Fact]
    public async Task NonAvailableTableIsRejectedEvenWithAMatchingVersion()
    {
        var terminalId = Guid.NewGuid();
        var tableId = await SeedTableAsync("S-RESERVED", "Reserved", 1);

        // A genuine blocker (table not Available) still rejects with a specific
        // error even when the caller's version is exactly right.
        await Assert.ThrowsAsync<DualScreenConflictException>(() => _store!.StartOrderAsync(
            terminalId,
            Guid.NewGuid(),
            new StartOrderRequest(tableId, 1),
            CancellationToken.None));
        Assert.Equal(0L, await ScalarAsync<long>(
            "SELECT count(*) FROM orders.orders WHERE table_id = @table_id;",
            ("table_id", tableId)));
    }

    [Fact]
    public async Task ConcurrentTableStartsAllowOnlyOneOrderAndLeaveConsistentPointers()
    {
        var firstTerminalId = Guid.NewGuid();
        var secondTerminalId = Guid.NewGuid();
        var tableId = await SeedTableAsync("S-RACE", "Available", 1);

        var first = CaptureAsync(() => _store!.StartOrderAsync(
            firstTerminalId,
            Guid.NewGuid(),
            new StartOrderRequest(tableId, 1),
            CancellationToken.None));
        var second = CaptureAsync(() => _store!.StartOrderAsync(
            secondTerminalId,
            Guid.NewGuid(),
            new StartOrderRequest(tableId, 1),
            CancellationToken.None));
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result.Result is not null);
        Assert.Single(results, result => result.Exception is DualScreenConflictException);
        Assert.Equal(1L, await ScalarAsync<long>(
            "SELECT count(*) FROM orders.orders WHERE table_id = @table_id;",
            ("table_id", tableId)));
        Assert.Equal(1L, await ScalarAsync<long>(
            "SELECT count(*) FROM table_mgmt.tables WHERE table_id = @table_id AND current_status = 'Occupied' AND current_order_id IS NOT NULL;",
            ("table_id", tableId)));
    }

    [Fact]
    public async Task AuthenticateCashierByCookieResolvesBoundTerminalFromDeviceId()
    {
        var terminalId = Guid.NewGuid();
        await _store!.EnsureTerminalAsync(terminalId, CancellationToken.None);
        await SeedCashierSessionAsync(terminalId, "raw-cookie-token", "Kasiyer Ada");

        var principal = await _store.AuthenticateCashierByCookieAsync("raw-cookie-token", CancellationToken.None);

        Assert.NotNull(principal);
        Assert.Equal(terminalId, principal!.TerminalId);
        Assert.Equal("Kasiyer Ada", principal.DisplayName);
    }

    [Fact]
    public async Task AuthenticateCashierByCookieRejectsUnknownToken()
    {
        var principal = await _store!.AuthenticateCashierByCookieAsync("no-such-token", CancellationToken.None);
        Assert.Null(principal);
    }

    // V1-WTR-054: the shared terminal catalog (Garson + Kasa) reports how
    // many more units a directly stock-mapped product can still sell.

    [Fact]
    public async Task GetCatalogAsyncReturnsRemainingCountFromDirectStockMapping()
    {
        var productId = Guid.NewGuid();
        await SeedProductAsync(productId);
        await SeedStockMappingAsync(productId, quantityMultiplier: 1.0m, onHand: 2.0m);

        var page = await _store!.GetCatalogAsync(null, 50, null, CancellationToken.None);

        var item = Assert.Single(page.Items, p => p.ProductId == productId);
        Assert.Equal(2, item.RemainingCount);
    }

    [Fact]
    public async Task GetCatalogAsyncLeavesRemainingCountNullWithoutStockMapping()
    {
        // _productId (seeded in InitializeAsync) carries no stock mapping.
        var page = await _store!.GetCatalogAsync(null, 50, null, CancellationToken.None);

        var item = Assert.Single(page.Items, p => p.ProductId == _productId);
        Assert.Null(item.RemainingCount);
    }

    // V1-RMD-233: found by an independent audit (2026-09-17) — a product
    // with more than one product_stock_mappings row (a real BOM scenario,
    // same reasoning as OrderDtoAssembler.WithAvailableStockAsync,
    // V1-RMD-143) had never been tested here; only the single-mapping case
    // above was covered.
    [Fact]
    public async Task GetCatalogAsyncPicksTheMostRestrictiveMappingWhenAProductHasMultipleStockMappings()
    {
        var productId = Guid.NewGuid();
        await SeedProductAsync(productId);
        // Mapping A alone would allow 10; mapping B alone would allow 3
        // (FLOOR(6 / 2)) — the limiting one (B) must win.
        await SeedStockMappingAsync(productId, quantityMultiplier: 1.0m, onHand: 10.0m);
        await SeedStockMappingAsync(productId, quantityMultiplier: 2.0m, onHand: 6.0m);

        var page = await _store!.GetCatalogAsync(null, 50, null, CancellationToken.None);

        var item = Assert.Single(page.Items, p => p.ProductId == productId);
        Assert.Equal(3, item.RemainingCount);
    }

    // V1-RMD-233: a mapping row with no corresponding stock_balances row is
    // an INTENTIONAL "unlimited" (RemainingCount stays null), not a bug —
    // the LATERAL join's MIN() ignores NULL rows the same way
    // OrderDtoAssembler.WithAvailableStockAsync's own loop `continue`s past
    // a mapping with no balance (same V1-RMD-143 reasoning, referenced
    // directly in this query's own comment). Decision: keep "unlimited",
    // not "0" — a mapping that exists but has never had a balance entered
    // (e.g. a brand-new stock item awaiting its first count) is a data-
    // entry gap, not proof the product is out of stock; showing "0" would
    // hide the product from sale for a reason the operator never intended,
    // whereas "unlimited" matches what every other unmapped product already
    // shows and is silently corrected the moment a balance is recorded.
    [Fact]
    public async Task GetCatalogAsyncLeavesRemainingCountNullWhenMappingHasNoBalanceRow()
    {
        var productId = Guid.NewGuid();
        await SeedProductAsync(productId);
        await SeedStockMappingWithNoBalanceAsync(productId, quantityMultiplier: 1.0m);

        var page = await _store!.GetCatalogAsync(null, 50, null, CancellationToken.None);

        var item = Assert.Single(page.Items, p => p.ProductId == productId);
        Assert.Null(item.RemainingCount);
    }

    [Fact]
    public async Task GetCatalogAsyncDropsProductWhenStockExhausted()
    {
        var productId = Guid.NewGuid();
        await SeedProductAsync(productId);
        await SeedStockMappingAsync(productId, quantityMultiplier: 1.0m, onHand: 0.0m);

        var page = await _store!.GetCatalogAsync(null, 50, null, CancellationToken.None);

        Assert.DoesNotContain(page.Items, p => p.ProductId == productId);
    }

    [Fact]
    public async Task GetCatalogAsyncPaginationIsUnaffectedByAnExhaustedProduct()
    {
        // The already-seeded _productId (no mapping, always visible) is the
        // only product this page should ever see once the exhausted one is
        // filtered out — a page-size-1 request must not come back empty or
        // report a phantom next page because of the dropped row.
        var exhaustedProductId = Guid.NewGuid();
        await SeedProductAsync(exhaustedProductId);
        await SeedStockMappingAsync(exhaustedProductId, quantityMultiplier: 1.0m, onHand: 0.0m);

        var page = await _store!.GetCatalogAsync(null, 1, null, CancellationToken.None);

        var item = Assert.Single(page.Items);
        Assert.Equal(_productId, item.ProductId);
        Assert.Null(page.NextCursor);
    }

    private async Task SeedStockMappingAsync(Guid productId, decimal quantityMultiplier, decimal onHand)
    {
        var locationId = Guid.NewGuid();
        var stockItemId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var command = _dataSource!.CreateCommand(
            """
            INSERT INTO inventory.stock_locations (id, code, name, location_type)
            VALUES (@location_id, @location_code, 'V1-WTR-054 Test Location', 'Counter');
            INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, default_location_id)
            VALUES (@stock_item_id, @stock_item_code, 'V1-WTR-054 Test Stock Item', 'Portion', 'adet', @location_id);
            INSERT INTO inventory.product_stock_mappings (product_id, stock_item_id, quantity_multiplier)
            VALUES (@product_id, @stock_item_id, @quantity_multiplier);
            INSERT INTO inventory.stock_balances (stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity, reserved_quantity, available_quantity)
            VALUES (@balance_id, @stock_item_id, @location_id, @on_hand, 0, @on_hand);
            """);
        command.Parameters.AddWithValue("location_id", locationId);
        command.Parameters.AddWithValue("location_code", "WTR054-" + suffix);
        command.Parameters.AddWithValue("stock_item_id", stockItemId);
        command.Parameters.AddWithValue("stock_item_code", "WTR054-" + suffix);
        command.Parameters.AddWithValue("product_id", productId);
        command.Parameters.AddWithValue("quantity_multiplier", quantityMultiplier);
        command.Parameters.AddWithValue("balance_id", Guid.NewGuid());
        command.Parameters.AddWithValue("on_hand", onHand);
        await command.ExecuteNonQueryAsync();
    }

    private async Task SeedStockMappingWithNoBalanceAsync(Guid productId, decimal quantityMultiplier)
    {
        var locationId = Guid.NewGuid();
        var stockItemId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var command = _dataSource!.CreateCommand(
            """
            INSERT INTO inventory.stock_locations (id, code, name, location_type)
            VALUES (@location_id, @location_code, 'V1-RMD-233 Test Location', 'Counter');
            INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, default_location_id)
            VALUES (@stock_item_id, @stock_item_code, 'V1-RMD-233 Test Stock Item', 'Portion', 'adet', @location_id);
            INSERT INTO inventory.product_stock_mappings (product_id, stock_item_id, quantity_multiplier)
            VALUES (@product_id, @stock_item_id, @quantity_multiplier);
            """);
        command.Parameters.AddWithValue("location_id", locationId);
        command.Parameters.AddWithValue("location_code", "RMD233-" + suffix);
        command.Parameters.AddWithValue("stock_item_id", stockItemId);
        command.Parameters.AddWithValue("stock_item_code", "RMD233-" + suffix);
        command.Parameters.AddWithValue("product_id", productId);
        command.Parameters.AddWithValue("quantity_multiplier", quantityMultiplier);
        await command.ExecuteNonQueryAsync();
    }

    private async Task SeedCashierSessionAsync(Guid terminalId, string rawToken, string displayName)
    {
        var userId = Guid.NewGuid();
        await using (var userCommand = _dataSource!.CreateCommand(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'x', @display_name, true);
            """))
        {
            userCommand.Parameters.AddWithValue("user_id", userId);
            userCommand.Parameters.AddWithValue("username", $"cashier-{userId:N}");
            userCommand.Parameters.AddWithValue("display_name", displayName);
            await userCommand.ExecuteNonQueryAsync();
        }

        await using var sessionCommand = _dataSource!.CreateCommand(
            """
            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, expires_at)
            VALUES (@session_id, @user_id, @device_id, @token_hash, now() + interval '1 hour');
            """);
        sessionCommand.Parameters.AddWithValue("session_id", Guid.NewGuid());
        sessionCommand.Parameters.AddWithValue("user_id", userId);
        sessionCommand.Parameters.AddWithValue("device_id", $"cashier:{terminalId:D}");
        sessionCommand.Parameters.AddWithValue("token_hash", DeviceSessionToken.Hash(rawToken));
        await sessionCommand.ExecuteNonQueryAsync();
    }

    private static async Task<MutationAttempt<T>> CaptureAsync<T>(Func<Task<T>> action)
        where T : class
    {
        try
        {
            return new MutationAttempt<T>(await action(), null);
        }
        catch (Exception exception)
        {
            return new MutationAttempt<T>(null, exception);
        }
    }

    private async Task SeedProductAsync(Guid productId)
    {
        await using var command = _dataSource!.CreateCommand(
            """
            INSERT INTO catalog.products (
                product_id, sku, name, category_id, tax_profile_id, product_type,
                stock_mode, active, display_order, current_price)
            VALUES (
                @product_id, @sku, 'Quantity invariant product', NULL, NULL, 1,
                1, true, 0, 1.00);
            """);
        command.Parameters.AddWithValue("product_id", productId);
        command.Parameters.AddWithValue("sku", $"QTY-{productId:N}");
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid> SeedTableAsync(string tableNumber, string status, long rowVersion)
    {
        var tableId = Guid.NewGuid();
        await using var command = _dataSource!.CreateCommand(
            """
            INSERT INTO table_mgmt.tables
                (table_id, zone_id, table_number, capacity, active, current_status,
                 current_order_id, current_bill_id, row_version)
            VALUES (@table_id, NULL, @table_number, 2, true, @status, NULL, NULL, @row_version);
            """);
        command.Parameters.AddWithValue("table_id", tableId);
        command.Parameters.AddWithValue("table_number", tableNumber);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("row_version", rowVersion);
        await command.ExecuteNonQueryAsync();
        return tableId;
    }

    private async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _dataSource!.CreateCommand(sql);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        return (T)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Scalar returned null."));
    }

    private string CreateConnectionString()
    {
        var uri = new Uri(_database.Url);
        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
            Username = uri.UserInfo,
            Password = _database.PsqlOptions.Password,
        }.ConnectionString;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "database", "MigrationComposition", "order.json")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }

    private sealed record MutationAttempt<T>(T? Result, Exception? Exception) where T : class;
}
