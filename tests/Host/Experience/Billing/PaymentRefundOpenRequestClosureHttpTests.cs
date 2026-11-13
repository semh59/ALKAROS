using System.Net;
using ALKAROS.Billing.PaymentClosure;
using ALKAROS.Payments.PaymentAggregate;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

public sealed partial class PaymentRefundHttpTests
{
    [Fact]
    public async Task AFullyPaidBillStaysOpenWhileARefundRequestWaitsAndClosesOnceTheManagerRejectsIt()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 250m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var closure = app.Services.GetRequiredService<IBillClosureService>();
        var request = await RequestAsync(client, seeded.BillId, paymentId, "Yanlış tutar");

        var held = await closure.TryCloseAsync(seeded.BillId);

        Assert.Equal(BillClosureOutcome.NotSatisfied, held.Outcome);
        Assert.Equal([BillClosureBlocker.HasOpenRefundRequest], held.Blockers);
        Assert.NotEqual("Paid", await Scalar<string>("SELECT status FROM billing.bills WHERE bill_id = @p", seeded.BillId));

        await DecideAsync(client, request.RequestId, "reject", _managerCookie, new RefundDecisionInputV1("Gerekçe yetersiz"));
        var released = await closure.TryCloseAsync(seeded.BillId);

        Assert.Equal(BillClosureOutcome.Closed, released.Outcome);
        Assert.Equal("Paid", await Scalar<string>("SELECT status FROM billing.bills WHERE bill_id = @p", seeded.BillId));
    }

    [Fact]
    public async Task AFullyPaidBillStaysOpenWhileAnApprovedCardRefundWaitsForItsSlip()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CardPaymentAsync(seeded.BillId, 250m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var closure = app.Services.GetRequiredService<IBillClosureService>();
        var payments = app.Services.GetRequiredService<IPaymentRepository>();
        var request = await RequestAsync(client, seeded.BillId, paymentId, "Kart iadesi");
        var approved = await DecideAsync(client, request.RequestId, "approve", _managerCookie);
        Assert.Equal("AwaitingManualRefund", approved.Status);

        var held = await closure.TryCloseAsync(seeded.BillId);

        Assert.Equal(BillClosureOutcome.NotSatisfied, held.Outcome);
        Assert.Equal([BillClosureBlocker.HasOpenRefundRequest], held.Blockers);
        Assert.NotEqual("Paid", await Scalar<string>("SELECT status FROM billing.bills WHERE bill_id = @p", seeded.BillId));
        Assert.True(await payments.HasOpenRefundRequestAsync(seeded.BillId));

        using var complete = await client.SendAsync(
            Post($"refund-requests/{request.RequestId:D}/complete", _cashierCookie, new RefundCompleteInputV1("POS-4471", null)));
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        Assert.False(await payments.HasOpenRefundRequestAsync(seeded.BillId));
    }
}
