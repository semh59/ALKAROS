using System.Net;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

public sealed partial class PaymentRefundHttpTests
{
    [Fact]
    public async Task ADiscountOnABillWhosePaymentWasFullyRefundedIsApplied()
    {
        var seeded = await _database.SeedBillAsync();
        var payable = await _database.BillPayableAsync(seeded.BillId);
        var paymentId = await CashPaymentAsync(seeded.BillId, payable);
        await ReverseAsync(paymentId, payable);
        var (_, supervisor) = await _database.SeedSessionWithPermissionsAsync(_terminalId, "supervisor", "bills.discount");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(Post(
            $"bills/{seeded.BillId:D}/discount", supervisor,
            new ApplyBillDiscountRequestV1(Guid.NewGuid().ToString(), "FixedAmount", 10m, "PromotionalOffer")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ADiscountIsStillRefusedWhenItGoesBelowWhatIsLeftAfterTheRefund()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, await _database.BillPayableAsync(seeded.BillId));
        await ReverseAsync(paymentId, 50m);
        var (_, supervisor) = await _database.SeedSessionWithPermissionsAsync(_terminalId, "supervisor", "bills.discount");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var tooMuch = await client.SendAsync(Post(
            $"bills/{seeded.BillId:D}/discount", supervisor,
            new ApplyBillDiscountRequestV1(Guid.NewGuid().ToString(), "FixedAmount", 60m, "PromotionalOffer")));
        using var fits = await client.SendAsync(Post(
            $"bills/{seeded.BillId:D}/discount", supervisor,
            new ApplyBillDiscountRequestV1(Guid.NewGuid().ToString(), "FixedAmount", 50m, "PromotionalOffer")));

        Assert.Equal(HttpStatusCode.Conflict, tooMuch.StatusCode);
        Assert.Equal("DISCOUNT_BELOW_COLLECTED", await ErrorCode(tooMuch));
        Assert.Equal(HttpStatusCode.OK, fits.StatusCode);
    }

    [Fact]
    public async Task ARequestThatNeedsAManagerIsNotRefusedWhenTheRefundLeavesRoomForIt()
    {
        var seeded = await _database.SeedBillAsync();
        var payable = await _database.BillPayableAsync(seeded.BillId);
        var paymentId = await CashPaymentAsync(seeded.BillId, payable);
        await ReverseAsync(paymentId, payable);
        var (waiterId, waiter) = await _database.SeedSessionWithPermissionsAsync(_terminalId, "cashier");
        await _database.SetPolicyForRoleOfAsync(waiterId, "bills.discount", 5m, 5);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(Post(
            $"bills/{seeded.BillId:D}/discount", waiter,
            new ApplyBillDiscountRequestV1(Guid.NewGuid().ToString(), "FixedAmount", 10m, "PromotionalOffer")));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task ATipCanBeTakenOffAfterThePaymentThatCoveredItWasRefunded()
    {
        var seeded = await _database.SeedBillAsync();
        var tip = Guid.NewGuid();
        await new ALKAROS.Billing.Adjustments.PostgresBillAdjustmentRepository(_source).AddAsync(
            ALKAROS.Billing.Adjustments.BillAdjustment.CreateTip(tip, seeded.BillId, 20m, "Tip", _managerId, createdBy: _managerId));
        var withTip = await _database.BillPayableAsync(seeded.BillId) + 20m;
        var paymentId = await CashPaymentAsync(seeded.BillId, withTip);
        await ReverseAsync(paymentId, withTip);
        var (_, supervisor) = await _database.SeedSessionWithPermissionsAsync(_terminalId, "supervisor", "bills.split");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(Post($"bills/{seeded.BillId:D}/adjustments/{tip:D}/undo", supervisor));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ATipCannotBeTakenOffBelowWhatIsLeftAfterAPartRefundButCanAtTheRemainder()
    {
        var seeded = await _database.SeedBillAsync();
        var tip = Guid.NewGuid();
        await new ALKAROS.Billing.Adjustments.PostgresBillAdjustmentRepository(_source).AddAsync(
            ALKAROS.Billing.Adjustments.BillAdjustment.CreateTip(tip, seeded.BillId, 20m, "Tip", _managerId, createdBy: _managerId));
        var payable = await _database.BillPayableAsync(seeded.BillId);
        var paymentId = await CashPaymentAsync(seeded.BillId, payable + 20m);
        await ReverseAsync(paymentId, 10m);
        var (_, supervisor) = await _database.SeedSessionWithPermissionsAsync(_terminalId, "supervisor", "bills.split");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var refused = await client.SendAsync(Post($"bills/{seeded.BillId:D}/adjustments/{tip:D}/undo", supervisor));
        await ReverseAsync(paymentId, 10m);
        using var applied = await client.SendAsync(Post($"bills/{seeded.BillId:D}/adjustments/{tip:D}/undo", supervisor));

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("ADJUSTMENT_UNDO_BELOW_COLLECTED", await ErrorCode(refused));
        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
    }

    [Fact]
    public async Task ARefundOfAnotherBillDoesNotLowerWhatThisBillHolds()
    {
        var seeded = await _database.SeedBillAsync();
        var other = await _database.SeedBillAsync();
        var otherPayment = await CashPaymentAsync(other.BillId, 50m);
        await ReverseAsync(otherPayment, 50m);
        await CashPaymentAsync(seeded.BillId, await _database.BillPayableAsync(seeded.BillId));
        var (_, supervisor) = await _database.SeedSessionWithPermissionsAsync(_terminalId, "supervisor", "bills.discount");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(Post(
            $"bills/{seeded.BillId:D}/discount", supervisor,
            new ApplyBillDiscountRequestV1(Guid.NewGuid().ToString(), "FixedAmount", 10m, "PromotionalOffer")));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("DISCOUNT_BELOW_COLLECTED", await ErrorCode(response));
    }

    private Task ReverseAsync(Guid paymentId, decimal amount) => Execute(
        """
        INSERT INTO payments.payment_reversals (reversal_id, payment_id, reversal_type, amount, reason, refunded_by, idempotency_key, occurred_at)
        VALUES (gen_random_uuid(), @p, 'PartialRefund', @a, 'İade', @u, gen_random_uuid()::text, now());
        """, paymentId, ("a", amount), ("u", _managerId));
}
