using ALKAROS.Host.Experience.Orders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

/// <summary>A bill that was closed while the step after it failed: the order stays live and the table stays occupied.</summary>
public sealed class PaidOrderRepairTests : IAsyncLifetime
{
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(5);

    private readonly BillingSplitTestDatabase _database = new();
    private NpgsqlDataSource _source = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _source = NpgsqlDataSource.Create(_database.ConnectionString);
    }

    public async Task DisposeAsync()
    {
        await _source.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task APaidBillWhoseOrderWasNeverCompletedGetsItsOrderCompletedAndItsTableFreed()
    {
        var seeded = await SeedPaidAsync(paidMinutesAgo: 30);
        await using var app = await StartAsync();

        var first = await Sweeper(app).RepairPaidBillOrdersAsync(Grace, CancellationToken.None);
        var second = await Sweeper(app).RepairPaidBillOrdersAsync(Grace, CancellationToken.None);

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Equal("Completed", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", seeded.OrderId));
        Assert.Equal("Available", await Text("SELECT current_status FROM table_mgmt.tables WHERE table_id = @id;", seeded.TableId));
        Assert.Equal(0L, await Count("SELECT count(*) FROM table_mgmt.tables WHERE table_id = @id AND current_order_id IS NOT NULL;", seeded.TableId));
    }

    [Theory]
    [InlineData("just-paid")]
    [InlineData("another-bill-still-open")]
    [InlineData("no-paid-bill")]
    public async Task AnOrderThatIsNotProvablySettledIsLeftAlone(string scenario)
    {
        var seeded = await SeedPaidAsync(paidMinutesAgo: scenario == "just-paid" ? 0 : 30);
        if (scenario == "another-bill-still-open")
            await Exec(
                """
                INSERT INTO billing.bills (bill_id, bill_number, order_id, status, currency_code, opened_at, created_at, updated_at)
                VALUES (@bill, @number, @order, 'Open', 'TRY', now(), now(), now());
                """,
                ("bill", Guid.NewGuid()), ("number", "BIL-OPEN-" + Guid.NewGuid().ToString("N")[..8]), ("order", seeded.OrderId));
        if (scenario == "no-paid-bill")
            await Exec("UPDATE billing.bills SET status = 'Cancelled' WHERE bill_id = @id;", ("id", seeded.BillId));
        await using var app = await StartAsync();

        var repaired = await Sweeper(app).RepairPaidBillOrdersAsync(Grace, CancellationToken.None);

        Assert.Equal(0, repaired);
        Assert.Equal("Submitted", await Text("SELECT status FROM orders.orders WHERE order_id = @id;", seeded.OrderId));
        Assert.Equal(seeded.OrderId, await Guid_("SELECT current_order_id FROM table_mgmt.tables WHERE table_id = @id;", seeded.TableId));
    }

    private sealed record Seeded(Guid OrderId, Guid BillId, Guid TableId);

    private async Task<Seeded> SeedPaidAsync(int paidMinutesAgo)
    {
        var bill = await _database.SeedBillAsync();
        var orderId = await _database.OrderIdOfBillAsync(bill.BillId);
        var tableId = (Guid)(await Scalar("SELECT table_id FROM orders.orders WHERE order_id = @id;", orderId))!;
        await Exec("UPDATE table_mgmt.tables SET current_order_id = @order, current_bill_id = @bill WHERE table_id = @table;",
            ("order", orderId), ("bill", bill.BillId), ("table", tableId));
        await Exec(
            "UPDATE billing.bills SET status = 'Paid', closed_at = now() - make_interval(mins => @minutes) WHERE bill_id = @id;",
            ("minutes", paidMinutesAgo), ("id", bill.BillId));
        return new Seeded(orderId, bill.BillId, tableId);
    }

    private static BillClosureSweeper Sweeper(WebApplication app)
        => app.Services.GetServices<IHostedService>().OfType<BillClosureSweeper>().Single();

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(NpgsqlDataSource.Create(_database.ConnectionString));
        builder.Services.AddOrderManagementExperience();
        builder.Services.AddBillingSplitExperience();
        var app = builder.Build();
        await app.StartAsync();
        return app;
    }

    private async Task<object?> Scalar(string sql, Guid id)
    {
        await using var command = _source.CreateCommand(sql);
        command.Parameters.AddWithValue("id", id);
        return await command.ExecuteScalarAsync();
    }

    private async Task<string> Text(string sql, Guid id) => (string)(await Scalar(sql, id))!;

    private async Task<long> Count(string sql, Guid id) => (long)(await Scalar(sql, id))!;

    private async Task<Guid?> Guid_(string sql, Guid id) => await Scalar(sql, id) as Guid?;

    private async Task Exec(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _source.CreateCommand(sql);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }
}
