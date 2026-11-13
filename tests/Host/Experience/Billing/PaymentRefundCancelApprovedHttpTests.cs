using System.Net;
using ALKAROS.Billing.PaymentClosure;
using ALKAROS.Payments.PaymentAggregate;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

public sealed partial class PaymentRefundHttpTests
{
    [Fact]
    public async Task AManagerCancelsAnApprovedCardRefundWithAReasonAndTheBillClosesAgain()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CardPaymentAsync(seeded.BillId, 250m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var closure = app.Services.GetRequiredService<IBillClosureService>();
        var payments = app.Services.GetRequiredService<IPaymentRepository>();
        var request = await RequestAsync(client, seeded.BillId, paymentId, "Kart iadesi");
        await DecideAsync(client, request.RequestId, "approve", _managerCookie);
        Assert.Equal(BillClosureOutcome.NotSatisfied, (await closure.TryCloseAsync(seeded.BillId)).Outcome);

        var cancelled = await DecideAsync(
            client, request.RequestId, "reject", _managerCookie, new RefundDecisionInputV1("Müşteri iadeden vazgeçti"));

        Assert.Equal("Rejected", cancelled.Status);
        Assert.False(await payments.HasOpenRefundRequestAsync(seeded.BillId));
        Assert.Equal("Approved", await Scalar<string>("SELECT status FROM payments.payments WHERE payment_id = @p", paymentId));
        Assert.Equal("Müşteri iadeden vazgeçti", await Scalar<string>(
            "SELECT decision_note FROM payments.refund_requests WHERE refund_request_id = @p", request.RequestId));
        Assert.Equal(1L, await Scalar<long>(
            """
            SELECT count(*) FROM audit.audit_events
            WHERE aggregate_id = @p AND event_name = 'refund.rejected' AND actor_id = @a AND reason = 'Müşteri iadeden vazgeçti'
              AND (before_state_json::jsonb ->> 'status') = 'AwaitingManualRefund'
              AND (before_state_json::jsonb ->> 'decidedBy') = @d
              AND (after_state_json::jsonb ->> 'allowanceReleased') = 'false'
            """, paymentId, ("a", _managerId), ("d", _managerId.ToString())));
        Assert.Equal(BillClosureOutcome.Closed, (await closure.TryCloseAsync(seeded.BillId)).Outcome);
        Assert.Equal("Paid", await Scalar<string>("SELECT status FROM billing.bills WHERE bill_id = @p", seeded.BillId));
        Assert.Equal("Requested", (await RequestAsync(client, seeded.BillId, paymentId, "Yeniden")).Status);
    }

