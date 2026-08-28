using ALKAROS.Host.Composition;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Tests.Fixtures;
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
                Path.Combine(root, "database", "migrations", "V1"),
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
        var order = await _store!.StartOrderAsync(terminalId, CancellationToken.None);

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
        var order = await _store!.StartOrderAsync(terminalId, CancellationToken.None);
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
    public async Task TableBoundOrderCommitsOrderAndTablePointersTogether()
    {
        var terminalId = Guid.NewGuid();
        var tableId = await SeedTableAsync("S-BOUND", "Available", 1);

        var order = await _store!.StartOrderAsync(
            terminalId,
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
    public async Task TableBoundOrderRejectsStaleOrBusyTablesWithoutCreatingAnotherOrder()
    {
        var firstTerminalId = Guid.NewGuid();
        var secondTerminalId = Guid.NewGuid();
        var tableId = await SeedTableAsync("S-CONFLICT", "Available", 1);

        await Assert.ThrowsAsync<DualScreenConflictException>(() => _store!.StartOrderAsync(
            firstTerminalId,
            new StartOrderRequest(tableId, 2),
            CancellationToken.None));
        Assert.Equal(0L, await ScalarAsync<long>(
            "SELECT count(*) FROM orders.orders WHERE table_id = @table_id;",
            ("table_id", tableId)));

        var first = await _store!.StartOrderAsync(
            firstTerminalId,
            new StartOrderRequest(tableId, 1),
            CancellationToken.None);
        await Assert.ThrowsAsync<DualScreenConflictException>(() => _store.StartOrderAsync(
            secondTerminalId,
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
    public async Task ConcurrentTableStartsAllowOnlyOneOrderAndLeaveConsistentPointers()
    {
        var firstTerminalId = Guid.NewGuid();
        var secondTerminalId = Guid.NewGuid();
        var tableId = await SeedTableAsync("S-RACE", "Available", 1);

        var first = CaptureAsync(() => _store!.StartOrderAsync(
            firstTerminalId,
            new StartOrderRequest(tableId, 1),
            CancellationToken.None));
        var second = CaptureAsync(() => _store!.StartOrderAsync(
            secondTerminalId,
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
