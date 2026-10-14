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
            Assert.Equal("REFUND_INPUT_INVALID", await ErrorCode(noSlip));
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
        Assert.Equal("REFUND_INPUT_INVALID", await ErrorCode(negative));
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

    private async Task<RefundRequestV1> DecideAsync(HttpClient client, Guid requestId, string action, string cookie)
    {
        using var response = await client.SendAsync(Post($"refund-requests/{requestId:D}/{action}", cookie));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<RefundRequestV1>())!;
    }

    private HttpRequestMessage Post(string path, string cookie, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/terminals/{_terminalId:D}/billing/{path}");
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return request;
    }

    private static async Task<string?> ErrorCode(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("error").GetProperty("code").GetString();
    }

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(NpgsqlDataSource.Create(_database.ConnectionString));
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
