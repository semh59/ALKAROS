using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ALKAROS.Billing.Adjustments;
using ALKAROS.Host.DualScreen;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.PaymentTender.Tests;

[Collection("PaymentTender HTTP")]
public sealed class BillReceiptHttpTests : IAsyncLifetime
{
    private readonly PaymentTenderHttpTestDatabase _database = new();
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"alkaros-receipt-webroot-{Guid.NewGuid():N}");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_webRoot);
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><html><body>Test</body></html>");
        await _database.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        if (Directory.Exists(_webRoot))
            Directory.Delete(_webRoot, recursive: true);
    }

    [Fact]
    public async Task TheReceiptListsItemsAdjustmentsTotalsAndPaymentsWithTheirMethod()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "receipt-full");
        var billId = await _database.SeedBillAsync(payable: 100m);
        var actor = await UserIdAsync();
        await new PostgresBillAdjustmentRepository(_database.DataSource).AddAsync(
            BillAdjustment.CreateTip(Guid.NewGuid(), billId, 10m, "Tip", actor, createdBy: actor));
        await _database.SeedCollectedAsync(billId, 60m);
        await _database.SeedCollectedAsync(billId, 20m);
        await _database.SeedCollectedAsync(billId, 25m);
        await _database.SeedCollectedAsync(billId, 5m);
        await MarkCardAsync(await FirstPaymentIdAsync(billId, 5m));
        await MarkCashAsync(await FirstPaymentIdAsync(billId, 60m), actor);
        await MarkAccountAsync(await FirstPaymentIdAsync(billId, 20m), actor);
        await SetApprovedAtAsync(await FirstPaymentIdAsync(billId, 60m), hoursFromNow: 1);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var response = await GetAsync(client, ReceiptPath(terminalId, billId), cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.StartsWith("BILL-", body.GetProperty("billNumber").GetString());
        var item = Assert.Single(body.GetProperty("items").EnumerateArray());
        Assert.Equal("Test Item", item.GetProperty("name").GetString());
        Assert.Equal(100m, item.GetProperty("amount").GetDecimal());
        Assert.Equal(1m, item.GetProperty("quantity").GetDecimal());
        Assert.Equal(100m, item.GetProperty("unitPrice").GetDecimal());
        Assert.Equal("Open", body.GetProperty("status").GetString());
        Assert.Equal("TRY", body.GetProperty("currencyCode").GetString());
        Assert.Equal(0m, body.GetProperty("taxTotal").GetDecimal());
        var adjustment = Assert.Single(body.GetProperty("adjustments").EnumerateArray());
        Assert.Equal("Tip", adjustment.GetProperty("type").GetString());
        Assert.Equal(10m, adjustment.GetProperty("amount").GetDecimal());
        Assert.False(adjustment.GetProperty("isDeduction").GetBoolean());
        Assert.Equal(100m, body.GetProperty("subtotal").GetDecimal());
        Assert.Equal(110m, body.GetProperty("payableAmount").GetDecimal());
        var payments = body.GetProperty("payments").EnumerateArray().ToList();
        Assert.Equal(4, payments.Count);
        Assert.Equal([20m, 25m, 5m, 60m], payments.Select(p => p.GetProperty("amount").GetDecimal()).ToArray());
        Assert.All(payments, p => Assert.NotEqual(JsonValueKind.Null, p.GetProperty("at").ValueKind));
        Assert.Equal(60m, payments.Single(p => p.GetProperty("method").GetString() == "Cash").GetProperty("amount").GetDecimal());
        Assert.Equal(20m, payments.Single(p => p.GetProperty("method").GetString() == "Account").GetProperty("amount").GetDecimal());
        Assert.Equal(25m, payments.Single(p => p.GetProperty("method").GetString() == "Manual").GetProperty("amount").GetDecimal());
        Assert.Equal(5m, payments.Single(p => p.GetProperty("method").GetString() == "Card").GetProperty("amount").GetDecimal());
        Assert.All(payments, p => Assert.Equal("Approved", p.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task EachItemCarriesItsIdAndTheUnitsAlreadyAskedToBeRefundedButNotTheRejectedOnes()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "receipt-refunded");
        var billId = await _database.SeedBillAsync(payable: 100m);
        var actor = await UserIdAsync();
        await _database.SeedCollectedAsync(billId, 100m);
        var paymentId = await FirstPaymentIdAsync(billId, 100m);
        var billItemId = await BillItemIdAsync(billId);
        await AddRefundAsync(billId, paymentId, billItemId, "Completed", actor);
        await AddRefundAsync(billId, paymentId, billItemId, "Rejected", actor);
        await AddRefundAsync(billId, paymentId, billItemId, "Requested", actor);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var response = await GetAsync(client, ReceiptPath(terminalId, billId), cookie);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var item = Assert.Single(body.GetProperty("items").EnumerateArray());
        Assert.Equal(billItemId, item.GetProperty("billItemId").GetGuid());
        Assert.Equal(2m, item.GetProperty("refundedQuantity").GetDecimal());
    }

    [Fact]
    public async Task EachPaymentCarriesWhatWasPaidBackOfIt()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "receipt-paid-back");
        var billId = await _database.SeedBillAsync(payable: 100m);
        var actor = await UserIdAsync();
        await _database.SeedCollectedAsync(billId, 60m);
        await _database.SeedCollectedAsync(billId, 40m);
        var refunded = await FirstPaymentIdAsync(billId, 60m);
        await AddReversalAsync(refunded, 25m, "a");
        await AddReversalAsync(refunded, 10m, "b");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var response = await GetAsync(client, ReceiptPath(terminalId, billId), cookie);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var payments = body.GetProperty("payments").EnumerateArray().ToList();
        Assert.Equal(35m, payments.Single(p => p.GetProperty("amount").GetDecimal() == 60m).GetProperty("refundedAmount").GetDecimal());
        Assert.Equal(0m, payments.Single(p => p.GetProperty("amount").GetDecimal() == 40m).GetProperty("refundedAmount").GetDecimal());
        Assert.NotEqual(Guid.Empty, actor);
    }

    [Fact]
    public async Task ADiscountIsListedAsADeductionAndAnUnapprovedPaymentIsLeftOut()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "receipt-discount");
        var billId = await _database.SeedBillAsync(payable: 100m);
        var actor = await UserIdAsync();
        await new PostgresBillAdjustmentRepository(_database.DataSource).AddAsync(
            BillAdjustment.CreateDiscountAmount(Guid.NewGuid(), billId, 15m, 0m, "Discount", actor, createdBy: actor));
        await new ALKAROS.Payments.PaymentAggregate.PostgresPaymentRepository(_database.DataSource).AddAsync(
            new ALKAROS.Payments.PaymentAggregate.Payment(Guid.NewGuid(), billId, 40m));
        await _database.SetBillStatusAsync(billId, "Cancelled");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var body = await (await GetAsync(client, ReceiptPath(terminalId, billId), cookie)).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Cancelled", body.GetProperty("status").GetString());
        var adjustment = Assert.Single(body.GetProperty("adjustments").EnumerateArray());
        Assert.True(adjustment.GetProperty("isDeduction").GetBoolean());
        Assert.Equal(85m, body.GetProperty("payableAmount").GetDecimal());
        Assert.Empty(body.GetProperty("payments").EnumerateArray());
    }

    [Fact]
    public async Task AnUnknownBillIsNotFoundAndAnAnonymousCallIsRefused()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "receipt-missing");
        var billId = await _database.SeedBillAsync(payable: 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var missing = await GetAsync(client, ReceiptPath(terminalId, Guid.NewGuid()), cookie);
        var anonymous = await GetAsync(client, ReceiptPath(terminalId, billId), cookie: null);

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task WithoutPaymentsTakeTheReceiptAndThePaymentSummaryAreForbidden()
    {
        var terminalId = Guid.NewGuid();
        var waiter = await _database.SeedCashierSessionAsync(terminalId, "receipt-no-payments", takesPayments: false);
        var billId = await _database.SeedBillAsync(payable: 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var receipt = await GetAsync(client, ReceiptPath(terminalId, billId), waiter);
        var summary = await GetAsync(
            client, $"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/tenders/", waiter);

        Assert.Equal(HttpStatusCode.Forbidden, receipt.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, summary.StatusCode);
    }

    private async Task<Guid> UserIdAsync()
    {
        await using var command = _database.DataSource.CreateCommand("SELECT user_id FROM identity.users ORDER BY username LIMIT 1;");
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private async Task<Guid> FirstPaymentIdAsync(Guid billId, decimal amount)
    {
        await using var command = _database.DataSource.CreateCommand(
            "SELECT payment_id FROM payments.payments WHERE bill_id = @b AND approved_amount = @a LIMIT 1;");
        command.Parameters.AddWithValue("b", billId);
        command.Parameters.AddWithValue("a", amount);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private async Task AddReversalAsync(Guid paymentId, decimal amount, string key)
    {
        await using var command = _database.DataSource.CreateCommand(
            """
            INSERT INTO payments.payment_reversals
                (reversal_id, payment_id, reversal_type, amount, reason, refunded_by, idempotency_key, occurred_at)
            VALUES (gen_random_uuid(), @p, 'PartialRefund', @a, 'Test', gen_random_uuid(), @k, now());
            """);
        command.Parameters.AddWithValue("p", paymentId);
        command.Parameters.AddWithValue("a", amount);
        command.Parameters.AddWithValue("k", "rcpt-" + key + "-" + Guid.NewGuid().ToString("N"));
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid> BillItemIdAsync(Guid billId)
    {
        await using var command = _database.DataSource.CreateCommand(
            "SELECT bill_item_id FROM billing.bill_items WHERE bill_id = @b LIMIT 1;");
        command.Parameters.AddWithValue("b", billId);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private async Task AddRefundAsync(Guid billId, Guid paymentId, Guid billItemId, string status, Guid actor)
    {
        var requestId = Guid.NewGuid();
        await using var command = _database.DataSource.CreateCommand(
            """
            INSERT INTO payments.refund_requests
                (refund_request_id, bill_id, payment_id, amount, method, reason, status, requested_by, requested_at)
            VALUES (@r, @b, @p, 10, 'Manual', 'Test', @s, @u, now());
            INSERT INTO payments.refund_request_items (refund_request_id, bill_item_id, quantity, restore_stock)
            VALUES (@r, @i, 1, false);
            """);
        command.Parameters.AddWithValue("r", requestId);
        command.Parameters.AddWithValue("b", billId);
        command.Parameters.AddWithValue("p", paymentId);
        command.Parameters.AddWithValue("s", status);
        command.Parameters.AddWithValue("u", actor);
        command.Parameters.AddWithValue("i", billItemId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task MarkCashAsync(Guid paymentId, Guid actor)
    {
        var sessionId = Guid.NewGuid();
        await using var command = _database.DataSource.CreateCommand(
            """
            INSERT INTO cash.cash_sessions (cash_session_id, cashier_user_id, terminal_id, status, opening_balance, opened_at, created_at, updated_at)
            VALUES (@s, @u, gen_random_uuid(), 'Open', 0, now(), now(), now());
            INSERT INTO cash.cash_transactions (cash_transaction_id, cash_session_id, type, direction, amount, related_payment_id, recorded_by, occurred_at)
            VALUES (gen_random_uuid(), @s, 'Sale', 'In', 60, @p, @u, now());
            """);
        command.Parameters.AddWithValue("s", sessionId);
        command.Parameters.AddWithValue("u", actor);
        command.Parameters.AddWithValue("p", paymentId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task SetApprovedAtAsync(Guid paymentId, int hoursFromNow)
    {
        await using var command = _database.DataSource.CreateCommand(
            "UPDATE payments.payments SET approved_at = approved_at + make_interval(hours => @h) WHERE payment_id = @p;");
        command.Parameters.AddWithValue("h", hoursFromNow);
        command.Parameters.AddWithValue("p", paymentId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task MarkCardAsync(Guid paymentId)
    {
        await using var command = _database.DataSource.CreateCommand(
            """
            INSERT INTO payments.card_settlement_attempts
                (card_settlement_attempt_id, idempotency_key, provider_correlation_id, payment_id, bill_id, outcome, approved_amount, allocation_id, fiscal_handoff_queued, created_at)
            SELECT gen_random_uuid(), @key, @key, @p, bill_id, 'Approved', 5, (SELECT payment_allocation_id FROM payments.payment_allocations WHERE payment_id = @p LIMIT 1), true, now() FROM payments.payments WHERE payment_id = @p;
            """);
        command.Parameters.AddWithValue("key", "receipt-card-" + paymentId.ToString("N"));
        command.Parameters.AddWithValue("p", paymentId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task MarkAccountAsync(Guid paymentId, Guid actor)
    {
        await using var command = _database.DataSource.CreateCommand(
            """
            INSERT INTO customer_account.account_transactions
                (id, customer_id, transaction_type, amount, source_reference_type, source_reference_id, created_by, occurred_at)
            VALUES (gen_random_uuid(), gen_random_uuid(), 'Charge', 20, 'Payment', @p, @u, now());
            """);
        command.Parameters.AddWithValue("p", paymentId);
        command.Parameters.AddWithValue("u", actor);
        await command.ExecuteNonQueryAsync();
    }

    private static string ReceiptPath(Guid terminalId, Guid billId)
        => $"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/receipt";

    private async Task<WebApplication> StartAsync()
    {
        var app = DualScreenApplication.Build(new DualScreenOptions(
            _database.ConnectionString,
            _webRoot,
            "http://127.0.0.1:0",
            TrustedProxies: [System.Net.IPAddress.Loopback]));
        await app.StartAsync();
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string path, string? cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.10");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        if (cookie is not null)
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return await client.SendAsync(request);
    }
}
