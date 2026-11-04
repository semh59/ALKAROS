using System.Net;
using ALKAROS.Cash.TransactionLedger;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

public sealed partial class PaymentRefundHttpTests
{
    [Fact]
    public async Task ACashRefundIsPaidOutOfTheTillOfTheTerminalItWasAskedAtNotTheNewestTillOfTheCashier()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        var askedAt = await OpenSessionAsync(_cashierId);
        var newer = await OpenSessionAsync(_cashierId, terminalId: Guid.NewGuid());
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var request = await RequestAsync(client, seeded.BillId, paymentId, "Yanlış tutar");
        Assert.Equal(_terminalId, request.TerminalId);

        var approved = await DecideAsync(client, request.RequestId, "approve", _managerCookie);

        Assert.Equal("Completed", approved.Status);
        Assert.Equal(1L, await Scalar<long>(
            "SELECT count(*) FROM cash.cash_transactions WHERE cash_session_id = @p AND type = 'Refund' AND amount = 100", askedAt));
        Assert.Equal(0L, await Scalar<long>(
            "SELECT count(*) FROM cash.cash_transactions WHERE cash_session_id = @p AND type = 'Refund'", newer));
    }

    [Fact]
    public async Task ARefundAskedAtATerminalWhereTheCashierHasNoOpenTillIsNeverPaidFromAnotherTill()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        var otherTill = await OpenSessionAsync(_cashierId, terminalId: Guid.NewGuid());
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var request = await RequestAsync(client, seeded.BillId, paymentId, "Yanlış tutar");

        using var response = await client.SendAsync(Post($"refund-requests/{request.RequestId:D}/approve", _managerCookie));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("NO_OPEN_CASH_SESSION", await ErrorCode(response));
        Assert.Equal(0L, await Scalar<long>(
            "SELECT count(*) FROM cash.cash_transactions WHERE cash_session_id = @p AND type = 'Refund'", otherTill));
        Assert.Equal("Approved", await Scalar<string>("SELECT status FROM payments.payments WHERE payment_id = @p", paymentId));
    }

    [Fact]
    public async Task ARequestFromBeforeTheTerminalWasRecordedIsNotPaidFromAnyTill()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        var till = await OpenSessionAsync(_cashierId);
        var requestId = Guid.NewGuid();
        await Execute(
            """
            INSERT INTO payments.refund_requests
                (refund_request_id, bill_id, payment_id, amount, method, reason, status, requested_by, requested_at)
            VALUES (@p, @b, @pay, 100, 'Cash', 'Eski istek', 'Requested', @u, now());
            """, requestId, ("b", seeded.BillId), ("pay", paymentId), ("u", _cashierId));
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var response = await client.SendAsync(Post($"refund-requests/{requestId:D}/approve", _managerCookie));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("NO_OPEN_CASH_SESSION", await ErrorCode(response));
        Assert.Equal(0L, await Scalar<long>(
            "SELECT count(*) FROM cash.cash_transactions WHERE cash_session_id = @p AND type = 'Refund'", till));
    }

    [Fact]
    public async Task TheRefundAllowanceBelongsToTheTillOfTheTerminalItWasGrantedAt()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 60m);
        var thisTill = await OpenSessionAsync(_cashierId);
        var otherTill = await OpenSessionAsync(_cashierId, terminalId: Guid.NewGuid());
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var granted = await PutAllowanceAsync(client, _cashierId, 100m, _managerCookie);
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
        Assert.Equal(100m, await Scalar<decimal>("SELECT refund_allowance FROM cash.cash_sessions WHERE cash_session_id = @p", thisTill));
        Assert.Equal(0m, await Scalar<decimal>("SELECT refund_allowance FROM cash.cash_sessions WHERE cash_session_id = @p", otherTill));
        Assert.Equal(new RefundAllowanceV1(true, 100m, 0m, 100m), await GetAllowanceAsync(client, _cashierCookie));

        var request = await RequestAsync(client, seeded.BillId, paymentId, "Bütçeden");
        Assert.True(request.ApprovedFromAllowance);
        Assert.Equal(60m, await Scalar<decimal>("SELECT refund_allowance_used FROM cash.cash_sessions WHERE cash_session_id = @p", thisTill));
        Assert.Equal(0m, await Scalar<decimal>("SELECT refund_allowance_used FROM cash.cash_sessions WHERE cash_session_id = @p", otherTill));
        Assert.Equal(1L, await Scalar<long>(
            "SELECT count(*) FROM cash.cash_transactions WHERE cash_session_id = @p AND type = 'Refund'", thisTill));

        using var noTill = await PutAllowanceAsync(client, _cashierId, 50m, _managerCookie, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Conflict, noTill.StatusCode);
        Assert.Equal("NO_OPEN_CASH_SESSION", await ErrorCode(noTill));
    }

    [Fact]
    public async Task TheAllowanceReadsEmptyAtATerminalWhereTheCashierHasNoOpenTill()
    {
        await OpenSessionAsync(_cashierId, terminalId: Guid.NewGuid());
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        Assert.Equal(new RefundAllowanceV1(false, 0m, 0m, 0m), await GetAllowanceAsync(client, _cashierCookie));
    }

    [Fact]
    public async Task AnAllowanceRefundMeetingATillThatClosedMeanwhileSpendsNothingAndWaitsForTheManager()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        var sessionId = await OpenSessionAsync(_cashierId);
        await Execute("UPDATE cash.cash_sessions SET refund_allowance = 500 WHERE cash_session_id = @p", sessionId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        await using var connection = await _source.OpenConnectionAsync();
        await using var closing = await connection.BeginTransactionAsync();
        await using (var lockRow = new NpgsqlCommand(
            "SELECT 1 FROM cash.cash_sessions WHERE cash_session_id = @id FOR UPDATE;", connection, closing))
        {
            lockRow.Parameters.AddWithValue("id", sessionId);
            await lockRow.ExecuteNonQueryAsync();
        }
        var asking = RequestAsync(client, seeded.BillId, paymentId, "Kapanışla yarış");
        await WaitUntilARequestWaitsOnALockAsync();
        await using (var close = new NpgsqlCommand(
            "UPDATE cash.cash_sessions SET status = 'Closed', closed_at = now() WHERE cash_session_id = @id;", connection, closing))
        {
            close.Parameters.AddWithValue("id", sessionId);
            await close.ExecuteNonQueryAsync();
        }
        await closing.CommitAsync();

        var request = await asking;

        Assert.Equal("Requested", request.Status);
        Assert.False(request.ApprovedFromAllowance);
        Assert.Equal(0m, await Scalar<decimal>("SELECT refund_allowance_used FROM cash.cash_sessions WHERE cash_session_id = @p", sessionId));
        Assert.Equal(0L, await Scalar<long>(
            "SELECT count(*) FROM cash.cash_transactions WHERE cash_session_id = @p AND type = 'Refund'", sessionId));
    }

    [Fact]
    public async Task AGrantMeetingATillThatClosedMeanwhileIsRefusedAndGivesNothing()
    {
        var sessionId = await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        await using var connection = await _source.OpenConnectionAsync();
        await using var closing = await connection.BeginTransactionAsync();
        await using (var lockRow = new NpgsqlCommand(
            "SELECT 1 FROM cash.cash_sessions WHERE cash_session_id = @id FOR UPDATE;", connection, closing))
        {
            lockRow.Parameters.AddWithValue("id", sessionId);
            await lockRow.ExecuteNonQueryAsync();
        }
        var granting = PutAllowanceAsync(client, _cashierId, 100m, _managerCookie);
        await WaitUntilARequestWaitsOnALockAsync();
        await using (var close = new NpgsqlCommand(
            "UPDATE cash.cash_sessions SET status = 'Closed', closed_at = now() WHERE cash_session_id = @id;", connection, closing))
        {
            close.Parameters.AddWithValue("id", sessionId);
            await close.ExecuteNonQueryAsync();
        }
        await closing.CommitAsync();

        using var response = await granting;

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("NO_OPEN_CASH_SESSION", await ErrorCode(response));
        Assert.Equal(0m, await Scalar<decimal>("SELECT refund_allowance FROM cash.cash_sessions WHERE cash_session_id = @p", sessionId));
    }

    [Fact]
    public async Task AManagerAtAnotherTerminalApprovesAndTheRefundStillLeavesTheTillOfTheRequestingTerminal()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        var askedAt = await OpenSessionAsync(_cashierId);
        var managerTerminalId = Guid.NewGuid();
        var (remoteManagerId, remoteManagerCookie) = await _database.SeedSessionWithPermissionsAsync(
            managerTerminalId, "manager", "payments.refund.request", "payments.refund.approve");
        var managersOwnTill = await OpenSessionAsync(remoteManagerId, terminalId: managerTerminalId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var request = await RequestAsync(client, seeded.BillId, paymentId, "Başka terminalden onay");

        var approval = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/terminals/{managerTerminalId:D}/billing/refund-requests/{request.RequestId:D}/approve");
        approval.Headers.TryAddWithoutValidation("Cookie", remoteManagerCookie);
        using var response = await client.SendAsync(approval);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1L, await Scalar<long>(
            "SELECT count(*) FROM cash.cash_transactions WHERE cash_session_id = @p AND type = 'Refund'", askedAt));
        Assert.Equal(0L, await Scalar<long>(
            "SELECT count(*) FROM cash.cash_transactions WHERE cash_session_id = @p AND type = 'Refund'", managersOwnTill));
    }

    [Fact]
    public async Task APartialCashRefundLowersTheCashTheTillIsExpectedToHold()
    {
        var seeded = await _database.SeedBillAsync();
        var sessionId = await OpenSessionAsync(_cashierId);
        var paymentId = await CashPaymentAsync(seeded.BillId, 250m, saleSessionId: sessionId);
        var unitPrice = await Scalar<decimal>(
            "SELECT round(gross_amount / quantity, 2) FROM billing.bill_items WHERE bill_item_id = @p", seeded.FirstItemId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var request = await RequestItemsAsync(client, seeded.BillId, paymentId, [new RefundItemInputV1(seeded.FirstItemId, 1m, false)]);

        var approved = await DecideAsync(client, request.RequestId, "approve", _managerCookie);

        Assert.Equal("Completed", approved.Status);
        Assert.Equal(unitPrice, approved.Amount);
        Assert.True(approved.Amount < 250m);
        await using var connection = await _source.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        Assert.Equal(
            250m - unitPrice,
            await new PostgresCashTransactionLedgerRepository(_source).LockOpenSessionAndReadExpectedCashAsync(
                sessionId, connection, transaction));
    }
}
