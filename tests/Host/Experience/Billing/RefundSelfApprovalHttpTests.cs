using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.Billing.Tests;

public sealed partial class PaymentRefundHttpTests
{
    [Fact]
    public async Task TheRequesterDecidesTheirOwnRefundOnlyWhenTheSharedSettingIsOnAndTheRecordSaysSo()
    {
        var (selfId, selfCookie) = await _database.SeedSessionWithPermissionsAsync(
            _terminalId, "supervisor", "payments.refund.request", "payments.refund.approve");
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        await OpenSessionAsync(selfId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var request = await RequestAsync(client, seeded.BillId, paymentId, "Kendi iadem", selfCookie);

        using var refused = await client.SendAsync(Post($"refund-requests/{request.RequestId:D}/approve", selfCookie));
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Contains("başka bir yetkili", await ErrorMessage(refused));
        Assert.Equal("Requested", await Scalar<string>(
            "SELECT status FROM payments.refund_requests WHERE refund_request_id = @p", request.RequestId));

        await SetSelfApprovalAsync(true);
        var approved = await DecideAsync(client, request.RequestId, "approve", selfCookie);

        Assert.Equal("Completed", approved.Status);
        Assert.True(approved.SelfApproved);
        Assert.Equal("Self", await Scalar<string>(
            "SELECT approval_mode FROM payments.refund_requests WHERE refund_request_id = @p", request.RequestId));
        Assert.Equal(2L, await Scalar<long>(
            """
            SELECT count(*) FROM audit.audit_events
            WHERE aggregate_id = @p AND event_name IN ('refund.approved', 'payment.refunded')
              AND (after_state_json::jsonb ->> 'selfApproved') = 'true'
            """, paymentId));
        Assert.Equal(request.RequestId, Assert.Single(await ListAsync(client, "?selfApproved=true")).RequestId);
        Assert.Empty(await ListAsync(client, "?selfApproved=false"));
    }

    [Fact]
    public async Task TheListSaysWhetherTheCallerMayDecideEachRequestFromTheSharedSetting()
    {
        var (selfId, selfCookie) = await _database.SeedSessionWithPermissionsAsync(
            _terminalId, "supervisor", "payments.refund.request", "payments.refund.approve");
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        await OpenSessionAsync(selfId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        await RequestAsync(client, seeded.BillId, paymentId, "Kendi iadem", selfCookie);

        async Task<bool> MayDecideAsync(string cookie)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/terminals/{_terminalId:D}/billing/refund-requests");
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
            using var response = await client.SendAsync(request);
            return Assert.Single((await response.Content.ReadFromJsonAsync<List<RefundRequestV1>>())!).MayDecide;
        }

        await SetSelfApprovalAsync(false);
        Assert.False(await MayDecideAsync(selfCookie));
        Assert.True(await MayDecideAsync(_managerCookie));

        await SetSelfApprovalAsync(true);
        Assert.True(await MayDecideAsync(selfCookie));
    }

    [Fact]
    public async Task ADecisionByAnotherPersonIsNeverMarkedAsSelfApprovedEvenWhenTheSettingIsOn()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        await OpenSessionAsync(_cashierId);
        await SetSelfApprovalAsync(true);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var request = await RequestAsync(client, seeded.BillId, paymentId, "Normal iade");

        var approved = await DecideAsync(client, request.RequestId, "approve", _managerCookie);

        Assert.False(approved.SelfApproved);
        Assert.Equal("Manager", await Scalar<string>(
            "SELECT approval_mode FROM payments.refund_requests WHERE refund_request_id = @p", request.RequestId));
        Assert.Equal(request.RequestId, Assert.Single(await ListAsync(client, "?selfApproved=false")).RequestId);
        Assert.Empty(await ListAsync(client, "?selfApproved=true"));
    }

    [Fact]
    public async Task TheRequesterRejectsTheirOwnRefundOnlyWhenTheSharedSettingIsOn()
    {
        var (selfId, selfCookie) = await _database.SeedSessionWithPermissionsAsync(
            _terminalId, "supervisor", "payments.refund.request", "payments.refund.approve");
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);
        await OpenSessionAsync(selfId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var request = await RequestAsync(client, seeded.BillId, paymentId, "Vazgeçtim", selfCookie);
        var note = new RefundDecisionInputV1("Müşteri vazgeçti");

        using var refused = await client.SendAsync(Post($"refund-requests/{request.RequestId:D}/reject", selfCookie, note));
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        await SetSelfApprovalAsync(true);
        var rejected = await DecideAsync(client, request.RequestId, "reject", selfCookie, note);

        Assert.Equal("Rejected", rejected.Status);
        Assert.True(rejected.SelfApproved);
        Assert.Equal("Self", await Scalar<string>(
            "SELECT approval_mode FROM payments.refund_requests WHERE refund_request_id = @p", request.RequestId));
        Assert.Equal(1L, await Scalar<long>(
            """
            SELECT count(*) FROM audit.audit_events
            WHERE aggregate_id = @p AND event_name = 'refund.rejected' AND (after_state_json::jsonb ->> 'selfApproved') = 'true'
            """, paymentId));
        Assert.Equal(request.RequestId, Assert.Single(await ListAsync(client, "?selfApproved=true")).RequestId);
    }

