using ALKAROS.Reporting.BusinessDayTotals;
using Xunit;

namespace ALKAROS.Host.Experience.Reporting.Tests;

public sealed class BusinessDayRefundNettingTests : IAsyncLifetime
{
    private static readonly DateTimeOffset PaidAt = new(2026, 3, 10, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RefundedAt = new(2026, 3, 11, 10, 0, 0, TimeSpan.Zero);

    private readonly EndOfDayTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task ARefundLowersTheRevenueOfTheDayItWasPaidBackNotTheDayOfThePayment()
    {
        await _database.SeedPaymentAsync(PaidAt, 200m);
        await using (var refund = _database.DataSource.CreateCommand(
            """
            INSERT INTO payments.payment_reversals
                (reversal_id, payment_id, reversal_type, amount, reason, refunded_by, idempotency_key, occurred_at)
            SELECT gen_random_uuid(), payment_id, 'FullRefund', 200, 'test', gen_random_uuid(), 'k-' || payment_id, @at
            FROM payments.payments;
            UPDATE payments.payments SET status = 'Refunded';
            """))
        {
            refund.Parameters.AddWithValue("at", RefundedAt.UtcDateTime);
            await refund.ExecuteNonQueryAsync();
        }
        var reader = new PostgresBusinessDayTotalsReader(_database.DataSource);

        var paidDay = await reader.ReadAsync(new DateOnly(2026, 3, 10));
        var refundDay = await reader.ReadAsync(new DateOnly(2026, 3, 11));

        Assert.Equal(200m, paidDay.Revenue);
        Assert.Equal(-200m, refundDay.Revenue);
    }
}
