using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ALKAROS.Host.Experience.Billing;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.PaymentTender.Tests;

public sealed partial class PaymentTenderHttpTests
{
    private static string ExternalCardPath(Guid terminalId, Guid billId) => TendersPath(terminalId, billId) + "external-card";

    private async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _database.DataSource.CreateCommand(sql);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<string> ErrorCodeAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetProperty("code").GetString()!;

    private async Task<(Guid TerminalId, string Cookie, Guid BillId)> ExternalCardSetupAsync(string token, decimal payable = 100m)
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, token);
        var billId = await _database.SeedBillAsync(payable);
        await _database.RemoveCardTerminalAsync();
        return (terminalId, cookie, billId);
    }

    [Fact]
    public async Task WithoutACardTerminalAnExternalCardPaymentIsRecordedWithItsSlipAndNeverTouchesCashOrTheDrawer()
    {
        var (terminalId, cookie, billId) = await ExternalCardSetupAsync("ext-happy");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var response = await PostAsync(client, ExternalCardPath(terminalId, billId), cookie,
            new { Amount = 100m, SlipNumber = "  pos-0042 ", IdempotencyKey = "ext-happy-1" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Approved", body.GetProperty("outcome").GetString());
        Assert.Equal(100m, body.GetProperty("approvedAmount").GetDecimal());
        Assert.True(body.GetProperty("billClosed").GetBoolean());
        Assert.Equal(1L, await _database.PaymentCountAsync(billId));
        Assert.Equal("POS-0042", await ScalarAsync<string>("SELECT slip_number FROM payments.external_card_receipts WHERE bill_id = @b", ("b", billId)));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM cash.cash_transactions"));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM audit.audit_events WHERE event_name LIKE 'cash.drawer.%'"));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM payments.card_settlement_attempts"));
        var summary = await (await GetAsync(client, TendersPath(terminalId, billId), cookie)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ExternalCard", Assert.Single(summary.GetProperty("allocations").EnumerateArray()).GetProperty("method").GetString());
    }

    [Fact]
    public async Task TheAuditTrailNamesWhoRecordedWhichSlipAndHowMuchWithoutAnyCardNumberField()
    {
        var (terminalId, cookie, billId) = await ExternalCardSetupAsync("ext-audit");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        await PostAsync(client, ExternalCardPath(terminalId, billId), cookie,
            new { Amount = 40m, SlipNumber = "ABC-123", IdempotencyKey = "ext-audit-1" });

        var actor = await ScalarAsync<Guid>("SELECT actor_id FROM audit.audit_events WHERE event_name = 'payment.external-card-recorded'");
        var state = JsonDocument.Parse(await ScalarAsync<string>(
            "SELECT after_state_json::text FROM audit.audit_events WHERE event_name = 'payment.external-card-recorded'")).RootElement;

        Assert.Equal(await ScalarAsync<Guid>("SELECT recorded_by FROM payments.external_card_receipts"), actor);
        Assert.Equal("ABC-123", state.GetProperty("slipNumber").GetString());
        Assert.Equal(40m, state.GetProperty("amount").GetDecimal());
        Assert.Equal(terminalId, state.GetProperty("terminalId").GetGuid());
        Assert.Equal(["amount", "billId", "method", "slipNumber", "terminalId"], state.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM payments.payment_status_history WHERE reason IS NOT NULL"));
    }

    [Fact]
    public async Task WithACardTerminalDefinedTheExternalCardPaymentIsRefusedAndNothingIsRecorded()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "ext-terminal");
        var billId = await _database.SeedBillAsync(100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var response = await PostAsync(client, ExternalCardPath(terminalId, billId), cookie,
            new { Amount = 100m, SlipNumber = "POS-0042", IdempotencyKey = "ext-terminal-1" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("CARD_TERMINAL_CONFIGURED", await ErrorCodeAsync(response));
        Assert.Equal(0L, await _database.PaymentCountAsync(billId));
    }

    [Theory]
    [InlineData("", "EXTERNAL_CARD_SLIP_INVALID")]
    [InlineData("    ", "EXTERNAL_CARD_SLIP_INVALID")]
    [InlineData("AB1", "EXTERNAL_CARD_SLIP_INVALID")]
    [InlineData("AB 12", "EXTERNAL_CARD_SLIP_INVALID")]
    [InlineData("AB*12", "EXTERNAL_CARD_SLIP_INVALID")]
    [InlineData("-ABC1", "EXTERNAL_CARD_SLIP_INVALID")]
    [InlineData("4111111111111111", "EXTERNAL_CARD_SLIP_IS_CARD_NUMBER")]
    [InlineData("4111-1111-1111-1111", "EXTERNAL_CARD_SLIP_IS_CARD_NUMBER")]
    [InlineData("378282246310005", "EXTERNAL_CARD_SLIP_IS_CARD_NUMBER")]
    public async Task ASlipThatIsMissingMalformedOrLooksLikeACardNumberIsRefusedInTurkishAndNothingIsRecorded(string slip, string code)
    {
        var (terminalId, cookie, billId) = await ExternalCardSetupAsync("ext-bad-" + code.Length + slip.Length);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var response = await PostAsync(client, ExternalCardPath(terminalId, billId), cookie,
            new { Amount = 50m, SlipNumber = slip, IdempotencyKey = "ext-bad-1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.Contains("numara", error.GetProperty("message").GetString());
        Assert.Equal(0L, await _database.PaymentCountAsync(billId));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM payments.external_card_receipts"));
    }

    [Fact]
    public async Task TheSlipLengthLimitsAreTheServerConstantsAndTheDigitsOnlyCheckOnlyCatchesCardLikeNumbers()
    {
        Assert.Equal(4, ExternalCardTenderStore.MinSlipLength);
        Assert.Equal(32, ExternalCardTenderStore.MaxSlipLength);
        var (terminalId, cookie, billId) = await ExternalCardSetupAsync("ext-limits", payable: 400m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        async Task<HttpStatusCode> TryAsync(string slip, string key) =>
            (await PostAsync(client, ExternalCardPath(terminalId, billId), cookie, new { Amount = 10m, SlipNumber = slip, IdempotencyKey = key })).StatusCode;

        Assert.Equal(HttpStatusCode.OK, await TryAsync(new string('A', ExternalCardTenderStore.MinSlipLength), "ext-limits-min"));
        Assert.Equal(HttpStatusCode.OK, await TryAsync(new string('B', ExternalCardTenderStore.MaxSlipLength), "ext-limits-max"));
        Assert.Equal(HttpStatusCode.BadRequest, await TryAsync(new string('C', ExternalCardTenderStore.MinSlipLength - 1), "ext-limits-short"));
        Assert.Equal(HttpStatusCode.BadRequest, await TryAsync(new string('D', ExternalCardTenderStore.MaxSlipLength + 1), "ext-limits-long"));
        // A 12-digit reference and a 16-digit number that fails the Luhn check are slips, not card numbers.
        Assert.Equal(HttpStatusCode.OK, await TryAsync("123456789012", "ext-limits-12"));
        Assert.Equal(HttpStatusCode.OK, await TryAsync("4111111111111112", "ext-limits-luhn"));
    }

    [Fact]
    public async Task TheSameSlipOnTheSameBillIsRefusedWhateverItsCaseButAnotherBillMayUseTheNumber()
    {
        var (terminalId, cookie, billId) = await ExternalCardSetupAsync("ext-dup");
        var otherBill = await _database.SeedBillAsync(100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var first = await PostAsync(client, ExternalCardPath(terminalId, billId), cookie,
            new { Amount = 30m, SlipNumber = "POS-77", IdempotencyKey = "ext-dup-1" });

        var again = await PostAsync(client, ExternalCardPath(terminalId, billId), cookie,
            new { Amount = 30m, SlipNumber = "pos-77", IdempotencyKey = "ext-dup-2" });
        var elsewhere = await PostAsync(client, ExternalCardPath(terminalId, otherBill), cookie,
            new { Amount = 30m, SlipNumber = "POS-77", IdempotencyKey = "ext-dup-3" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("EXTERNAL_CARD_SLIP_REUSED", await ErrorCodeAsync(again));
        Assert.Equal(HttpStatusCode.OK, elsewhere.StatusCode);
        Assert.Equal(1L, await _database.PaymentCountAsync(billId));
    }

    [Fact]
    public async Task ARetryWithTheSameKeyReturnsTheSameResultAndDifferentContentWithThatKeyIsRefused()
    {
        var (terminalId, cookie, billId) = await ExternalCardSetupAsync("ext-replay");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var body = new { Amount = 30m, SlipNumber = "POS-501", IdempotencyKey = "ext-replay-1" };
        await PostAsync(client, ExternalCardPath(terminalId, billId), cookie, body);

        var replay = await PostAsync(client, ExternalCardPath(terminalId, billId), cookie, body);
        var otherSlip = await PostAsync(client, ExternalCardPath(terminalId, billId), cookie, body with { SlipNumber = "POS-502" });
        var otherAmount = await PostAsync(client, ExternalCardPath(terminalId, billId), cookie, body with { Amount = 31m });

        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(30m, (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("approvedAmount").GetDecimal());
        Assert.Equal(HttpStatusCode.Conflict, otherSlip.StatusCode);
        Assert.Equal("TENDER_IDEMPOTENCY_KEY_REUSED", await ErrorCodeAsync(otherSlip));
        Assert.Equal(HttpStatusCode.Conflict, otherAmount.StatusCode);
        var otherBill = await _database.SeedBillAsync(100m);
        var elsewhere = await PostAsync(client, ExternalCardPath(terminalId, otherBill), cookie, body);
        Assert.Equal(HttpStatusCode.Conflict, elsewhere.StatusCode);
        Assert.Equal("TENDER_IDEMPOTENCY_KEY_REUSED", await ErrorCodeAsync(elsewhere));
        Assert.Equal(1L, await _database.PaymentCountAsync(billId));
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM audit.audit_events WHERE event_name = 'payment.external-card-recorded'"));
    }

    [Fact]
    public async Task AKeyAlreadyUsedByAnotherTenderMethodIsNotReplayedAsAnExternalCardPayment()
    {
        var (terminalId, cookie, billId) = await ExternalCardSetupAsync("ext-other-method");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        await PostAsync(client, TendersPath(terminalId, billId), cookie, new { Method = "Eft", Amount = 30m, IdempotencyKey = "ext-shared-key" });

        var response = await PostAsync(client, ExternalCardPath(terminalId, billId), cookie,
            new { Amount = 30m, SlipNumber = "POS-601", IdempotencyKey = "ext-shared-key" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("TENDER_IDEMPOTENCY_KEY_REUSED", await ErrorCodeAsync(response));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM payments.external_card_receipts"));
    }

    [Fact]
    public async Task TwoSimultaneousSubmissionsWithOneKeyRecordOnePayment()
    {
        var (terminalId, cookie, billId) = await ExternalCardSetupAsync("ext-double-click");
        await using var app = await StartAsync();
        using var clientA = CreateClient(app);
        using var clientB = CreateClient(app);
        var body = new { Amount = 60m, SlipNumber = "POS-901", IdempotencyKey = "ext-double-click-1" };

        var responses = await Task.WhenAll(
            PostAsync(clientA, ExternalCardPath(terminalId, billId), cookie, body),
            PostAsync(clientB, ExternalCardPath(terminalId, billId), cookie, body));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(1L, await _database.PaymentCountAsync(billId));
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM payments.external_card_receipts"));
    }

    [Fact]
    public async Task WithoutPaymentsTakeTheExternalCardPaymentIsForbidden()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "ext-forbidden", takesPayments: false);
        var billId = await _database.SeedBillAsync(100m);
        await _database.RemoveCardTerminalAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var response = await PostAsync(client, ExternalCardPath(terminalId, billId), cookie,
            new { Amount = 100m, SlipNumber = "POS-0042", IdempotencyKey = "ext-forbidden-1" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0L, await _database.PaymentCountAsync(billId));
    }

    [Fact]
    public async Task AnAmountOverTheRemainingIsRefusedWithAnAuditEventAndAnInvalidAmountIsARequestError()
    {
        var (terminalId, cookie, billId) = await ExternalCardSetupAsync("ext-over", payable: 50m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var over = await PostAsync(client, ExternalCardPath(terminalId, billId), cookie,
            new { Amount = 51m, SlipNumber = "POS-701", IdempotencyKey = "ext-over-1" });
        var zero = await PostAsync(client, ExternalCardPath(terminalId, billId), cookie,
            new { Amount = 0m, SlipNumber = "POS-702", IdempotencyKey = "ext-over-2" });
        var noKey = await PostAsync(client, ExternalCardPath(terminalId, billId), cookie,
            new { Amount = 10m, SlipNumber = "POS-703", IdempotencyKey = " " });

        Assert.Equal(HttpStatusCode.Conflict, over.StatusCode);
        Assert.Equal("TENDER_OVER_ALLOCATION", await ErrorCodeAsync(over));
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noKey.StatusCode);
        Assert.Equal(1L, await ScalarAsync<long>(
            "SELECT count(*) FROM audit.audit_events WHERE event_name = 'payment.tender-rejected' AND after_state_json->>'method' = 'ExternalCard'"));
        Assert.Equal(0L, await _database.PaymentCountAsync(billId));
    }

    [Fact]
    public async Task ACancelledBillTakesNoExternalCardPayment()
    {
        var (terminalId, cookie, billId) = await ExternalCardSetupAsync("ext-cancelled");
        await _database.SetBillStatusAsync(billId, "Cancelled");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var response = await PostAsync(client, ExternalCardPath(terminalId, billId), cookie,
            new { Amount = 60m, SlipNumber = "POS-0101", IdempotencyKey = "ext-cancelled-1" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("TENDER_BILL_NOT_PAYABLE", await ErrorCodeAsync(response));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM payments.external_card_receipts"));
        Assert.Equal(0L, await _database.PaymentCountAsync(billId));
    }

    [Fact]
    public async Task AnUnresolvedCardPaymentOnTheBillBlocksTheExternalCardPayment()
    {
        var (terminalId, cookie, billId) = await ExternalCardSetupAsync("ext-unsettled");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        await PostAsync(client, TendersPath(terminalId, billId), cookie, new { Method = "Eft", Amount = 10m, IdempotencyKey = "ext-unsettled-eft" });
        await using (var unsettle = _database.DataSource.CreateCommand(
            "UPDATE payments.payments SET status = 'Unknown', approved_amount = NULL, approved_at = NULL WHERE bill_id = @bill;"))
        {
            unsettle.Parameters.AddWithValue("bill", billId);
            await unsettle.ExecuteNonQueryAsync();
        }

        var response = await PostAsync(client, ExternalCardPath(terminalId, billId), cookie,
            new { Amount = 10m, SlipNumber = "POS-801", IdempotencyKey = "ext-unsettled-1" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("TENDER_UNSETTLED_PAYMENT_EXISTS", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task AReceiptRowCannotCarryASlipTheServerWouldRefuseOrAnAmountThatIsNotPositive()
    {
        var (terminalId, cookie, billId) = await ExternalCardSetupAsync("ext-constraints");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        await PostAsync(client, ExternalCardPath(terminalId, billId), cookie,
            new { Amount = 10m, SlipNumber = "POS-9001", IdempotencyKey = "ext-constraints-1" });

        async Task<string> TryUpdateAsync(string set)
        {
            await using var command = _database.DataSource.CreateCommand($"UPDATE payments.external_card_receipts SET {set};");
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            return error.ConstraintName!;
        }

        Assert.Equal("ck_external_card_receipts_slip", await TryUpdateAsync("slip_number = 'abc'"));
        Assert.Equal("ck_external_card_receipts_slip", await TryUpdateAsync("slip_number = repeat('A', 33)"));
        Assert.Equal("ck_external_card_receipts_amount", await TryUpdateAsync("amount = 0"));

        // The database alone also refuses the same slip twice on one bill, whatever the application checked first.
        await using var twice = _database.DataSource.CreateCommand(
            """
            SET session_replication_role = replica;
            INSERT INTO payments.external_card_receipts (payment_id, bill_id, terminal_id, slip_number, amount, recorded_by)
            SELECT gen_random_uuid(), bill_id, terminal_id, slip_number, amount, recorded_by FROM payments.external_card_receipts;
            """);
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => twice.ExecuteNonQueryAsync());
        Assert.Equal("ux_external_card_receipts_bill_slip", duplicate.ConstraintName);
    }
}
