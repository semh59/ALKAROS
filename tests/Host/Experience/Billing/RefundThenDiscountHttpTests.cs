using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

public sealed partial class PaymentRefundHttpTests
{
    [Theory]
    [InlineData("discount", true)]
    [InlineData("comp", true)]
    [InlineData("discount", false)]
    public async Task ABillDiscountOrCompAfterAnItemRefundIsRefusedSoNoRefundIsOverpaid(string kind, bool approved)
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        await OpenSessionAsync(_cashierId);
        var (_, supervisor) = await _database.SeedSessionWithPermissionsAsync(_terminalId, "supervisor", "bills.discount", "bills.comp");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var refund = await RequestItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(seeded.SecondItemId, 1m, false)]);
        if (approved)
            await DecideAsync(client, refund.RequestId, "approve", _managerCookie);

        using var response = await client.SendAsync(Post(
            $"bills/{seeded.BillId:D}/{kind}", supervisor,
            kind == "discount"
                ? new ApplyBillDiscountRequestV1(Guid.NewGuid().ToString(), "FixedAmount", 10m, "PromotionalOffer")
                : new ApplyBillCompRequestV1(Guid.NewGuid().ToString(), 10m, "CustomerSatisfaction")));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("DISCOUNT_AFTER_REFUND", await ErrorCode(response));
        Assert.Equal(0L, await Scalar<long>("SELECT count(*) FROM billing.bill_adjustments WHERE bill_id = @p", seeded.BillId));
    }

    [Fact]
    public async Task ABillDiscountOnABillWithoutAnItemRefundIsStillApplied()
    {
        var seeded = await _database.SeedBillAsync();
        await CashPaymentAsync(seeded.BillId, 100m);
        var refunded = await _database.SeedBillAsync();
        var refundedPayment = await CashPaymentAsync(refunded.BillId, 100m);
        await OpenSessionAsync(_cashierId);
        var (_, supervisor) = await _database.SeedSessionWithPermissionsAsync(_terminalId, "supervisor", "bills.discount");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        await RequestItemsAsync(client, refunded.BillId, refundedPayment, [new RefundItemInputV1(refunded.SecondItemId, 1m, false)]);

        using var response = await client.SendAsync(Post(
            $"bills/{seeded.BillId:D}/discount", supervisor,
            new ApplyBillDiscountRequestV1(Guid.NewGuid().ToString(), "FixedAmount", 10m, "PromotionalOffer")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ARequestThatNeedsAManagerIsRefusedAtOnceWhenTheBillHasAnItemRefund()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        await OpenSessionAsync(_cashierId);
        var (waiterId, waiter) = await _database.SeedSessionWithPermissionsAsync(_terminalId, "cashier");
        await _database.SetPolicyForRoleOfAsync(waiterId, "bills.comp", 5m, 5);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        await RequestItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(seeded.SecondItemId, 1m, false)]);

        using var response = await client.SendAsync(Post(
            $"bills/{seeded.BillId:D}/comp", waiter, new ApplyBillCompRequestV1(Guid.NewGuid().ToString(), 10m, "CustomerSatisfaction")));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("DISCOUNT_AFTER_REFUND", await ErrorCode(response));
    }

    [Fact]
    public async Task ARejectedItemRefundDoesNotBlockABillDiscount()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        await OpenSessionAsync(_cashierId);
        var (_, supervisor) = await _database.SeedSessionWithPermissionsAsync(_terminalId, "supervisor", "bills.discount");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var refund = await RequestItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(seeded.SecondItemId, 1m, false)]);
        await DecideAsync(client, refund.RequestId, "reject", _managerCookie, new RefundDecisionInputV1("Hayır"));

        using var response = await client.SendAsync(Post(
            $"bills/{seeded.BillId:D}/discount", supervisor,
            new ApplyBillDiscountRequestV1(Guid.NewGuid().ToString(), "FixedAmount", 10m, "PromotionalOffer")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
