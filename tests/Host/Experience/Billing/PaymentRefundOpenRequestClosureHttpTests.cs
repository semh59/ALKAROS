using ALKAROS.Billing.PaymentClosure;
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
}
