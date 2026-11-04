using ALKAROS.Host.Composition;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Tests.DualScreen;

[Collection("Host database password environment")]
public sealed class CustomerDisplayBillBindingTests : IAsyncLifetime
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

    [Fact]
    public async Task OpeningABillShowsItsOrderOnATerminalWithoutAnActiveOrder()
    {
        var orderId = await StartOrderAsync(Guid.NewGuid());
        var billId = await InsertBillAsync(orderId, "Open");
        var terminalId = Guid.NewGuid();

        Assert.True(await _store!.BindBillOrderToDisplayAsync(terminalId, billId, CancellationToken.None));

        var snapshot = await _store.GetSnapshotAsync(Guid.NewGuid(), terminalId, CancellationToken.None);
        Assert.Equal(orderId, snapshot.OrderId);
    }

    [Fact]
    public async Task AnOrderBeingEditedOnTheTerminalIsNeverReplaced()
    {
        var billId = await InsertBillAsync(await StartOrderAsync(Guid.NewGuid()), "Open");
        var terminalId = Guid.NewGuid();
        var editing = await StartOrderAsync(terminalId);

        Assert.False(await _store!.BindBillOrderToDisplayAsync(terminalId, billId, CancellationToken.None));

        var snapshot = await _store.GetSnapshotAsync(Guid.NewGuid(), terminalId, CancellationToken.None);
        Assert.Equal(editing, snapshot.OrderId);
    }

    [Theory]
    [InlineData("Submitted")]
    [InlineData("Preparing")]
    public async Task AnOrderNoLongerBeingEditedOnTheTerminalGivesWayToTheBillBeingPaid(string status)
    {
        var orderId = await StartOrderAsync(Guid.NewGuid());
        var billId = await InsertBillAsync(orderId, "Open");
        var terminalId = Guid.NewGuid();
        var earlier = await StartOrderAsync(terminalId);
        await ExecuteAsync("UPDATE orders.orders SET status = @status WHERE order_id = @id;", ("id", earlier), ("status", status));

        Assert.True(await _store!.BindBillOrderToDisplayAsync(terminalId, billId, CancellationToken.None));

        var snapshot = await _store.GetSnapshotAsync(Guid.NewGuid(), terminalId, CancellationToken.None);
        Assert.Equal(orderId, snapshot.OrderId);
    }

    [Fact]
    public async Task ACancelledBillAPaidBillAndABillWithoutAnOrderAreNotShown()
    {
        var orderId = await StartOrderAsync(Guid.NewGuid());
        var cancelled = await InsertBillAsync(orderId, "Cancelled");
        var paid = await InsertBillAsync(orderId, "Paid");
        var withoutOrder = await InsertBillAsync(null, "Open");
        var terminalId = Guid.NewGuid();

        Assert.False(await _store!.BindBillOrderToDisplayAsync(terminalId, cancelled, CancellationToken.None));
        Assert.False(await _store.BindBillOrderToDisplayAsync(terminalId, paid, CancellationToken.None));
        Assert.False(await _store.BindBillOrderToDisplayAsync(terminalId, withoutOrder, CancellationToken.None));
        Assert.Null((await _store.GetSnapshotAsync(Guid.NewGuid(), terminalId, CancellationToken.None)).OrderId);
    }

    [Fact]
    public async Task OpeningTheSameBillAgainKeepsItShown()
    {
        var orderId = await StartOrderAsync(Guid.NewGuid());
        var billId = await InsertBillAsync(orderId, "Open");
        var terminalId = Guid.NewGuid();

        Assert.True(await _store!.BindBillOrderToDisplayAsync(terminalId, billId, CancellationToken.None));
        Assert.True(await _store.BindBillOrderToDisplayAsync(terminalId, billId, CancellationToken.None));

        Assert.Equal(orderId, (await _store.GetSnapshotAsync(Guid.NewGuid(), terminalId, CancellationToken.None)).OrderId);
    }

    [Fact]
    public async Task AnUnknownBillIsNotFound()
        => await Assert.ThrowsAsync<DualScreenNotFoundException>(() =>
            _store!.BindBillOrderToDisplayAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));

    private async Task<Guid> StartOrderAsync(Guid terminalId)
        => (await _store!.StartOrderAsync(terminalId, Guid.NewGuid(), CancellationToken.None)).OrderId;

    private async Task<Guid> InsertBillAsync(Guid? orderId, string status)
    {
        var billId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO billing.bills (bill_id, bill_number, order_id, status, cancelled_at, opened_at, created_at, updated_at)
            VALUES (@bill_id, @number, @order_id, @status, CASE WHEN @status = 'Cancelled' THEN now() END, now(), now(), now());
            """,
            ("bill_id", billId), ("number", $"B-{billId:N}"[..20]), ("order_id", (object?)orderId ?? DBNull.Value), ("status", status));
        return billId;
    }

    private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _dataSource!.CreateCommand(sql);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync();
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