    [Fact]
    public async Task ACashierCannotCancelAnApprovedRefundAndAnEmptyReasonIsRefused()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CardPaymentAsync(seeded.BillId, 250m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var request = await RequestAsync(client, seeded.BillId, paymentId, "Kart iadesi");
        await DecideAsync(client, request.RequestId, "approve", _managerCookie);

        using var byCashier = await client.SendAsync(
            Post($"refund-requests/{request.RequestId:D}/reject", _cashierCookie, new RefundDecisionInputV1("İptal")));
        using var noReason = await client.SendAsync(
            Post($"refund-requests/{request.RequestId:D}/reject", _managerCookie, new RefundDecisionInputV1(" ")));

        Assert.Equal(HttpStatusCode.Forbidden, byCashier.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal("REFUND_TEXT_INVALID", await ErrorCode(noReason));
        Assert.Equal("AwaitingManualRefund", await Scalar<string>(
            "SELECT status FROM payments.refund_requests WHERE refund_request_id = @p", request.RequestId));
    }

    [Fact]
    public async Task ACompletedOrAlreadyCancelledRefundCannotBeCancelled()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CardPaymentAsync(seeded.BillId, 250m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var request = await RequestAsync(client, seeded.BillId, paymentId, "Kart iadesi");
        await DecideAsync(client, request.RequestId, "approve", _managerCookie);
        await DecideAsync(client, request.RequestId, "reject", _managerCookie, new RefundDecisionInputV1("Vazgeçildi"));

        using var again = await client.SendAsync(
            Post($"refund-requests/{request.RequestId:D}/reject", _managerCookie, new RefundDecisionInputV1("Tekrar")));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var second = await RequestAsync(client, seeded.BillId, paymentId, "Kart iadesi 2");
        await DecideAsync(client, second.RequestId, "approve", _managerCookie);
        using var complete = await client.SendAsync(
            Post($"refund-requests/{second.RequestId:D}/complete", _cashierCookie, new RefundCompleteInputV1("POS-1", null)));
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        using var late = await client.SendAsync(
            Post($"refund-requests/{second.RequestId:D}/reject", _managerCookie, new RefundDecisionInputV1("Geç")));
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        Assert.Equal("REFUND_STATE", await ErrorCode(late));
    }

    [Fact]
    public async Task CancellingAnAllowanceApprovedRefundGivesTheAmountBackOnlyToTheShiftThatSpentIt()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CardPaymentAsync(seeded.BillId, 100m);
        var firstSession = await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        await GrantAsync(client, _cashierId, 100m);
        var request = await RequestAsync(client, seeded.BillId, paymentId, "Kart iadesi");
        Assert.True(request.ApprovedFromAllowance);
        Assert.Equal(100m, (await GetAllowanceAsync(client, _cashierCookie)).Used);

        await DecideAsync(client, request.RequestId, "reject", _managerCookie, new RefundDecisionInputV1("Vazgeçildi"));
        Assert.Equal(new RefundAllowanceV1(true, 100m, 0m, 100m), await GetAllowanceAsync(client, _cashierCookie));
        Assert.Equal(1L, await Scalar<long>(
            """
            SELECT count(*) FROM audit.audit_events
            WHERE aggregate_id = @p AND event_name = 'refund.rejected'
              AND (before_state_json::jsonb ->> 'viaAllowance') = 'true'
              AND (after_state_json::jsonb ->> 'allowanceReleased') = 'true'
            """, paymentId));

        var second = await RequestAsync(client, seeded.BillId, paymentId, "Kart iadesi 2");
        Assert.Equal(100m, (await GetAllowanceAsync(client, _cashierCookie)).Used);
        await Execute("UPDATE cash.cash_sessions SET status = 'Closed', closed_at = now() WHERE cash_session_id = @p", firstSession);
        var nextSession = await OpenSessionAsync(_cashierId);
        await Execute("UPDATE cash.cash_sessions SET refund_allowance = 100, refund_allowance_used = 40 WHERE cash_session_id = @p", nextSession);

        await DecideAsync(client, second.RequestId, "reject", _managerCookie, new RefundDecisionInputV1("Vazgeçildi"));

        Assert.Equal(40m, await Scalar<decimal>(
            "SELECT refund_allowance_used FROM cash.cash_sessions WHERE cash_session_id = @p", nextSession));
        Assert.Equal(1L, await Scalar<long>(
            """
            SELECT count(*) FROM audit.audit_events
            WHERE aggregate_id = @p AND event_name = 'refund.rejected'
              AND (after_state_json::jsonb ->> 'allowanceReleased') = 'false'
            """, paymentId));
    }

    [Fact]
    public async Task CancellingAManagerApprovedRefundLeavesTheShiftAllowanceAlone()
    {
        var seeded = await _database.SeedBillAsync();
        var spentOn = await CardPaymentAsync(seeded.BillId, 100m);
        var heldBack = await CardPaymentAsync(seeded.BillId, 60m);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        await GrantAsync(client, _cashierId, 100m);
        Assert.True((await RequestAsync(client, seeded.BillId, spentOn, "Kart iadesi")).ApprovedFromAllowance);
        var beyond = await RequestAsync(client, seeded.BillId, heldBack, "Kart iadesi 2");
        Assert.False(beyond.ApprovedFromAllowance);
        await DecideAsync(client, beyond.RequestId, "approve", _managerCookie);

        await DecideAsync(client, beyond.RequestId, "reject", _managerCookie, new RefundDecisionInputV1("Vazgeçildi"));

        Assert.Equal(100m, (await GetAllowanceAsync(client, _cashierCookie)).Used);
    }
}
