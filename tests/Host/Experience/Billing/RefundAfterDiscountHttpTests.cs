using System.Net;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

public sealed partial class PaymentRefundHttpTests
{
    [Fact]
    public async Task ADiscountGivenAfterAPartRefundMakesTheRestOfTheItemRefundsSumToExactlyWhatWasCollected()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 150m);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var amounts = new List<decimal>();

        var before = await RequestItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(seeded.FirstItemId, 1m, false)]);
        amounts.Add(before.Amount);
        await DecideAsync(client, before.RequestId, "approve", _managerCookie);
        await new ALKAROS.Billing.Adjustments.PostgresBillAdjustmentRepository(_source).AddAsync(
            ALKAROS.Billing.Adjustments.BillAdjustment.CreateDiscountAmount(
                Guid.NewGuid(), seeded.BillId, 100m, 0m, "Discount", _managerId, createdBy: _managerId));
        foreach (var itemId in new[] { seeded.FirstItemId, seeded.SecondItemId })
        {
            var request = await RequestItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(itemId, 1m, false)]);
            amounts.Add(request.Amount);
            await DecideAsync(client, request.RequestId, "approve", _managerCookie);
        }

        Assert.Equal([100m, 20m, 30m], amounts);
        Assert.Equal(150m, amounts.Sum());
    }

    [Fact]
    public async Task AnItemWorthLessThanWhatAnEarlierRefundWasOverpaidIsRefusedWithTheReasonAndGoesTogetherWithTheRest()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 150m);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var before = await RequestItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(seeded.FirstItemId, 1m, false)]);
        await DecideAsync(client, before.RequestId, "approve", _managerCookie);
        await new ALKAROS.Billing.Adjustments.PostgresBillAdjustmentRepository(_source).AddAsync(
            ALKAROS.Billing.Adjustments.BillAdjustment.CreateDiscountAmount(
                Guid.NewGuid(), seeded.BillId, 100m, 0m, "Discount", _managerId, createdBy: _managerId));

        using var alone = await SendItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(seeded.SecondItemId, 1m, false)]);
        var together = await RequestItemsAsync(
            client, seeded.BillId, paymentId,
            [new RefundItemInputV1(seeded.FirstItemId, 1m, false), new RefundItemInputV1(seeded.SecondItemId, 1m, false)]);

        Assert.Equal(HttpStatusCode.BadRequest, alone.StatusCode);
        Assert.Equal("REFUND_EARLIER_OVERPAID", await ErrorCode(alone));
        Assert.Equal(50m, together.Amount);
    }
}
