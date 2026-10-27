using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ALKAROS.Host.Experience.Billing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

[Collection("Billing split PostgreSQL HTTP")]
public sealed class PaymentRefundHttpTests : IAsyncLifetime
{
    private readonly BillingSplitTestDatabase _database = new();
    private NpgsqlDataSource _source = null!;
    private Guid _terminalId;
    private Guid _cashierId;
    private string _cashierCookie = "";
    private Guid _managerId;
    private string _managerCookie = "";

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _source = NpgsqlDataSource.Create(_database.ConnectionString);
        _terminalId = Guid.NewGuid();
        (_cashierId, _cashierCookie) = await _database.SeedSessionWithPermissionsAsync(
            _terminalId, "cashier", "payments.refund.request");
        (_managerId, _managerCookie) = await _database.SeedSessionWithPermissionsAsync(
            _terminalId, "manager", "payments.refund.request", "payments.refund.approve");
    }

    public async Task DisposeAsync()
    {
        await _source.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task ApprovedCashRefundPaysOutOfTheCashiersSessionRefundsThePaymentAndCancelsTheBill()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        var sessionId = await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var request = await RequestAsync(client, seeded.BillId, paymentId, "Yanlış tutar alındı");
        Assert.Equal("Cash", request.Method);
        Assert.Equal("Requested", request.Status);
        Assert.Equal(100m, request.Amount);

        var approved = await DecideAsync(client, request.RequestId, "approve", _managerCookie);
        Assert.Equal("Completed", approved.Status);
        Assert.True(approved.BillCancelled);

        Assert.Equal("Refunded", await Scalar<string>("SELECT status FROM payments.payments WHERE payment_id = @p", paymentId));
        Assert.Equal(100m, await Scalar<decimal>(
            "SELECT COALESCE(SUM(amount), 0) FROM payments.payment_reversals WHERE payment_id = @p AND reversal_type = 'FullRefund'",
            paymentId));
        Assert.Equal(1L, await Scalar<long>(
            """
            SELECT count(*) FROM cash.cash_transactions
            WHERE related_payment_id = @p AND type = 'Refund' AND direction = 'Out' AND amount = 100
              AND cash_session_id = @s
            """, paymentId, ("s", sessionId)));
        Assert.Equal("Cancelled", await Scalar<string>("SELECT status FROM billing.bills WHERE bill_id = @p", seeded.BillId));
        Assert.Equal(1L, await Scalar<long>(
            "SELECT count(*) FROM audit.audit_events WHERE aggregate_id = @p AND event_name = 'payment.refunded' AND actor_id = @a",
            paymentId, ("a", _managerId)));
    }

    [Fact]
    public async Task ARefundIsNotDecidedByTheSamePersonWhoAskedForIt()
    {
        var (selfId, selfCookie) = await _database.SeedSessionWithPermissionsAsync(
            _terminalId, "supervisor", "payments.refund.request", "payments.refund.approve");
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        await OpenSessionAsync(selfId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var request = await RequestAsync(client, seeded.BillId, paymentId, "Deneme", selfCookie);

        using var response = await client.SendAsync(Post($"refund-requests/{request.RequestId:D}/approve", selfCookie));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("SELF_DECISION", await ErrorCode(response));
        Assert.Equal("Requested", await Scalar<string>(
            "SELECT status FROM payments.refund_requests WHERE refund_request_id = @p", request.RequestId));
        Assert.Equal("Approved", await Scalar<string>("SELECT status FROM payments.payments WHERE payment_id = @p", paymentId));
    }

    [Fact]
    public async Task ACashierWithoutTheApprovePermissionCannotDecide()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var request = await RequestAsync(client, seeded.BillId, paymentId, "Deneme");
        var (_, otherCookie) = await _database.SeedSessionWithPermissionsAsync(
            _terminalId, "cashier", "payments.refund.request");

        using var approve = await client.SendAsync(Post($"refund-requests/{request.RequestId:D}/approve", otherCookie));
        using var reject = await client.SendAsync(
            Post($"refund-requests/{request.RequestId:D}/reject", otherCookie, new RefundDecisionInputV1("Hayır")));

        Assert.Equal(HttpStatusCode.Forbidden, approve.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, reject.StatusCode);
    }

    [Fact]
    public async Task CashApprovalWithoutAnOpenSessionIsRefusedAndNothingMoves()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var request = await RequestAsync(client, seeded.BillId, paymentId, "Deneme");

        using var response = await client.SendAsync(Post($"refund-requests/{request.RequestId:D}/approve", _managerCookie));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("NO_OPEN_CASH_SESSION", await ErrorCode(response));
        Assert.Equal("Approved", await Scalar<string>("SELECT status FROM payments.payments WHERE payment_id = @p", paymentId));
        Assert.Equal("Requested", await Scalar<string>(
            "SELECT status FROM payments.refund_requests WHERE refund_request_id = @p", request.RequestId));
    }

    [Fact]
    public async Task ManualRefundWaitsForTheSlipNumberThenRefundsAndCancelsTheBill()
    {
        var seeded = await _database.SeedBillAsync();
        await _database.SeedCollectedAsync(seeded.BillId, 100m, _cashierId);
        var paymentId = await PaymentIdAsync(seeded.BillId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var request = await RequestAsync(client, seeded.BillId, paymentId, "Kart iadesi");
        Assert.Equal("Manual", request.Method);

        var approved = await DecideAsync(client, request.RequestId, "approve", _managerCookie);
        Assert.Equal("AwaitingManualRefund", approved.Status);
        Assert.False(approved.BillCancelled);
        Assert.Equal("Approved", await Scalar<string>("SELECT status FROM payments.payments WHERE payment_id = @p", paymentId));

        using (var noSlip = await client.SendAsync(
            Post($"refund-requests/{request.RequestId:D}/complete", _cashierCookie, new RefundCompleteInputV1(" ", null))))
        {
            Assert.Equal(HttpStatusCode.BadRequest, noSlip.StatusCode);
            Assert.Equal("REFUND_TEXT_INVALID", await ErrorCode(noSlip));
        }

        using var complete = await client.SendAsync(
            Post($"refund-requests/{request.RequestId:D}/complete", _cashierCookie, new RefundCompleteInputV1("POS-4471", null)));
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var done = (await complete.Content.ReadFromJsonAsync<RefundRequestV1>())!;
        Assert.Equal("Completed", done.Status);
        Assert.Equal("POS-4471", done.SlipNumber);
        Assert.True(done.BillCancelled);
        Assert.Equal("Refunded", await Scalar<string>("SELECT status FROM payments.payments WHERE payment_id = @p", paymentId));
        Assert.Equal("Cancelled", await Scalar<string>("SELECT status FROM billing.bills WHERE bill_id = @p", seeded.BillId));
        Assert.Equal(0L, await Scalar<long>(
            "SELECT count(*) FROM cash.cash_transactions WHERE related_payment_id = @p AND type = 'Refund'", paymentId));

        using var again = await client.SendAsync(
            Post($"refund-requests/{request.RequestId:D}/complete", _cashierCookie, new RefundCompleteInputV1("POS-4471", null)));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("REFUND_STATE", await ErrorCode(again));
        Assert.Equal(1L, await Scalar<long>(
            """
            SELECT count(*) FROM audit.audit_events
            WHERE aggregate_id = @p AND event_name = 'payment.refunded' AND actor_id = @a
              AND after_state_json::text LIKE '%POS-4471%'
            """, paymentId, ("a", _cashierId)));
    }

    [Fact]
    public async Task ADecidedRequestCannotBeApprovedAgain()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var request = await RequestAsync(client, seeded.BillId, paymentId, "Deneme");
        await DecideAsync(client, request.RequestId, "approve", _managerCookie);

        using var second = await client.SendAsync(Post($"refund-requests/{request.RequestId:D}/approve", _managerCookie));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("REFUND_STATE", await ErrorCode(second));
        Assert.Equal(1L, await Scalar<long>(
            "SELECT count(*) FROM payments.payment_reversals WHERE payment_id = @p", paymentId));
    }

    [Fact]
    public async Task APaymentThatIsNoLongerApprovedCannotBeRequested()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        await Execute("UPDATE payments.payments SET status = 'Refunded' WHERE payment_id = @p", paymentId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(
            Post($"bills/{seeded.BillId:D}/refund-requests", _cashierCookie, new RefundRequestInputV1(paymentId, "Deneme")));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("PAYMENT_NOT_REFUNDABLE", await ErrorCode(response));
    }

    [Fact]
    public async Task TheShippedRolesSplitAskingFromDeciding()
    {
        await using var read = _source.CreateCommand(
            """
            SELECT p.code, r.code FROM identity.role_permissions rp
            JOIN identity.roles r ON r.role_id = rp.role_id
            JOIN identity.permissions p ON p.permission_id = rp.permission_id
            WHERE p.code IN ('payments.refund.request', 'payments.refund.approve')
              AND r.code IN ('cashier', 'supervisor', 'manager', 'waiter')
            ORDER BY p.code, r.code;
            """);
        var pairs = new List<string>();
        await using var reader = await read.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            pairs.Add(reader.GetString(0) + ":" + reader.GetString(1));
        Assert.Equal(
            [
                "payments.refund.approve:manager", "payments.refund.approve:supervisor",
                "payments.refund.request:cashier", "payments.refund.request:manager", "payments.refund.request:supervisor",
            ],
            pairs);
    }

    [Fact]
    public async Task TheBillStaysOpenUntilEveryPaymentOnItIsRefunded()
    {
        var seeded = await _database.SeedBillAsync();
        var cashPayment = await CashPaymentAsync(seeded.BillId, 100m);
        await _database.SeedCollectedAsync(seeded.BillId, 50m, _cashierId);
        var manualPayment = (await PaymentIdsAsync(seeded.BillId)).Single(id => id != cashPayment);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var first = await RequestAsync(client, seeded.BillId, cashPayment, "Birinci");
        var firstDone = await DecideAsync(client, first.RequestId, "approve", _managerCookie);
        Assert.False(firstDone.BillCancelled);
        Assert.NotEqual("Cancelled", await Scalar<string>("SELECT status FROM billing.bills WHERE bill_id = @p", seeded.BillId));

        var second = await RequestAsync(client, seeded.BillId, manualPayment, "İkinci");
        await DecideAsync(client, second.RequestId, "approve", _managerCookie);
        using var complete = await client.SendAsync(
            Post($"refund-requests/{second.RequestId:D}/complete", _cashierCookie, new RefundCompleteInputV1("FIS-1", null)));
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        Assert.Equal("Cancelled", await Scalar<string>("SELECT status FROM billing.bills WHERE bill_id = @p", seeded.BillId));
    }

    [Fact]
    public async Task OnlyOneOpenRequestPerPaymentAndOnlyThePaymentOfThatBill()
    {
        var seeded = await _database.SeedBillAsync();
        var other = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        await RequestAsync(client, seeded.BillId, paymentId, "Birinci");

        using var duplicate = await client.SendAsync(
            Post($"bills/{seeded.BillId:D}/refund-requests", _cashierCookie, new RefundRequestInputV1(paymentId, "İkinci")));
        using var foreign = await client.SendAsync(
            Post($"bills/{other.BillId:D}/refund-requests", _cashierCookie, new RefundRequestInputV1(paymentId, "Yanlış hesap")));
        using var noReason = await client.SendAsync(
            Post($"bills/{seeded.BillId:D}/refund-requests", _cashierCookie, new RefundRequestInputV1(paymentId, "  ")));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("REFUND_ALREADY_REQUESTED", await ErrorCode(duplicate));
        Assert.Equal(HttpStatusCode.Conflict, foreign.StatusCode);
        Assert.Equal("PAYMENT_NOT_REFUNDABLE", await ErrorCode(foreign));
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
    }

    [Fact]
    public async Task ARefundDecisionWhoseAuditRecordCannotBeWrittenIsNotKept()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var request = await RequestAsync(client, seeded.BillId, paymentId, "Deneme");
        var store = new PaymentRefundStore(_source, new FailingAuditEventStore());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.RejectAsync(request.RequestId, _managerId, "Gerekçe yetersiz", CancellationToken.None));

        Assert.Equal("Requested", await Scalar<string>("SELECT status FROM payments.refund_requests WHERE refund_request_id = @p", request.RequestId));
    }

    [Fact]
    public async Task ARequesterCannotRejectTheirOwnRequestEither()
    {
        var (selfId, selfCookie) = await _database.SeedSessionWithPermissionsAsync(
            _terminalId, "supervisor", "payments.refund.request", "payments.refund.approve");
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        await OpenSessionAsync(selfId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var request = await RequestAsync(client, seeded.BillId, paymentId, "Deneme", selfCookie);

        using var response = await client.SendAsync(
            Post($"refund-requests/{request.RequestId:D}/reject", selfCookie, new RefundDecisionInputV1("Vazgeçtim")));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("SELF_DECISION", await ErrorCode(response));
        Assert.Equal("Requested", await Scalar<string>(
            "SELECT status FROM payments.refund_requests WHERE refund_request_id = @p", request.RequestId));
    }

    [Fact]
    public async Task TheAllowanceMayBeClearedToZeroOrSetToTheMaximumButNotBeyond()
    {
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var maximum = await GrantAsync(client, _cashierId, PaymentRefundStore.MaxAllowance);
        var cleared = await GrantAsync(client, _cashierId, 0m);
        using var beyond = await PutAllowanceAsync(client, _cashierId, PaymentRefundStore.MaxAllowance + 0.01m, _managerCookie);

        Assert.Equal(PaymentRefundStore.MaxAllowance, maximum.Allowance);
        Assert.Equal(0m, cleared.Allowance);
        Assert.Equal(HttpStatusCode.BadRequest, beyond.StatusCode);
    }

    [Fact]
    public async Task APaymentWithNothingLeftToRefundCannotBeRequestedAgain()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        await Execute(
            """
            INSERT INTO payments.payment_reversals (reversal_id, payment_id, reversal_type, amount, reason, refunded_by, idempotency_key, occurred_at)
            VALUES (gen_random_uuid(), @p, 'PartialRefund', 100, 'Önceki iade', @u, 'rmd737-full', now());
            """, paymentId, ("u", _managerId));
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(
            Post($"bills/{seeded.BillId:D}/refund-requests", _cashierCookie, new RefundRequestInputV1(paymentId, "Tekrar")));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("PAYMENT_NOT_REFUNDABLE", await ErrorCode(response));
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Unknown")]
    [InlineData("ReconciliationRequired")]
    [InlineData("PartiallyRefunded")]
    public async Task RefundingOnePaymentDoesNotCancelTheBillWhileAnotherIsStillOpen(string siblingStatus)
    {
        var seeded = await _database.SeedBillAsync();
        var cashPayment = await CashPaymentAsync(seeded.BillId, 100m);
        await _database.SeedPaymentAsync(seeded.BillId, siblingStatus, 50m, _cashierId);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var request = await RequestAsync(client, seeded.BillId, cashPayment, "Birinci");
        var done = await DecideAsync(client, request.RequestId, "approve", _managerCookie);

        Assert.False(done.BillCancelled);
        Assert.NotEqual("Cancelled", await Scalar<string>("SELECT status FROM billing.bills WHERE bill_id = @p", seeded.BillId));
    }

    [Fact]
    public async Task ARejectedRequestLeavesThePaymentAloneAndAllowsANewRequest()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var request = await RequestAsync(client, seeded.BillId, paymentId, "Deneme");

        using var rejected = await client.SendAsync(
            Post($"refund-requests/{request.RequestId:D}/reject", _managerCookie, new RefundDecisionInputV1("Gerekçe yetersiz")));
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Equal("Approved", await Scalar<string>("SELECT status FROM payments.payments WHERE payment_id = @p", paymentId));

        var second = await RequestAsync(client, seeded.BillId, paymentId, "Yeniden");
        Assert.Equal("Requested", second.Status);
    }

    [Fact]
    public async Task APaymentChargedToACustomerAccountIsNotRefundedHere()
    {
        var seeded = await _database.SeedBillAsync();
        await _database.SeedCollectedAsync(seeded.BillId, 100m, _cashierId);
        var paymentId = await PaymentIdAsync(seeded.BillId);
        await Execute(
            """
            INSERT INTO customer_account.account_transactions
                (id, customer_id, transaction_type, amount, source_reference_type, source_reference_id, occurred_at)
            VALUES (gen_random_uuid(), gen_random_uuid(), 'Charge', 100, 'Payment', @p, now());
            """, paymentId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(
            Post($"bills/{seeded.BillId:D}/refund-requests", _cashierCookie, new RefundRequestInputV1(paymentId, "Deneme")));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ACCOUNT_REFUND_UNSUPPORTED", await ErrorCode(response));
    }

    [Fact]
    public async Task ACancelledBillAfterARefundNoLongerCountsTheRefundedPaymentAsMoney()
    {
        var seeded = await _database.SeedBillAsync();
        await _database.SeedCollectedAsync(seeded.BillId, 100m, _cashierId);
        var paymentId = await PaymentIdAsync(seeded.BillId);
        await Execute("UPDATE payments.payments SET status = 'Refunded' WHERE payment_id = @p", paymentId);
        var (_, cancelCookie) = await _database.SeedSessionWithPermissionsAsync(_terminalId, "cashier", "bills.cancel");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var cancel = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/terminals/{_terminalId:D}/billing/bills/{seeded.BillId:D}/cancel");
        cancel.Headers.TryAddWithoutValidation("Cookie", cancelCookie);
        cancel.Content = JsonContent.Create(new CancelBillRequestV1("İade sonrası"));

        using var response = await client.SendAsync(cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ARefundWithinTheShiftAllowanceIsCompletedByTheCashierAndDrawsFromTheAllowance()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        var sessionId = await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        Assert.Equal(new RefundAllowanceV1(true, 150m, 0m, 150m), await GrantAsync(client, _cashierId, 150m));

        var request = await RequestAsync(client, seeded.BillId, paymentId, "Yanlış tutar alındı");

        Assert.Equal("Completed", request.Status);
        Assert.True(request.ApprovedFromAllowance);
        Assert.True(request.BillCancelled);
        Assert.Equal(_cashierId, request.DecidedBy);
        Assert.Equal("Refunded", await Scalar<string>("SELECT status FROM payments.payments WHERE payment_id = @p", paymentId));
        Assert.Equal(1L, await Scalar<long>(
            """
            SELECT count(*) FROM cash.cash_transactions
            WHERE related_payment_id = @p AND type = 'Refund' AND direction = 'Out' AND amount = 100 AND cash_session_id = @s
            """, paymentId, ("s", sessionId)));
        Assert.Equal("Allowance", await Scalar<string>(
            "SELECT approval_mode FROM payments.refund_requests WHERE refund_request_id = @p", request.RequestId));
        Assert.Equal(1L, await Scalar<long>(
            """
            SELECT count(*) FROM audit.audit_events
            WHERE aggregate_id = @p AND event_name = 'payment.refunded' AND actor_id = @a
              AND (after_state_json::jsonb ->> 'viaAllowance') = 'true'
            """, paymentId, ("a", _cashierId)));
        var allowance = await GetAllowanceAsync(client, _cashierCookie);
        Assert.Equal(new RefundAllowanceV1(true, 150m, 100m, 50m), allowance);
    }

    [Fact]
    public async Task ARefundBeyondTheRemainingAllowanceGoesToTheManagerAndSpendsNothing()
    {
        var first = await _database.SeedBillAsync();
        var firstPayment = await CashPaymentAsync(first.BillId, 100m);
        var second = await _database.SeedBillAsync();
        var secondPayment = await CashPaymentAsync(second.BillId, 100m);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        await GrantAsync(client, _cashierId, 150m);

        var inside = await RequestAsync(client, first.BillId, firstPayment, "Birinci");
        var beyond = await RequestAsync(client, second.BillId, secondPayment, "İkinci");

        Assert.Equal("Completed", inside.Status);
        Assert.Equal("Requested", beyond.Status);
        Assert.False(beyond.ApprovedFromAllowance);
        Assert.Equal(100m, (await GetAllowanceAsync(client, _cashierCookie)).Used);

        var approved = await DecideAsync(client, beyond.RequestId, "approve", _managerCookie);
        Assert.Equal("Completed", approved.Status);
        Assert.Equal(100m, (await GetAllowanceAsync(client, _cashierCookie)).Used);
        Assert.Equal("Manager", await Scalar<string>(
            "SELECT approval_mode FROM payments.refund_requests WHERE refund_request_id = @p", beyond.RequestId));
    }

    [Fact]
    public async Task ARetriedRequestWithTheSameKeyReturnsTheStoredRefundAndPaysOutOnce()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        var other = await CashPaymentAsync(seeded.BillId, 40m);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        await GrantAsync(client, _cashierId, 500m);
        var path = $"bills/{seeded.BillId:D}/refund-requests";

        using var first = await client.SendAsync(Post(path, _cashierCookie, new RefundRequestInputV1(paymentId, "Yanlış tutar"), "retry-1"));
        using var again = await client.SendAsync(Post(path, _cashierCookie, new RefundRequestInputV1(paymentId, "Yanlış tutar"), "retry-1"));
        using var reused = await client.SendAsync(Post(path, _cashierCookie, new RefundRequestInputV1(other, "Başka ödeme"), "retry-1"));

        var created = (await first.Content.ReadFromJsonAsync<RefundRequestV1>())!;
        var replayed = (await again.Content.ReadFromJsonAsync<RefundRequestV1>())!;
        Assert.Equal("Completed", created.Status);
        Assert.Equal(created.RequestId, replayed.RequestId);
        Assert.Equal(1L, await Scalar<long>(
            "SELECT count(*) FROM cash.cash_transactions WHERE related_payment_id = @p AND type = 'Refund'", paymentId));
        Assert.Equal(1L, await Scalar<long>(
            "SELECT count(*) FROM payments.refund_requests WHERE bill_id = @p", seeded.BillId));
        Assert.Equal(100m, (await GetAllowanceAsync(client, _cashierCookie)).Used);
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ErrorCode(reused));
    }

    [Fact]
    public async Task ARequestWithoutAnIdempotencyKeyIsRefused()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(
            Post($"bills/{seeded.BillId:D}/refund-requests", _cashierCookie, new RefundRequestInputV1(paymentId, "Deneme"), " "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0L, await Scalar<long>("SELECT count(*) FROM payments.refund_requests WHERE bill_id = @p", seeded.BillId));
    }

    [Fact]
    public async Task TheRequestAndTheApprovalAreAudited()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var request = await RequestAsync(client, seeded.BillId, paymentId, "Deneme");
        await DecideAsync(client, request.RequestId, "approve", _managerCookie);

        Assert.Equal(1L, await Scalar<long>(
            "SELECT count(*) FROM audit.audit_events WHERE aggregate_id = @p AND event_name = 'refund.requested' AND actor_id = @a",
            paymentId, ("a", _cashierId)));
        Assert.Equal(1L, await Scalar<long>(
            "SELECT count(*) FROM audit.audit_events WHERE aggregate_id = @p AND event_name = 'refund.approved'",
            paymentId));
    }

    [Fact]
    public async Task TheListFiltersByBillAndPagesBackwards()
    {
        var first = await _database.SeedBillAsync();
        var second = await _database.SeedBillAsync();
        var firstPayment = await CashPaymentAsync(first.BillId, 100m);
        var secondPayment = await CashPaymentAsync(second.BillId, 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var older = await RequestAsync(client, first.BillId, firstPayment, "Birinci");
        var newer = await RequestAsync(client, second.BillId, secondPayment, "İkinci");

        var forBill = await ListAsync(client, $"?billId={first.BillId:D}");
        var page = await ListAsync(client, "?limit=1");
        var before = await ListAsync(client, $"?limit=1&before={Uri.EscapeDataString(page.Single().RequestedAt.ToString("O"))}");

        Assert.Equal(older.RequestId, forBill.Single().RequestId);
        Assert.Equal(newer.RequestId, page.Single().RequestId);
        Assert.Equal(older.RequestId, before.Single().RequestId);
    }

    [Fact]
    public async Task WithoutAnAllowanceTheRequestWaitsForTheManagerAsBefore()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var request = await RequestAsync(client, seeded.BillId, paymentId, "Deneme");

        Assert.Equal("Requested", request.Status);
        Assert.Equal(new RefundAllowanceV1(true, 0m, 0m, 0m), await GetAllowanceAsync(client, _cashierCookie));
    }

    [Fact]
    public async Task ACardRefundWithinTheAllowanceReservesItAndTheCashierRecordsTheSlip()
    {
        var seeded = await _database.SeedBillAsync();
        await _database.SeedCollectedAsync(seeded.BillId, 100m, _cashierId);
        var paymentId = await PaymentIdAsync(seeded.BillId);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        await GrantAsync(client, _cashierId, 100m);

        var request = await RequestAsync(client, seeded.BillId, paymentId, "Kart iadesi");

        Assert.Equal("Manual", request.Method);
        Assert.Equal("AwaitingManualRefund", request.Status);
        Assert.True(request.ApprovedFromAllowance);
        Assert.Equal(0m, (await GetAllowanceAsync(client, _cashierCookie)).Remaining);

        using var complete = await client.SendAsync(
            Post($"refund-requests/{request.RequestId:D}/complete", _cashierCookie, new RefundCompleteInputV1("POS-9001", null)));
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        Assert.Equal("Refunded", await Scalar<string>("SELECT status FROM payments.payments WHERE payment_id = @p", paymentId));
        Assert.Equal(1L, await Scalar<long>(
            """
            SELECT count(*) FROM audit.audit_events
            WHERE aggregate_id = @p AND event_name = 'payment.refunded' AND actor_id = @a
              AND (after_state_json::jsonb ->> 'viaAllowance') = 'true'
            """, paymentId, ("a", _cashierId)));
    }

    [Fact]
    public async Task OnlyAnOpenSessionOfTheRequesterSpendsTheAllowance()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        var closedSession = await OpenSessionAsync(_cashierId, closed: true);
        await Execute(
            "UPDATE cash.cash_sessions SET refund_allowance = 500 WHERE cash_session_id = @p", closedSession);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var request = await RequestAsync(client, seeded.BillId, paymentId, "Deneme");

        Assert.Equal("Requested", request.Status);
        Assert.Equal(0m, await Scalar<decimal>(
            "SELECT refund_allowance_used FROM cash.cash_sessions WHERE cash_session_id = @p", closedSession));
    }

    [Fact]
    public async Task OnlyAManagerGrantsTheAllowanceNotToThemselvesNotBelowWhatIsUsedAndNotWithoutASession()
    {
        var (selfId, selfCookie) = await _database.SeedSessionWithPermissionsAsync(
            _terminalId, "supervisor", "payments.refund.request", "payments.refund.approve");
        await OpenSessionAsync(_cashierId);
        await OpenSessionAsync(selfId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var byCashier = await PutAllowanceAsync(client, selfId, 50m, _cashierCookie);
        using var toSelf = await PutAllowanceAsync(client, selfId, 50m, selfCookie);
        using var negative = await PutAllowanceAsync(client, _cashierId, -1m, _managerCookie);
        using var noSession = await PutAllowanceAsync(client, Guid.NewGuid(), 50m, _managerCookie);

        Assert.Equal(HttpStatusCode.Forbidden, byCashier.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, toSelf.StatusCode);
        Assert.Equal("SELF_DECISION", await ErrorCode(toSelf));
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
        Assert.Equal("ALLOWANCE_INVALID", await ErrorCode(negative));
        Assert.Equal(HttpStatusCode.Conflict, noSession.StatusCode);
        Assert.Equal("NO_OPEN_CASH_SESSION", await ErrorCode(noSession));

        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        await GrantAsync(client, _cashierId, 100m);
        await RequestAsync(client, seeded.BillId, paymentId, "Harcama");
        using var below = await PutAllowanceAsync(client, _cashierId, 40m, _managerCookie);
        Assert.Equal(HttpStatusCode.BadRequest, below.StatusCode);
        Assert.Equal(100m, (await GetAllowanceAsync(client, _cashierCookie)).Allowance);
        using var tooMuch = await PutAllowanceAsync(
            client, _cashierId, PaymentRefundStore.MaxAllowance + 1m, _managerCookie);
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);

        Assert.Equal(new RefundAllowanceV1(true, 100m, 100m, 0m), await GrantAsync(client, _cashierId, 100m));
        Assert.Equal(2L, await Scalar<long>(
            "SELECT count(*) FROM audit.audit_events WHERE event_name = 'refund.allowance.set' AND actor_id = @p", _managerId));
    }

    [Fact]
    public async Task TheAllowanceReadsEmptyWithoutAnOpenSession()
    {
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        Assert.Equal(new RefundAllowanceV1(false, 0m, 0m, 0m), await GetAllowanceAsync(client, _cashierCookie));
    }

    private async Task<RefundAllowanceV1> GrantAsync(HttpClient client, Guid cashierUserId, decimal amount)
    {
        using var response = await PutAllowanceAsync(client, cashierUserId, amount, _managerCookie);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<RefundAllowanceV1>())!;
    }

    private Task<HttpResponseMessage> PutAllowanceAsync(HttpClient client, Guid cashierUserId, decimal amount, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/terminals/{_terminalId:D}/billing/refund-allowance")
        {
            Content = JsonContent.Create(new RefundAllowanceInputV1(cashierUserId, amount)),
        };
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return client.SendAsync(request);
    }

    private async Task<RefundAllowanceV1> GetAllowanceAsync(HttpClient client, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/terminals/{_terminalId:D}/billing/refund-allowance");
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        using var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<RefundAllowanceV1>())!;
    }

    [Fact]
    public async Task AnItemRefundPaysBackOnlyTheChosenUnitsAndTheBillStaysOpen()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 250m);
        var sessionId = await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var request = await RequestItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(seeded.FirstItemId, 1m, false)]);

        Assert.Equal("Requested", request.Status);
        Assert.Equal(100m, request.Amount);
        Assert.Equal(new RefundItemV1(seeded.FirstItemId, 1m, false), Assert.Single(request.Items!));
        var approved = await DecideAsync(client, request.RequestId, "approve", _managerCookie);
        Assert.Equal("Completed", approved.Status);
        Assert.False(approved.BillCancelled);
        Assert.Equal(1m, Assert.Single(approved.Items!).Quantity);
        Assert.Equal("PartiallyRefunded", await Scalar<string>("SELECT status FROM payments.payments WHERE payment_id = @p", paymentId));
        Assert.Equal(100m, await Scalar<decimal>(
            "SELECT COALESCE(SUM(amount), 0) FROM payments.payment_reversals WHERE payment_id = @p AND reversal_type = 'PartialRefund'",
            paymentId));
        Assert.Equal(1L, await Scalar<long>(
            """
            SELECT count(*) FROM cash.cash_transactions
            WHERE related_payment_id = @p AND type = 'Refund' AND direction = 'Out' AND amount = 100 AND cash_session_id = @s
            """, paymentId, ("s", sessionId)));
        Assert.NotEqual("Cancelled", await Scalar<string>("SELECT status FROM billing.bills WHERE bill_id = @p", seeded.BillId));
        var listed = await ListAsync(client);
        Assert.Equal(1m, Assert.Single(listed.Single(row => row.RequestId == request.RequestId).Items!).Quantity);
    }

    [Fact]
    public async Task TheItemRefundIsScaledByTheBillDiscountAndLeavesTipsAndFeesAlone()
    {
        var seeded = await _database.SeedBillAsync();
        await new ALKAROS.Billing.Adjustments.PostgresBillAdjustmentRepository(_source).AddAsync(
            ALKAROS.Billing.Adjustments.BillAdjustment.CreateDiscountAmount(
                Guid.NewGuid(), seeded.BillId, 25m, 0m, "Discount", _managerId, createdBy: _managerId));
        var paymentId = await CashPaymentAsync(seeded.BillId, 225m);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var request = await RequestItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(seeded.FirstItemId, 1m, false)]);

        Assert.Equal(90m, request.Amount);
    }

    [Fact]
    public async Task ARejectedItemRequestFreesTheUnitsAndTheRemainingUnitsAreCapped()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 250m);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var first = await RequestItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(seeded.FirstItemId, 2m, false)]);
        await DecideAsync(client, first.RequestId, "reject", _managerCookie, new RefundDecisionInputV1("Hayır"));

        var again = await RequestItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(seeded.FirstItemId, 1m, false)]);
        await DecideAsync(client, again.RequestId, "approve", _managerCookie);
        using var tooMany = await SendItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(seeded.FirstItemId, 2m, false)]);

        Assert.Equal(HttpStatusCode.Conflict, tooMany.StatusCode);
        Assert.Equal("REFUND_ITEM_QUANTITY", await ErrorCode(tooMany));
        var rest = await RequestItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(seeded.FirstItemId, 1m, false)]);
        Assert.Equal(100m, rest.Amount);
    }

    [Fact]
    public async Task RefusesAnItemRefundThatIsInvalidOrMoreThanThePaymentHolds()
    {
        var seeded = await _database.SeedBillAsync();
        var small = await CashPaymentAsync(seeded.BillId, 100m);
        await _database.SeedCollectedAsync(seeded.BillId, 150m, _cashierId);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var tooMuch = await SendItemsAsync(client, seeded.BillId, small, [
            new RefundItemInputV1(seeded.FirstItemId, 2m, false), new RefundItemInputV1(seeded.SecondItemId, 1m, false)]);
        using var twice = await SendItemsAsync(client, seeded.BillId, small, [
            new RefundItemInputV1(seeded.FirstItemId, 1m, false), new RefundItemInputV1(seeded.FirstItemId, 1m, false)]);
        using var zero = await SendItemsAsync(client, seeded.BillId, small, [new RefundItemInputV1(seeded.FirstItemId, 0m, false)]);
        using var negative = await SendItemsAsync(client, seeded.BillId, small, [
            new RefundItemInputV1(seeded.FirstItemId, 2m, false), new RefundItemInputV1(seeded.SecondItemId, -1m, false)]);
        using var fraction = await SendItemsAsync(client, seeded.BillId, small, [
            new RefundItemInputV1(seeded.FirstItemId, 1m, false), new RefundItemInputV1(seeded.SecondItemId, 0.0004m, false)]);
        using var foreign = await SendItemsAsync(client, seeded.BillId, small, [new RefundItemInputV1(Guid.NewGuid(), 1m, false)]);
        using var noStockService = await SendItemsAsync(client, seeded.BillId, small, [new RefundItemInputV1(seeded.SecondItemId, 1m, true)]);

        Assert.Equal(HttpStatusCode.Conflict, tooMuch.StatusCode);
        Assert.Equal("REFUND_AMOUNT_EXCEEDS_PAYMENT", await ErrorCode(tooMuch));
        foreach (var (response, code) in new[]
        {
            (twice, "REFUND_ITEMS_DUPLICATE"), (zero, "REFUND_QUANTITY_INVALID"), (negative, "REFUND_QUANTITY_INVALID"),
            (fraction, "REFUND_QUANTITY_INVALID"), (foreign, "REFUND_ITEM_NOT_ON_BILL"), (noStockService, "REFUND_STOCK_UNAVAILABLE"),
        })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(code, await ErrorCode(response));
        }

        Assert.Equal(0L, await Scalar<long>(
            "SELECT count(*) FROM payments.refund_requests WHERE bill_id = @p", seeded.BillId));
    }

    [Fact]
    public async Task OpenRequestsReserveUnitsAcrossPaymentsAndTheRestCanBeRefundedWhole()
    {
        var seeded = await _database.SeedBillAsync();
        var first = await CashPaymentAsync(seeded.BillId, 100m);
        var second = await CashPaymentAsync(seeded.BillId, 150m);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        await RequestItemsAsync(client, seeded.BillId, first, [new RefundItemInputV1(seeded.FirstItemId, 1m, false)]);

        using var reserved = await SendItemsAsync(client, seeded.BillId, second, [new RefundItemInputV1(seeded.FirstItemId, 2m, false)]);

        Assert.Equal(HttpStatusCode.Conflict, reserved.StatusCode);
        Assert.Equal("REFUND_ITEM_QUANTITY", await ErrorCode(reserved));

        var partial = await RequestItemsAsync(client, seeded.BillId, second, [new RefundItemInputV1(seeded.SecondItemId, 1m, false)]);
        await DecideAsync(client, partial.RequestId, "approve", _managerCookie);
        var rest = await RequestAsync(client, seeded.BillId, second, "Kalanı iade");
        Assert.Equal(100m, rest.Amount);
        Assert.Empty(rest.Items ?? []);
    }

    [Fact]
    public async Task AnItemRefundWithinTheAllowanceIsCompletedByTheCashierAndDrawsFromIt()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 250m);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        await GrantAsync(client, _cashierId, 120m);

        var inside = await RequestItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(seeded.FirstItemId, 1m, false)]);

        Assert.Equal("Completed", inside.Status);
        Assert.True(inside.ApprovedFromAllowance);
        Assert.Equal(20m, (await GetAllowanceAsync(client, _cashierCookie)).Remaining);
        Assert.Equal("PartiallyRefunded", await Scalar<string>("SELECT status FROM payments.payments WHERE payment_id = @p", paymentId));
    }

    [Fact]
    public async Task TheChosenUnitsGoBackToStockInTheCompletingTransactionAndTheOthersDoNot()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 250m);
        await OpenSessionAsync(_cashierId);
        var stockItemId = await SeedConsumptionAsync(seeded.FirstItemId, 20m);
        await using var app = await StartAsync(withStock: true);
        using var client = CreateClient(app);

        var kept = await RequestItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(seeded.FirstItemId, 1m, false)]);
        await DecideAsync(client, kept.RequestId, "approve", _managerCookie);
        Assert.Equal(0L, await Scalar<long>("SELECT count(*) FROM inventory.stock_movements WHERE movement_type = 'Return' AND stock_item_id = @p", stockItemId));

        var restored = await RequestItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(seeded.FirstItemId, 1m, true)]);
        var approved = await DecideAsync(client, restored.RequestId, "approve", _managerCookie);

        Assert.Equal("Completed", approved.Status);
        Assert.Equal(10m, await Scalar<decimal>(
            "SELECT COALESCE(SUM(quantity), 0) FROM inventory.stock_movements WHERE movement_type = 'Return' AND stock_item_id = @p",
            stockItemId));
        Assert.Equal(90m, await Scalar<decimal>(
            "SELECT on_hand_quantity FROM inventory.stock_balances WHERE stock_item_id = @p", stockItemId));

        var last = await RequestItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(seeded.SecondItemId, 1m, false)]);
        await DecideAsync(client, last.RequestId, "approve", _managerCookie);
        Assert.Equal(10m, await Scalar<decimal>(
            "SELECT COALESCE(SUM(quantity), 0) FROM inventory.stock_movements WHERE movement_type = 'Return' AND stock_item_id = @p",
            stockItemId));
    }

    [Fact]
    public async Task SuccessiveRestoringRefundsOfOneItemReturnEachShareOnce()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 250m);
        await OpenSessionAsync(_cashierId);
        var stockItemId = await SeedConsumptionAsync(seeded.FirstItemId, 20m);
        await using var app = await StartAsync(withStock: true);
        using var client = CreateClient(app);

        for (var round = 0; round < 2; round++)
        {
            var request = await RequestItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(seeded.FirstItemId, 1m, true)]);
            await DecideAsync(client, request.RequestId, "approve", _managerCookie);
        }

        Assert.Equal(20m, await Scalar<decimal>(
            "SELECT COALESCE(SUM(quantity), 0) FROM inventory.stock_movements WHERE movement_type = 'Return' AND stock_item_id = @p",
            stockItemId));
        Assert.Equal(100m, await Scalar<decimal>(
            "SELECT on_hand_quantity FROM inventory.stock_balances WHERE stock_item_id = @p", stockItemId));
    }

    [Fact]
    public async Task ACardItemRefundPutsStockBackWhenTheSlipIsRecorded()
    {
        var seeded = await _database.SeedBillAsync();
        await _database.SeedCollectedAsync(seeded.BillId, 250m, _cashierId);
        var paymentId = await PaymentIdAsync(seeded.BillId);
        var stockItemId = await SeedConsumptionAsync(seeded.FirstItemId, 20m);
        await using var app = await StartAsync(withStock: true);
        using var client = CreateClient(app);
        var request = await RequestItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(seeded.FirstItemId, 2m, true)]);
        await DecideAsync(client, request.RequestId, "approve", _managerCookie);
        Assert.Equal(0L, await Scalar<long>(
            "SELECT count(*) FROM inventory.stock_movements WHERE movement_type = 'Return' AND stock_item_id = @p", stockItemId));

        using var complete = await client.SendAsync(
            Post($"refund-requests/{request.RequestId:D}/complete", _cashierCookie, new RefundCompleteInputV1("POS-7001", null)));

        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        Assert.Equal(20m, await Scalar<decimal>(
            "SELECT COALESCE(SUM(quantity), 0) FROM inventory.stock_movements WHERE movement_type = 'Return' AND stock_item_id = @p",
            stockItemId));
    }

    private async Task<Guid> SeedConsumptionAsync(Guid billItemId, decimal consumed)
    {
        var orderItemId = await Scalar<Guid>("SELECT order_item_id FROM billing.bill_items WHERE bill_item_id = @p", billItemId);
        var converter = new ALKAROS.Measurements.UnitConverter();
        var locations = new ALKAROS.Inventory.StockMaster.PostgresStockLocationRepository(_source);
        var items = new ALKAROS.Inventory.StockMaster.PostgresStockItemRepository(_source);
        var movements = new ALKAROS.Inventory.MovementLedger.PostgresStockMovementRepository(_source);
        var balances = new ALKAROS.Inventory.BalanceProjection.PostgresStockBalanceRepository(_source);
        var projector = new ALKAROS.Inventory.BalanceProjection.StockBalanceProjector(balances, movements, locations);
        var master = new ALKAROS.Inventory.StockMaster.StockMasterService(
            locations, items, new ALKAROS.Inventory.StockMaster.PostgresProductStockMappingRepository(_source), converter);
        var recorder = new ALKAROS.Inventory.MovementLedger.StockMovementService(movements, items, locations, converter);
        var location = await master.CreateLocationAsync(
            "LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), "Kitchen",
            ALKAROS.Inventory.StockMaster.StockLocationType.Kitchen);
        var item = await master.CreateStockItemAsync(
            "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), "Beef",
            ALKAROS.Inventory.StockMaster.StockItemType.RawMaterial, "kg");
        var receipt = await recorder.RecordMovementAsync(
            item.Id, location.Id, ALKAROS.Inventory.MovementLedger.StockMovementType.PurchaseReceipt, 100m, "kg",
            ALKAROS.Inventory.MovementLedger.StockMovementSourceType.PurchaseOrder);
        await projector.ApplyMovementAsync(receipt);
        var consumption = await recorder.RecordMovementAsync(
            item.Id, location.Id, ALKAROS.Inventory.MovementLedger.StockMovementType.Consumption, consumed, "kg",
            ALKAROS.Inventory.MovementLedger.StockMovementSourceType.Order, sourceReferenceId: orderItemId, reason: "Order accepted");
        await projector.ApplyMovementAsync(consumption);
        return item.Id;
    }

    private Task<HttpResponseMessage> SendItemsAsync(
        HttpClient client, Guid billId, Guid paymentId, IReadOnlyList<RefundItemInputV1> items)
        => client.SendAsync(Post(
            $"bills/{billId:D}/refund-requests", _cashierCookie, new RefundRequestInputV1(paymentId, "İkram", items)));

    private async Task<RefundRequestV1> RequestItemsAsync(
        HttpClient client, Guid billId, Guid paymentId, IReadOnlyList<RefundItemInputV1> items)
    {
        using var response = await SendItemsAsync(client, billId, paymentId, items);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<RefundRequestV1>())!;
    }

    private async Task<IReadOnlyList<RefundRequestV1>> ListAsync(HttpClient client, string query = "")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/terminals/{_terminalId:D}/billing/refund-requests{query}");
        request.Headers.TryAddWithoutValidation("Cookie", _cashierCookie);
        using var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<List<RefundRequestV1>>())!;
    }

    private async Task<Guid> CashPaymentAsync(Guid billId, decimal amount)
    {
        await _database.SeedCollectedAsync(billId, amount, _cashierId);
        var paymentId = (await PaymentIdsAsync(billId)).Last();
        var sessionId = await OpenSessionAsync(_cashierId, closed: true);
        await Execute(
            """
            INSERT INTO cash.cash_transactions
                (cash_transaction_id, cash_session_id, type, direction, amount, related_payment_id, occurred_at)
            VALUES (gen_random_uuid(), @s, 'Sale', 'In', @a, @p, now());
            """, paymentId, ("s", sessionId), ("a", amount));
        return paymentId;
    }

    private async Task<Guid> OpenSessionAsync(Guid cashierUserId, bool closed = false)
    {
        var sessionId = Guid.NewGuid();
        await using var command = _source.CreateCommand(
            """
            INSERT INTO cash.cash_sessions
                (cash_session_id, cashier_user_id, terminal_id, status, opening_balance, expected_cash, actual_cash,
                 difference, opened_at, closed_at, created_at, updated_at)
            VALUES (@s, @u, gen_random_uuid(), @status, 0, 0, 0, 0, now(), @closed_at, now(), now());
            """);
        command.Parameters.AddWithValue("s", sessionId);
        command.Parameters.AddWithValue("u", cashierUserId);
        command.Parameters.AddWithValue("status", closed ? "Closed" : "Open");
        command.Parameters.AddWithValue("closed_at", closed ? DateTime.UtcNow : DBNull.Value);
        await command.ExecuteNonQueryAsync();
        return sessionId;
    }

    private async Task<Guid> PaymentIdAsync(Guid billId) => (await PaymentIdsAsync(billId)).Single();

    private async Task<List<Guid>> PaymentIdsAsync(Guid billId)
    {
        await using var command = _source.CreateCommand(
            "SELECT payment_id FROM payments.payments WHERE bill_id = @b ORDER BY created_at, payment_id;");
        command.Parameters.AddWithValue("b", billId);
        var ids = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            ids.Add(reader.GetGuid(0));
        return ids;
    }

    private async Task<T> Scalar<T>(string sql, Guid id, params (string Name, object Value)[] extra)
    {
        await using var command = _source.CreateCommand(sql);
        command.Parameters.AddWithValue("p", id);
        foreach (var (name, value) in extra)
            command.Parameters.AddWithValue(name, value);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private async Task Execute(string sql, Guid id, params (string Name, object Value)[] extra)
    {
        await using var command = _source.CreateCommand(sql);
        command.Parameters.AddWithValue("p", id);
        foreach (var (name, value) in extra)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<RefundRequestV1> RequestAsync(
        HttpClient client, Guid billId, Guid paymentId, string reason, string? cookie = null)
    {
        using var response = await client.SendAsync(
            Post($"bills/{billId:D}/refund-requests", cookie ?? _cashierCookie, new RefundRequestInputV1(paymentId, reason)));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<RefundRequestV1>())!;
    }

    private async Task<RefundRequestV1> DecideAsync(
        HttpClient client, Guid requestId, string action, string cookie, RefundDecisionInputV1? body = null)
    {
        using var response = await client.SendAsync(Post($"refund-requests/{requestId:D}/{action}", cookie, body));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<RefundRequestV1>())!;
    }

    private HttpRequestMessage Post(string path, string cookie, object? body = null, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/terminals/{_terminalId:D}/billing/{path}");
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        if (path.EndsWith("/refund-requests", StringComparison.Ordinal))
            request.Headers.TryAddWithoutValidation("X-Idempotency-Key", key ?? Guid.NewGuid().ToString("N"));
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return request;
    }

    private static async Task<string?> ErrorCode(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("error").GetProperty("code").GetString();
    }

    private async Task<WebApplication> StartAsync(bool withStock = false)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(NpgsqlDataSource.Create(_database.ConnectionString));
        if (withStock)
        {
            builder.Services.AddSingleton<ALKAROS.Inventory.MovementReversal.IStockMovementReversalService>(_ =>
            {
                var locations = new ALKAROS.Inventory.StockMaster.PostgresStockLocationRepository(_source);
                var movements = new ALKAROS.Inventory.MovementLedger.PostgresStockMovementRepository(_source);
                var balances = new ALKAROS.Inventory.BalanceProjection.PostgresStockBalanceRepository(_source);
                return new ALKAROS.Inventory.MovementReversal.StockMovementReversalService(
                    movements, new ALKAROS.Inventory.StockMaster.PostgresStockItemRepository(_source), locations,
                    new ALKAROS.Inventory.BalanceProjection.StockBalanceProjector(balances, movements, locations), balances);
            });
        }

        builder.Services.AddBillingSplitExperience();
        var app = builder.Build();
        app.MapBillingSplitApi();
        await app.StartAsync();
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }
}
