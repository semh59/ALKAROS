using System.Net;
using System.Net.Http.Json;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

public sealed partial class PaymentRefundHttpTests
{
    [Fact]
    public async Task ATransferRefundNeedsNoSlipAndCompletesWhenThePersonConfirmsItWasSent()
    {
        var seeded = await _database.SeedBillAsync();
        await _database.SeedCollectedAsync(seeded.BillId, 100m, _cashierId);
        var paymentId = await PaymentIdAsync(seeded.BillId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var request = await RequestAsync(client, seeded.BillId, paymentId, "Havale iadesi");
        Assert.Equal("Eft", request.Method);
        await DecideAsync(client, request.RequestId, "approve", _managerCookie);

        using var complete = await client.SendAsync(
            Post($"refund-requests/{request.RequestId:D}/complete", _cashierCookie, new RefundCompleteInputV1(null, "Havale yapıldı")));

        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        Assert.Equal("Refunded", await Scalar<string>("SELECT status FROM payments.payments WHERE payment_id = @p", paymentId));
        Assert.Equal(1L, await Scalar<long>(
            "SELECT count(*) FROM payments.refund_requests WHERE refund_request_id = @p AND status = 'Completed' AND slip_number IS NULL",
            request.RequestId));
    }

    [Fact]
    public async Task ACardRefundStillNeedsTheSlipAndKeepsWaitingWithoutIt()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CardPaymentAsync(seeded.BillId, 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var request = await RequestAsync(client, seeded.BillId, paymentId, "Kart iadesi");
        Assert.Equal("Card", request.Method);
        await DecideAsync(client, request.RequestId, "approve", _managerCookie);

        using var withoutSlip = await client.SendAsync(
            Post($"refund-requests/{request.RequestId:D}/complete", _cashierCookie, new RefundCompleteInputV1(null, null)));
        Assert.Equal(HttpStatusCode.BadRequest, withoutSlip.StatusCode);
        Assert.Equal("REFUND_TEXT_INVALID", await ErrorCode(withoutSlip));
        Assert.Equal("AwaitingManualRefund", await Scalar<string>(
            "SELECT status FROM payments.refund_requests WHERE refund_request_id = @p", request.RequestId));

        using var withSlip = await client.SendAsync(
            Post($"refund-requests/{request.RequestId:D}/complete", _cashierCookie, new RefundCompleteInputV1("POS-5150", null)));
        Assert.Equal(HttpStatusCode.OK, withSlip.StatusCode);
        Assert.Equal("POS-5150", await Scalar<string>(
            "SELECT slip_number FROM payments.refund_requests WHERE refund_request_id = @p", request.RequestId));
    }

    [Fact]
    public async Task AnApprovedCardTerminalAttemptAlsoMakesItACardRefund()
    {
        var seeded = await _database.SeedBillAsync();
        await _database.SeedCollectedAsync(seeded.BillId, 100m, _cashierId);
        var paymentId = await PaymentIdAsync(seeded.BillId);
        await Execute(
            """
            INSERT INTO payments.card_settlement_attempts
                (card_settlement_attempt_id, bill_id, idempotency_key, provider_correlation_id, payment_id, outcome, approved_amount,
                 allocation_id, fiscal_handoff_queued, created_at)
            SELECT gen_random_uuid(), a.bill_id, 'k-' || @p::text, 'c-1', @p, 'Approved', 100, a.payment_allocation_id, true, now()
            FROM payments.payment_allocations a WHERE a.payment_id = @p;
            """, paymentId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var request = await RequestAsync(client, seeded.BillId, paymentId, "Kart iadesi");

        Assert.Equal("Card", request.Method);
    }

    [Fact]
    public async Task AClaimThatWasRejectedAndADeclinedAttemptDoNotMakeATransferACardRefund()
    {
        var seeded = await _database.SeedBillAsync();
        await _database.SeedCollectedAsync(seeded.BillId, 100m, _cashierId);
        var paymentId = await PaymentIdAsync(seeded.BillId);
        await Execute(
            """
            INSERT INTO payments.manual_card_confirmations
                (confirmation_id, payment_id, bill_id, slip_number, amount, status, requested_by, requested_at, decided_by, decided_at)
            VALUES (gen_random_uuid(), @p, @b, 'REJ-' || substr(@p::text, 1, 8), 100, 'Rejected', @r, now(), @d, now());
            INSERT INTO payments.card_settlement_attempts
                (card_settlement_attempt_id, bill_id, idempotency_key, provider_correlation_id, payment_id, outcome, created_at)
            VALUES (gen_random_uuid(), @b, 'declined-' || @p::text, 'c-2', @p, 'Declined', now());
            """, paymentId, ("b", seeded.BillId), ("r", _cashierId), ("d", _managerId));
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var request = await RequestAsync(client, seeded.BillId, paymentId, "Havale iadesi");

        Assert.Equal("Eft", request.Method);
    }

    [Fact]
    public async Task ATransferRefundWithinTheAllowanceIsCompletedByTheCashierWithoutASlip()
    {
        var seeded = await _database.SeedBillAsync();
        await _database.SeedCollectedAsync(seeded.BillId, 100m, _cashierId);
        var paymentId = await PaymentIdAsync(seeded.BillId);
        await OpenSessionAsync(_cashierId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        await GrantAsync(client, _cashierId, 100m);

        var request = await RequestAsync(client, seeded.BillId, paymentId, "Havale iadesi");
        Assert.Equal("Eft", request.Method);
        Assert.Equal("AwaitingManualRefund", request.Status);
        Assert.True(request.ApprovedFromAllowance);
        using var complete = await client.SendAsync(
            Post($"refund-requests/{request.RequestId:D}/complete", _cashierCookie, new RefundCompleteInputV1(null, null)));

        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var completed = (await complete.Content.ReadFromJsonAsync<RefundRequestV1>())!;
        Assert.Equal("Completed", completed.Status);
        Assert.Null(completed.SlipNumber);
        Assert.Equal("Refunded", await Scalar<string>("SELECT status FROM payments.payments WHERE payment_id = @p", paymentId));
    }

    [Fact]
    public async Task TheMigrationBackFillsTheMethodAndTheTerminalOfRequestsMadeBeforeIt()
    {
        var seeded = await _database.SeedBillAsync();
        var cashPayment = await CashPaymentAsync(seeded.BillId, 100m);
        var cardPayment = await CardPaymentAsync(seeded.BillId, 50m);
        await _database.SeedCollectedAsync(seeded.BillId, 25m, _cashierId);
        var transferPayment = (await PaymentIdsAsync(seeded.BillId)).Last();
        var till = await OpenSessionAsync(_cashierId);
        await RunMigrationScriptAsync("215-refund-method-terminal-self-approval.down.sql");
        foreach (var (payment, method) in new[] { (cashPayment, "Cash"), (cardPayment, "Manual"), (transferPayment, "Manual") })
        {
            await Execute(
                """
                INSERT INTO payments.refund_requests
                    (refund_request_id, bill_id, payment_id, amount, method, reason, status, requested_by, requested_at)
                VALUES (gen_random_uuid(), @b, @p, 10, @m, 'eski', 'Requested', @u, now() + interval '1 minute');
                """, payment, ("b", seeded.BillId), ("m", method), ("u", _cashierId));
        }

        await RunMigrationScriptAsync("215-refund-method-terminal-self-approval.up.sql");

        Assert.Equal("Cash", await Scalar<string>("SELECT method FROM payments.refund_requests WHERE payment_id = @p", cashPayment));
        Assert.Equal("Card", await Scalar<string>("SELECT method FROM payments.refund_requests WHERE payment_id = @p", cardPayment));
        Assert.Equal("Eft", await Scalar<string>("SELECT method FROM payments.refund_requests WHERE payment_id = @p", transferPayment));
        Assert.Equal(3L, await Scalar<long>(
            "SELECT count(*) FROM payments.refund_requests WHERE terminal_id = (SELECT terminal_id FROM cash.cash_sessions WHERE cash_session_id = @p)",
            till));
    }

    [Fact]
    public async Task TheMigrationLeavesTheTerminalEmptyWhenTheCashierHadTillsOpenAtTwoTerminals()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        await OpenSessionAsync(_cashierId);
        await OpenSessionAsync(_cashierId, terminalId: Guid.NewGuid());
        await RunMigrationScriptAsync("215-refund-method-terminal-self-approval.down.sql");
        await Execute(
            """
            INSERT INTO payments.refund_requests
                (refund_request_id, bill_id, payment_id, amount, method, reason, status, requested_by, requested_at)
            VALUES (gen_random_uuid(), @b, @p, 10, 'Cash', 'eski', 'Requested', @u, now() + interval '1 minute');
            """, paymentId, ("b", seeded.BillId), ("u", _cashierId));

        await RunMigrationScriptAsync("215-refund-method-terminal-self-approval.up.sql");

        Assert.Equal(1L, await Scalar<long>(
            "SELECT count(*) FROM payments.refund_requests WHERE payment_id = @p AND terminal_id IS NULL", paymentId));
    }

    private async Task RunMigrationScriptAsync(string file)
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, "ALKAROS.slnx")))
            directory = Path.GetDirectoryName(directory);
        var script = await File.ReadAllTextAsync(
            Path.Combine(directory!, "database", "migrations", "V1", "V1-RMD-793", file));
        await using var command = _source.CreateCommand(script);
        await command.ExecuteNonQueryAsync();
    }
}
