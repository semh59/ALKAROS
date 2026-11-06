using System.Net;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

public sealed partial class PaymentRefundHttpTests
{
    [Fact]
    public async Task AFractionOfAUnitIsRefusedForAProductThatDoesNotAllowHalfPortions()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 250m);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await SendItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(seeded.FirstItemId, 0.5m, false)]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("REFUND_QUANTITY_NOT_WHOLE", await ErrorCode(response));
        Assert.Equal(0L, await Scalar<long>("SELECT count(*) FROM payments.refund_requests WHERE bill_id = @p", seeded.BillId));
    }

    [Fact]
    public async Task AHalfPortionOfAProductThatAllowsItIsPricedByItsShareAndAThirdOfAUnitStaysRefused()
    {
        var seeded = await _database.SeedBillAsync();
        await AllowHalfPortionAsync(seeded.FirstItemId, true);
        var paymentId = await CashPaymentAsync(seeded.BillId, 250m);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var half = await RequestItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(seeded.FirstItemId, 0.5m, false)]);
        using var third = await SendItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(seeded.FirstItemId, 0.3m, false)]);

        Assert.Equal(50m, half.Amount);
        Assert.Equal(HttpStatusCode.BadRequest, third.StatusCode);
        Assert.Equal("REFUND_QUANTITY_NOT_WHOLE", await ErrorCode(third));
    }

    [Fact]
    public async Task ALineSoldInFractionsBeforeTheFlagExistedKeepsItsFractionalRefunds()
    {
        var seeded = await _database.SeedBillAsync();
        await Execute("UPDATE billing.bill_items SET quantity = 1.5 WHERE bill_item_id = @p", seeded.FirstItemId);
        var paymentId = await CashPaymentAsync(seeded.BillId, 250m);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var refund = await RequestItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(seeded.FirstItemId, 0.3m, false)]);

        Assert.Equal(40m, refund.Amount);
    }

    [Fact]
    public async Task TheHalfThatIsLeftOfALineRefundedInHalvesCanStillBeRefundedAfterTheFlagIsSwitchedOff()
    {
        var seeded = await _database.SeedBillAsync();
        await AllowHalfPortionAsync(seeded.SecondItemId, true);
        var firstPayment = await CashPaymentAsync(seeded.BillId, 100m);
        var secondPayment = await CashPaymentAsync(seeded.BillId, 150m);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        await RequestItemsAsync(client, seeded.BillId, firstPayment, [new RefundItemInputV1(seeded.SecondItemId, 0.5m, false)]);
        await AllowHalfPortionAsync(seeded.SecondItemId, false);

        var rest = await RequestItemsAsync(client, seeded.BillId, secondPayment, [new RefundItemInputV1(seeded.SecondItemId, 0.5m, false)]);

        Assert.Equal(25m, rest.Amount);
    }

    [Fact]
    public async Task AFractionalLineOfAProductThatAllowsHalvesStillOnlyTakesHalfSteps()
    {
        var seeded = await _database.SeedBillAsync();
        await Execute("UPDATE billing.bill_items SET quantity = 1.5 WHERE bill_item_id = @p", seeded.FirstItemId);
        await AllowHalfPortionAsync(seeded.FirstItemId, true);
        var paymentId = await CashPaymentAsync(seeded.BillId, 250m);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await SendItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(seeded.FirstItemId, 0.3m, false)]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("REFUND_QUANTITY_NOT_WHOLE", await ErrorCode(response));
    }

    [Fact]
    public async Task ARefundShareThatLandsOnAHalfCentRoundsAwayFromZero()
    {
        var seeded = await _database.SeedBillAsync();
        await Execute("UPDATE billing.bill_items SET quantity = 2, net_amount = 1.01, tax_amount = 0, gross_amount = 1.01 WHERE bill_item_id = @p", seeded.FirstItemId);
        var paymentId = await CashPaymentAsync(seeded.BillId, 51.01m);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var refund = await RequestItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(seeded.FirstItemId, 1m, false)]);

        Assert.Equal(0.51m, refund.Amount);
    }

    private Task AllowHalfPortionAsync(Guid billItemId, bool allow)
        => Execute(
            "UPDATE catalog.products SET allows_half_portion = @f WHERE product_id = (SELECT product_id FROM billing.bill_items WHERE bill_item_id = @p)",
            billItemId, ("f", allow));
}
