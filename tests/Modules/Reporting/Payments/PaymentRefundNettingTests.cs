using ALKAROS.Reporting.Payments.Tests.Fixtures;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Reporting.Payments.Tests;

public sealed class PaymentRefundNettingTests : IAsyncLifetime
{
    private static readonly DateOnly PaidDay = new(2026, 6, 15);
    private static readonly DateOnly RefundDay = new(2026, 6, 16);
    private static readonly DateTimeOffset PaidAt = new(2026, 6, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RefundedAt = new(2026, 6, 16, 10, 0, 0, TimeSpan.Zero);

    private readonly PaymentReportTestDatabase _database = new();
    private PaymentSettlementReportService _service = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _service = new PaymentSettlementReportService(new PostgresPaymentSettlementReportRepository(_database.DataSource));
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task APartialRefundOnTheSameDayLowersTheMethodAmountAndKeepsTheCount()
    {
        var bill = await _database.SeedBillAsync(100m);
        var payment = await _database.SeedPaymentAsync(bill, "Approved", 100m, PaidAt);
        await RefundAsync(payment, 30m, "PartiallyRefunded", PaidAt.AddHours(1));

        var mix = (await _service.GetReportAsync(new PaymentSettlementReportFilter(PaidDay))).PaymentMix;

        mix.Should().ContainSingle(e => e.Method == "Eft" && e.ApprovedAmount == 70m && e.ApprovedCount == 1);
    }

    [Fact]
    public async Task ARefundOnALaterDayLowersThatDayAndLeavesTheOriginalDayWhole()
    {
        var bill = await _database.SeedBillAsync(100m);
        var payment = await _database.SeedPaymentAsync(bill, "Approved", 100m, PaidAt);
        await RefundAsync(payment, 100m, "Refunded", RefundedAt);

        var paidDay = (await _service.GetReportAsync(new PaymentSettlementReportFilter(PaidDay))).PaymentMix;
        var refundDay = (await _service.GetReportAsync(new PaymentSettlementReportFilter(RefundDay))).PaymentMix;

        paidDay.Should().ContainSingle(e => e.Method == "Eft" && e.ApprovedAmount == 100m && e.ApprovedCount == 1);
        refundDay.Should().ContainSingle(e => e.Method == "Eft" && e.ApprovedAmount == -100m && e.ApprovedCount == 0);
    }

    private async Task RefundAsync(Guid paymentId, decimal amount, string status, DateTimeOffset at)
    {
        await using var command = _database.DataSource.CreateCommand(
            """
            INSERT INTO payments.payment_reversals
                (reversal_id, payment_id, reversal_type, amount, reason, refunded_by, idempotency_key, occurred_at)
            VALUES (gen_random_uuid(), @payment_id, 'PartialRefund', @amount, 'test', gen_random_uuid(), @key, @at);
            UPDATE payments.payments SET status = @status WHERE payment_id = @payment_id;
            """);
        command.Parameters.AddWithValue("payment_id", paymentId);
        command.Parameters.AddWithValue("amount", amount);
        command.Parameters.AddWithValue("key", Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("at", at.UtcDateTime);
        command.Parameters.AddWithValue("status", status);
        await command.ExecuteNonQueryAsync();
    }
}
