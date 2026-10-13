using ALKAROS.Reporting.BusinessDayTotals;
using Xunit;

namespace ALKAROS.Host.Experience.Reporting.Tests;

public sealed class BusinessDayOrderCountRefundTests : IAsyncLifetime
{
    private static readonly DateTimeOffset At = new(2026, 3, 10, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = new(2026, 3, 10);

    private readonly EndOfDayTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task AnOrderWhoseEveryPaymentWasRefundedIsNotCounted()
    {
        await _database.SeedOrderAsync(At, "Completed");
        await _database.SeedOrderAsync(At, "Completed");
        var refundedOrder = await OrderIdsAsync();

        await LinkBillAsync(refundedOrder[0], "Cancelled", "Refunded");

        var totals = await new PostgresBusinessDayTotalsReader(_database.DataSource).ReadAsync(Day);

        Assert.Equal(1, totals.OrderCount);
    }

    [Fact]
    public async Task AnOrderWithAPaidBillOrAnUnrelatedCancelledBillIsStillCounted()
    {
        await _database.SeedOrderAsync(At, "Completed");
        await _database.SeedOrderAsync(At, "Completed");
        var orders = await OrderIdsAsync();
        await LinkBillAsync(orders[0], "Cancelled", "Approved");
        await LinkBillAsync(orders[1], "Cancelled", "Refunded");
        await LinkBillAsync(orders[1], "Paid", "Approved");

        var totals = await new PostgresBusinessDayTotalsReader(_database.DataSource).ReadAsync(Day);

        Assert.Equal(2, totals.OrderCount);
    }

    [Fact]
    public async Task AnOrderWithAnOpenBillAndOneRefundedPaymentIsStillCounted()
    {
        await _database.SeedOrderAsync(At, "Submitted");
        var orders = await OrderIdsAsync();
        await LinkBillAsync(orders[0], "PartiallyPaid", "Refunded");

        var totals = await new PostgresBusinessDayTotalsReader(_database.DataSource).ReadAsync(Day);

        Assert.Equal(1, totals.OrderCount);
    }

    private async Task<List<Guid>> OrderIdsAsync()
    {
        await using var command = _database.DataSource.CreateCommand(
            "SELECT order_id FROM orders.orders ORDER BY order_id;");
        var ids = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            ids.Add(reader.GetGuid(0));
        return ids;
    }

    private async Task LinkBillAsync(Guid orderId, string billStatus, string paymentStatus)
    {
        var billId = Guid.NewGuid();
        await using var command = _database.DataSource.CreateCommand(
            """
            INSERT INTO billing.bills (bill_id, bill_number, order_id, status, payable_amount, opened_at, created_at, updated_at)
            VALUES (@bill, @number, @order, @status, 100, now(), now(), now());
            INSERT INTO payments.payments (payment_id, bill_id, status, requested_amount, tendered_amount, approved_amount,
                                           initiated_at, tendered_at, approved_at, created_at, updated_at)
            VALUES (gen_random_uuid(), @bill, @payment_status, 100, 100, 100, now(), now(), now(), now(), now());
            """);
        command.Parameters.AddWithValue("bill", billId);
        command.Parameters.AddWithValue("number", "RF-" + billId.ToString("N")[..12]);
        command.Parameters.AddWithValue("order", orderId);
        command.Parameters.AddWithValue("status", billStatus);
        command.Parameters.AddWithValue("payment_status", paymentStatus);
        await command.ExecuteNonQueryAsync();
    }
}