    [Fact]
    public async Task ASelfApprovedTransferRefundIsMarkedAgainWhenThePersonConfirmsItWasSent()
    {
        var (selfId, selfCookie) = await _database.SeedSessionWithPermissionsAsync(
            _terminalId, "supervisor", "payments.refund.request", "payments.refund.approve");
        var seeded = await _database.SeedBillAsync();
        await _database.SeedCollectedAsync(seeded.BillId, 100m, selfId);
        var paymentId = await PaymentIdAsync(seeded.BillId);
        await SetSelfApprovalAsync(true);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var request = await RequestAsync(client, seeded.BillId, paymentId, "Havale iadesi", selfCookie);
        var approved = await DecideAsync(client, request.RequestId, "approve", selfCookie);
        Assert.Equal("AwaitingManualRefund", approved.Status);
        Assert.True(approved.SelfApproved);

        using var complete = await client.SendAsync(
            Post($"refund-requests/{request.RequestId:D}/complete", selfCookie, new RefundCompleteInputV1(null, null)));

        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        Assert.True((await complete.Content.ReadFromJsonAsync<RefundRequestV1>())!.SelfApproved);
        Assert.Equal(1L, await Scalar<long>(
            """
            SELECT count(*) FROM audit.audit_events
            WHERE aggregate_id = @p AND event_name = 'payment.refunded' AND (after_state_json::jsonb ->> 'selfApproved') = 'true'
            """, paymentId));
    }

    [Fact]
    public async Task AManagerGivesThemselvesARefundAllowanceOnlyWhenTheSharedSettingIsOnAndItIsAudited()
    {
        var (selfId, selfCookie) = await _database.SeedSessionWithPermissionsAsync(
            _terminalId, "supervisor", "payments.refund.request", "payments.refund.approve");
        var selfTerminalId = Guid.NewGuid();
        await OpenSessionAsync(selfId, terminalId: selfTerminalId);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var refused = await PutAllowanceAsync(client, selfId, 50m, selfCookie, selfTerminalId);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("SELF_DECISION", await ErrorCode(refused));

        await SetSelfApprovalAsync(true);
        using var granted = await PutAllowanceAsync(client, selfId, 50m, selfCookie, selfTerminalId);

        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
        Assert.Equal(1L, await Scalar<long>(
            """
            SELECT count(*) FROM audit.audit_events
            WHERE event_name = 'refund.allowance.set' AND actor_id = @p AND (after_state_json::jsonb ->> 'selfApproved') = 'true'
            """, selfId));

        await OpenSessionAsync(_cashierId);
        using var forTheCashier = await PutAllowanceAsync(client, _cashierId, 70m, selfCookie);
        Assert.Equal(HttpStatusCode.OK, forTheCashier.StatusCode);
        Assert.Equal(1L, await Scalar<long>(
            """
            SELECT count(*) FROM audit.audit_events
            WHERE event_name = 'refund.allowance.set' AND actor_id = @p AND (after_state_json::jsonb ->> 'selfApproved') = 'false'
            """, selfId));

        await SetSelfApprovalAsync(false);
        using var refusedAgain = await PutAllowanceAsync(client, selfId, 60m, selfCookie, selfTerminalId);
        Assert.Equal(HttpStatusCode.Forbidden, refusedAgain.StatusCode);
        Assert.Equal(50m, await Scalar<decimal>(
            "SELECT refund_allowance FROM cash.cash_sessions WHERE cashier_user_id = @p AND status = 'Open'", selfId));
    }

    [Fact]
    public async Task ARowMarkedSelfApprovedMustHaveBeenDecidedByItsRequester()
    {
        var seeded = await _database.SeedBillAsync();
        var paymentId = await CashPaymentAsync(seeded.BillId, 100m);

        var failure = await Assert.ThrowsAnyAsync<NpgsqlException>(() => Execute(
            """
            INSERT INTO payments.refund_requests
                (refund_request_id, bill_id, payment_id, amount, method, reason, status, requested_by, requested_at,
                 decided_by, decided_at, approval_mode)
            VALUES (gen_random_uuid(), @b, @p, 100, 'Eft', 'x', 'Rejected', @u, now(), @m, now(), 'Self');
            """, paymentId, ("b", seeded.BillId), ("u", _cashierId), ("m", _managerId)));

        Assert.Equal("23514", ((PostgresException)failure).SqlState);
    }

    private async Task SetSelfApprovalAsync(bool enabled)
    {
        await using var command = _source.CreateCommand(
            """
            INSERT INTO settings.settings (setting_id, setting_key, setting_value, data_type, scope, module_owner)
            VALUES (gen_random_uuid(), 'approvals.manager_self_approval_enabled', @value, 'Boolean', 'Global', 'cash')
            ON CONFLICT (setting_key) DO UPDATE SET setting_value = @value;
            """);
        command.Parameters.AddWithValue("value", enabled ? "true" : "false");
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ErrorMessage(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("error").GetProperty("message").GetString();
    }
}
