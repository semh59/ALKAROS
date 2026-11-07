using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

public sealed partial class PaymentRefundHttpTests
{
    [Fact]
    public async Task RefundingEveryItemOfADiscountedBillAcrossTwoPaymentsGivesBackExactlyWhatWasPaid()
    {
        var seeded = await _database.SeedBillAsync();
        await new ALKAROS.Billing.Adjustments.PostgresBillAdjustmentRepository(_source).AddAsync(
            ALKAROS.Billing.Adjustments.BillAdjustment.CreateDiscountAmount(
                Guid.NewGuid(), seeded.BillId, 10.01m, 0m, "Discount", _managerId, createdBy: _managerId));
        var first = await CashPaymentAsync(seeded.BillId, 95.99m);
        var second = await CashPaymentAsync(seeded.BillId, 144.00m);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var amounts = new List<decimal>();

        foreach (var (itemId, paymentId) in new[] { (seeded.FirstItemId, first), (seeded.FirstItemId, second), (seeded.SecondItemId, second) })
        {
            var request = await RequestItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(itemId, 1m, false)]);
            amounts.Add(request.Amount);
            Assert.Equal("Completed", (await DecideAsync(client, request.RequestId, "approve", _managerCookie)).Status);
        }

        Assert.Equal(239.99m, amounts.Sum());
    }

    [Fact]
    public async Task TheKurusLeftUnpaidByAnItemRefundIsPaidByTheNextOneEvenAfterAWholePaymentRefund()
    {
        var seeded = await _database.SeedBillAsync();
        await new ALKAROS.Billing.Adjustments.PostgresBillAdjustmentRepository(_source).AddAsync(
            ALKAROS.Billing.Adjustments.BillAdjustment.CreateDiscountAmount(
                Guid.NewGuid(), seeded.BillId, 10.01m, 0m, "Discount", _managerId, createdBy: _managerId));
        var first = await CashPaymentAsync(seeded.BillId, 95.99m);
        var second = await CashPaymentAsync(seeded.BillId, 100.00m);
        var third = await CashPaymentAsync(seeded.BillId, 44.00m);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var firstUnit = new[] { new RefundItemInputV1(seeded.FirstItemId, 1m, false) };

        var capped = await RequestItemsAsync(client, seeded.BillId, first, firstUnit);
        await DecideAsync(client, capped.RequestId, "approve", _managerCookie);
        using var whole = await client.SendAsync(
            Post($"bills/{seeded.BillId:D}/refund-requests", _cashierCookie, new RefundRequestInputV1(third, "Tutar")));
        Assert.True(whole.IsSuccessStatusCode);
        var rest = await RequestItemsAsync(client, seeded.BillId, second, firstUnit);

        Assert.Equal(95.99m, capped.Amount);
        Assert.Equal(96.00m, rest.Amount);
    }

    [Fact]
    public async Task AKurusLeftUnpaidTwiceIsPaidByTheLastItemRefundAndDoesNotTurnAnEarlierOneAway()
    {
        var seeded = await _database.SeedBillAsync();
        await new ALKAROS.Billing.Adjustments.PostgresBillAdjustmentRepository(_source).AddAsync(
            ALKAROS.Billing.Adjustments.BillAdjustment.CreateDiscountAmount(
                Guid.NewGuid(), seeded.BillId, 10.01m, 0m, "Discount", _managerId, createdBy: _managerId));
        var first = await CashPaymentAsync(seeded.BillId, 95.99m);
        var second = await CashPaymentAsync(seeded.BillId, 95.98m);
        var third = await CashPaymentAsync(seeded.BillId, 48.02m);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var amounts = new List<decimal>();

        foreach (var (itemId, paymentId) in new[] { (seeded.FirstItemId, first), (seeded.FirstItemId, second), (seeded.SecondItemId, third) })
        {
            var request = await RequestItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(itemId, 1m, false)]);
            amounts.Add(request.Amount);
            await DecideAsync(client, request.RequestId, "approve", _managerCookie);
        }

        Assert.Equal([95.99m, 95.98m, 48.02m], amounts);
    }

    [Fact]
    public async Task ARejectedItemRefundLeavesNoKurusOwedToTheNextOne()
    {
        var seeded = await _database.SeedBillAsync();
        await new ALKAROS.Billing.Adjustments.PostgresBillAdjustmentRepository(_source).AddAsync(
            ALKAROS.Billing.Adjustments.BillAdjustment.CreateDiscountAmount(
                Guid.NewGuid(), seeded.BillId, 10.01m, 0m, "Discount", _managerId, createdBy: _managerId));
        var paymentId = await CashPaymentAsync(seeded.BillId, 95.99m);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var firstUnit = new[] { new RefundItemInputV1(seeded.FirstItemId, 1m, false) };
        var rejected = await RequestItemsAsync(client, seeded.BillId, paymentId, firstUnit);
        await DecideAsync(client, rejected.RequestId, "reject", _managerCookie, new RefundDecisionInputV1("Hayır"));

        var again = await RequestItemsAsync(client, seeded.BillId, paymentId, firstUnit);

        Assert.Equal(95.99m, again.Amount);
    }
}
