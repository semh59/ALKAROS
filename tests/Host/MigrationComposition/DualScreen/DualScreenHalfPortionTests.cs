using ALKAROS.Host.Composition;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Tests.Fixtures;
using ALKAROS.Orders.OrderAggregate;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Tests.DualScreen;

/// <summary>A product is sold in whole units unless it is marked as allowing half portions.</summary>
[Collection("Host database password environment")]
public sealed class DualScreenHalfPortionTests : IAsyncLifetime
{
    private readonly TestDatabase _database = new();
    private NpgsqlDataSource? _dataSource;
    private DualScreenStore? _store;

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

        var uri = new Uri(_database.Url);
        _dataSource = NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
            Username = uri.UserInfo,
            Password = _database.PsqlOptions.Password,
        }.ConnectionString);
        _store = new DualScreenStore(_dataSource);
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
            await _dataSource.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Theory]
    [InlineData(false, 1.5)]
    [InlineData(false, 0.5)]
    [InlineData(true, 1.3)]
    [InlineData(true, 0.25)]
    public async Task AnUnitTheProductDoesNotAllowIsRefusedOnAdd(bool allowsHalfPortion, double quantity)
    {
        var productId = await SeedProductAsync(allowsHalfPortion);
        var terminalId = Guid.NewGuid();
        var order = await _store!.StartOrderAsync(terminalId, Guid.NewGuid(), CancellationToken.None);

        await Assert.ThrowsAsync<OrderQuantityNotAllowedException>(() => _store.AddItemAsync(
            terminalId, order.OrderId, new AddOrderItemRequest(productId, (decimal)quantity, order.Revision), CancellationToken.None));

        Assert.Equal(0L, await ItemCountAsync(order.OrderId));
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    [InlineData(true, 1.5)]
    [InlineData(true, 0.5)]
    public async Task AnAllowedUnitIsAddedAndStoredExactly(bool allowsHalfPortion, double quantity)
    {
        var productId = await SeedProductAsync(allowsHalfPortion);
        var terminalId = Guid.NewGuid();
        var order = await _store!.StartOrderAsync(terminalId, Guid.NewGuid(), CancellationToken.None);

        await _store.AddItemAsync(
            terminalId, order.OrderId, new AddOrderItemRequest(productId, (decimal)quantity, order.Revision), CancellationToken.None);

        Assert.Equal((decimal)quantity, await QuantityAsync(order.OrderId));
    }

    [Fact]
    public async Task ChangingALineToAFractionIsRefusedUnlessTheProductAllowsIt()
    {
        var wholeOnly = await SeedProductAsync(false);
        var halves = await SeedProductAsync(true);
        var terminalId = Guid.NewGuid();
        var order = await _store!.StartOrderAsync(terminalId, Guid.NewGuid(), CancellationToken.None);
        var first = await _store.AddItemAsync(
            terminalId, order.OrderId, new AddOrderItemRequest(wholeOnly, 1m, order.Revision), CancellationToken.None);
        var second = await _store.AddItemAsync(
            terminalId, order.OrderId, new AddOrderItemRequest(halves, 1m, first.Revision), CancellationToken.None);
        var wholeOnlyLine = await ItemIdAsync(order.OrderId, wholeOnly);
        var halvesLine = await ItemIdAsync(order.OrderId, halves);

        await Assert.ThrowsAsync<OrderQuantityNotAllowedException>(() => _store.ChangeItemQuantityAsync(
            terminalId, order.OrderId, wholeOnlyLine, new ChangeOrderItemQuantityRequest(1.5m, second.Revision), CancellationToken.None));
        await _store.ChangeItemQuantityAsync(
            terminalId, order.OrderId, halvesLine, new ChangeOrderItemQuantityRequest(1.5m, second.Revision), CancellationToken.None);

        Assert.Equal(1.5m, await ScalarAsync<decimal>(
            "SELECT quantity FROM orders.order_items WHERE order_item_id = @id;", ("id", halvesLine)));
        Assert.Equal(1m, await ScalarAsync<decimal>(
            "SELECT quantity FROM orders.order_items WHERE order_item_id = @id;", ("id", wholeOnlyLine)));
    }

    [Fact]
    public async Task AddingToAHalfPortionLineAfterTheFlagWasSwitchedOffIsRefusedAndLeavesTheLineAlone()
    {
        var productId = await SeedProductAsync(true);
        var terminalId = Guid.NewGuid();
        var order = await _store!.StartOrderAsync(terminalId, Guid.NewGuid(), CancellationToken.None);
        var added = await _store.AddItemAsync(
            terminalId, order.OrderId, new AddOrderItemRequest(productId, 1.5m, order.Revision), CancellationToken.None);
        await using (var off = _dataSource!.CreateCommand("UPDATE catalog.products SET allows_half_portion = false WHERE product_id = @id;"))
        {
            off.Parameters.AddWithValue("id", productId);
            await off.ExecuteNonQueryAsync();
        }

        await Assert.ThrowsAsync<OrderQuantityNotAllowedException>(() => _store.AddItemAsync(
            terminalId, order.OrderId, new AddOrderItemRequest(productId, 1m, added.Revision), CancellationToken.None));

        Assert.Equal(1.5m, await QuantityAsync(order.OrderId));
    }

    [Fact]
    public async Task AddingAHalfToAWholeLineOfAFlaggedProductMergesIntoOneLine()
    {
        var productId = await SeedProductAsync(true);
        var terminalId = Guid.NewGuid();
        var order = await _store!.StartOrderAsync(terminalId, Guid.NewGuid(), CancellationToken.None);
        var added = await _store.AddItemAsync(
            terminalId, order.OrderId, new AddOrderItemRequest(productId, 1m, order.Revision), CancellationToken.None);

        await _store.AddItemAsync(
            terminalId, order.OrderId, new AddOrderItemRequest(productId, 0.5m, added.Revision), CancellationToken.None);

        Assert.Equal(1L, await ItemCountAsync(order.OrderId));
        Assert.Equal(1.5m, await QuantityAsync(order.OrderId));
    }

    private async Task<Guid> SeedProductAsync(bool allowsHalfPortion)
    {
        var productId = Guid.NewGuid();
        await using var command = _dataSource!.CreateCommand(
            """
            INSERT INTO catalog.products (
                product_id, sku, name, category_id, tax_profile_id, product_type,
                stock_mode, active, display_order, current_price, allows_half_portion)
            VALUES (@product_id, @sku, 'Half portion product', NULL, NULL, 1, 1, true, 0, 10.00, @allows);
            """);
        command.Parameters.AddWithValue("product_id", productId);
        command.Parameters.AddWithValue("sku", $"HALF-{productId:N}");
        command.Parameters.AddWithValue("allows", allowsHalfPortion);
        await command.ExecuteNonQueryAsync();
        return productId;
    }

    private Task<long> ItemCountAsync(Guid orderId)
        => ScalarAsync<long>("SELECT count(*) FROM orders.order_items WHERE order_id = @id;", ("id", orderId));

    private Task<decimal> QuantityAsync(Guid orderId)
        => ScalarAsync<decimal>("SELECT quantity FROM orders.order_items WHERE order_id = @id;", ("id", orderId));

    private Task<Guid> ItemIdAsync(Guid orderId, Guid productId)
        => ScalarAsync<Guid>(
            "SELECT order_item_id FROM orders.order_items WHERE order_id = @order AND product_id = @product;",
            ("order", orderId), ("product", productId));

    private async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _dataSource!.CreateCommand(sql);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return (T)(await command.ExecuteScalarAsync())!;
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
}
