using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Orders.OrderAggregate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.PaymentTender.Tests;

/// <summary>
/// Real-HTTP tests for the split-payment tender surface (V13-PUI-001)
/// against the actual DualScreenApplication.Build host and a real Postgres
/// database with every migration applied. Cash's own dedicated
/// /cash-sessions/{id}/cash-tender endpoint (V13-CSH-004) is exercised by
/// its own test project (CashSessionHttpTests) — this project covers only
/// the generic (BankCard/Eft) tender endpoint this task adds.
/// </summary>
[Collection("PaymentTender HTTP")]
public sealed class PaymentTenderHttpTests : IAsyncLifetime
{
    private readonly PaymentTenderHttpTestDatabase _database = new();
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"alkaros-pui001-webroot-{Guid.NewGuid():N}");

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
    public async Task EftTenderWithinRemainingIsApprovedAndReflectedInTheSummary()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "pui001-eft-approve");
        var billId = await _database.SeedBillAsync(payable: 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var response = await PostAsync(client, TendersPath(terminalId, billId), cookie,
            new { Method = "Eft", Amount = 60m, IdempotencyKey = "eft-1" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Approved", body.GetProperty("outcome").GetString());
        Assert.Equal(60m, body.GetProperty("approvedAmount").GetDecimal());

        var summary = await GetAsync(client, TendersPath(terminalId, billId), cookie);
        var summaryBody = await summary.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(100m, summaryBody.GetProperty("payableAmount").GetDecimal());
        Assert.Equal(60m, summaryBody.GetProperty("allocatedTotal").GetDecimal());
        Assert.Equal(40m, summaryBody.GetProperty("remainingAmount").GetDecimal());
        Assert.Single(summaryBody.GetProperty("allocations").EnumerateArray());
    }

    [Fact]
    public async Task EftTenderOverTheRemainingAmountIsRejectedAsAConflict()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "pui001-eft-over");
        var billId = await _database.SeedBillAsync(payable: 50m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var response = await PostAsync(client, TendersPath(terminalId, billId), cookie,
            new { Method = "Eft", Amount = 51m, IdempotencyKey = "eft-over-1" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var summary = await GetAsync(client, TendersPath(terminalId, billId), cookie);
        var summaryBody = await summary.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0m, summaryBody.GetProperty("allocatedTotal").GetDecimal());
    }

    [Fact]
    public async Task EftReplayOfTheSameIdempotencyKeyReturnsTheSameOutcomeWithoutASecondAllocation()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "pui001-eft-replay");
        var billId = await _database.SeedBillAsync(payable: 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        await PostAsync(client, TendersPath(terminalId, billId), cookie,
            new { Method = "Eft", Amount = 30m, IdempotencyKey = "eft-replay-1" });

        var replay = await PostAsync(client, TendersPath(terminalId, billId), cookie,
            new { Method = "Eft", Amount = 30m, IdempotencyKey = "eft-replay-1" });

        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var summary = await GetAsync(client, TendersPath(terminalId, billId), cookie);
        var summaryBody = await summary.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(30m, summaryBody.GetProperty("allocatedTotal").GetDecimal());
        Assert.Single(summaryBody.GetProperty("allocations").EnumerateArray());
    }

    [Fact]
    public async Task BankCardTenderAlwaysComesBackAsRequiresReconciliationNeverAFakeApprovalOrDecline()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "pui001-bankcard");
        var billId = await _database.SeedBillAsync(payable: 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var response = await PostAsync(client, TendersPath(terminalId, billId), cookie,
            new { Method = "BankCard", Amount = 40m, IdempotencyKey = "bankcard-1" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("RequiresReconciliation", body.GetProperty("outcome").GetString());
        Assert.False(body.TryGetProperty("approvedAmount", out var approved) && approved.ValueKind != JsonValueKind.Null);

        // No allocation is ever persisted for a placeholder BankCard result —
        // the summary must not silently show it as paid.
        var summary = await GetAsync(client, TendersPath(terminalId, billId), cookie);
        var summaryBody = await summary.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0m, summaryBody.GetProperty("allocatedTotal").GetDecimal());
        Assert.Equal(100m, summaryBody.GetProperty("remainingAmount").GetDecimal());
    }

    [Fact]
    public async Task BankCardRequiresReconciliationIsPersistedAndSurvivesAFreshClientLoad()
    {
        // V1-RMD-258: the Faz 2 independent audit found the "don't retry
        // into a duplicate charge" lock lived only in the browser's own JS
        // memory — a page reload silently dropped it. This proves the real
        // fix: a BRAND NEW HttpClient with no prior in-memory state (the
        // server-side equivalent of a page reload) still sees the
        // unresolved Payment via GET, because CardSettlementOrchestrator
        // now actually persists the RequiresReconciliation attempt.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "rmd258-bankcard-persist");
        var billId = await _database.SeedBillAsync(payable: 100m);
        await using var app = await StartAsync();
        using var firstClient = CreateClient(app);

        var response = await PostAsync(firstClient, TendersPath(terminalId, billId), cookie,
            new { Method = "BankCard", Amount = 40m, IdempotencyKey = "rmd258-bankcard-1" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var reloadedClient = CreateClient(app);
        var summary = await GetAsync(reloadedClient, TendersPath(terminalId, billId), cookie);
        var summaryBody = await summary.Content.ReadFromJsonAsync<JsonElement>();

        var unsettled = summaryBody.GetProperty("unsettledPayment");
        Assert.NotEqual(JsonValueKind.Null, unsettled.ValueKind);
        Assert.Equal("Unknown", unsettled.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ANewBankCardAttemptIsRejectedWhileAPriorOneIsUnresolved()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "rmd258-bankcard-block-bc");
        var billId = await _database.SeedBillAsync(payable: 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var first = await PostAsync(client, TendersPath(terminalId, billId), cookie,
            new { Method = "BankCard", Amount = 40m, IdempotencyKey = "rmd258-bankcard-block-1" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAsync(client, TendersPath(terminalId, billId), cookie,
            new { Method = "BankCard", Amount = 40m, IdempotencyKey = "rmd258-bankcard-block-2" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("TENDER_UNSETTLED_PAYMENT_EXISTS", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task AnEftAttemptIsRejectedWhileAPriorBankCardAttemptIsUnresolved()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "rmd258-bankcard-block-eft");
        var billId = await _database.SeedBillAsync(payable: 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var first = await PostAsync(client, TendersPath(terminalId, billId), cookie,
            new { Method = "BankCard", Amount = 40m, IdempotencyKey = "rmd258-bankcard-block-eft-1" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAsync(client, TendersPath(terminalId, billId), cookie,
            new { Method = "Eft", Amount = 40m, IdempotencyKey = "rmd258-eft-blocked-1" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("TENDER_UNSETTLED_PAYMENT_EXISTS", body.GetProperty("error").GetProperty("code").GetString());

        // Nothing was allocated by the rejected attempt.
        var summary = await GetAsync(client, TendersPath(terminalId, billId), cookie);
        var summaryBody = await summary.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0m, summaryBody.GetProperty("allocatedTotal").GetDecimal());
    }

    [Fact]
    public async Task SummaryWithNoUnsettledPaymentReportsUnsettledPaymentAsNull()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "rmd258-no-unsettled");
        var billId = await _database.SeedBillAsync(payable: 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var summary = await GetAsync(client, TendersPath(terminalId, billId), cookie);
        var summaryBody = await summary.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(JsonValueKind.Null, summaryBody.GetProperty("unsettledPayment").ValueKind);
    }

    [Fact]
    public async Task ConcurrentEftTendersThatTogetherOverAllocateGiveTheLoserAProperTurkish409NeverARaw500()
    {
        // V1-RMD-258: the Faz 2 independent audit found EftTenderHandler's
        // own pre-check reads through an unlocked connection, so two
        // concurrent tenders can both pass it; the REAL enforcement is the
        // per-bill advisory lock inside PostgresPaymentAllocationRepository
        // .AllocateAsync, which correctly throws OverAllocationException for
        // the loser — but that exception was previously uncaught, surfacing
        // as a raw, un-Turkish 500. This drives two REAL concurrent HTTP
        // requests (not sequential) against a bill that can only fit one of
        // them, and asserts the loser gets the same Turkish 409 contract a
        // client-side-detected over-tender gets.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "rmd258-eft-concurrent");
        var billId = await _database.SeedBillAsync(payable: 100m);
        await using var app = await StartAsync();
        using var clientA = CreateClient(app);
        using var clientB = CreateClient(app);

        var taskA = PostAsync(clientA, TendersPath(terminalId, billId), cookie,
            new { Method = "Eft", Amount = 60m, IdempotencyKey = "rmd258-eft-race-a" });
        var taskB = PostAsync(clientB, TendersPath(terminalId, billId), cookie,
            new { Method = "Eft", Amount = 60m, IdempotencyKey = "rmd258-eft-race-b" });
        var responses = await Task.WhenAll(taskA, taskB);

        var statusCodes = responses.Select(r => r.StatusCode).OrderBy(s => s).ToArray();
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], statusCodes);

        var loser = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);
        var loserBody = await loser.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("TENDER_OVER_ALLOCATION", loserBody.GetProperty("error").GetProperty("code").GetString());
        Assert.Contains("₺", loserBody.GetProperty("error").GetProperty("message").GetString());

        var summary = await GetAsync(clientA, TendersPath(terminalId, billId), cookie);
        var summaryBody = await summary.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(60m, summaryBody.GetProperty("allocatedTotal").GetDecimal());
    }

    [Fact]
    public async Task MealCardTenderIsRejectedAsNotRegisteredNeverSilentlyAccepted()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "pui001-mealcard");
        var billId = await _database.SeedBillAsync(payable: 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var response = await PostAsync(client, TendersPath(terminalId, billId), cookie,
            new { Method = "MealCard", Amount = 10m, IdempotencyKey = "mealcard-1" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("TENDER_METHOD_NOT_REGISTERED", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task CustomerAccountTenderIsRejectedAsVersionNotEnabled()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "pui001-customeraccount");
        var billId = await _database.SeedBillAsync(payable: 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var response = await PostAsync(client, TendersPath(terminalId, billId), cookie,
            new { Method = "CustomerAccount", Amount = 10m, IdempotencyKey = "ca-1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("TENDER_VERSION_NOT_ENABLED", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task CashMethodIsRejectedFromThisEndpointAndPointsAtTheCashSessionEndpointInstead()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "pui001-cash-rejected");
        var billId = await _database.SeedBillAsync(payable: 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var response = await PostAsync(client, TendersPath(terminalId, billId), cookie,
            new { Method = "Cash", Amount = 10m, IdempotencyKey = "cash-1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("USE_CASH_TENDER_ENDPOINT", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task EftOnlyTenderFullyClosingTheBillReflectsZeroRemainingAmount()
    {
        // V13-PUI-004: the split-payment screen's own "paid" phase switches
        // purely off a server-read remainingAmount of (approximately) zero,
        // never client-side arithmetic. Prove an EFT-only bill (no Cash, no
        // BankCard involved at all) genuinely reaches that server-confirmed
        // state, matching the manual scenario Semih can verify by hand: pay
        // a bill entirely via EFT and see it reach "Ödendi" (Paid).
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "pui004-eft-full");
        var billId = await _database.SeedBillAsync(payable: 75m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var first = await PostAsync(client, TendersPath(terminalId, billId), cookie,
            new { Method = "Eft", Amount = 50m, IdempotencyKey = "eft-full-1" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAsync(client, TendersPath(terminalId, billId), cookie,
            new { Method = "Eft", Amount = 25m, IdempotencyKey = "eft-full-2" });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var summary = await GetAsync(client, TendersPath(terminalId, billId), cookie);
        var summaryBody = await summary.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(75m, summaryBody.GetProperty("allocatedTotal").GetDecimal());
        Assert.Equal(0m, summaryBody.GetProperty("remainingAmount").GetDecimal());
        Assert.Equal(2, summaryBody.GetProperty("allocations").GetArrayLength());
    }

    [Fact]
    public async Task MixedEftAndBankCardOnlyReflectsTheEftAllocationServerSideNeverClientArithmetic()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "pui001-mixed");
        var billId = await _database.SeedBillAsync(payable: 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        await PostAsync(client, TendersPath(terminalId, billId), cookie,
            new { Method = "Eft", Amount = 40m, IdempotencyKey = "mixed-eft-1" });
        await PostAsync(client, TendersPath(terminalId, billId), cookie,
            new { Method = "BankCard", Amount = 60m, IdempotencyKey = "mixed-bankcard-1" });

        var summary = await GetAsync(client, TendersPath(terminalId, billId), cookie);
        var summaryBody = await summary.Content.ReadFromJsonAsync<JsonElement>();
        // The bill is NOT fully closed: only the Eft allocation is real.
        // A client that summed "40 Eft + 60 BankCard = 100" locally would
        // wrongly believe the bill is fully paid.
        Assert.Equal(40m, summaryBody.GetProperty("allocatedTotal").GetDecimal());
        Assert.Equal(60m, summaryBody.GetProperty("remainingAmount").GetDecimal());
    }

    [Fact]
    public async Task UnknownMethodNameIsRejectedWithoutReachingTheRouter()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "pui001-unknown-method");
        var billId = await _database.SeedBillAsync(payable: 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var response = await PostAsync(client, TendersPath(terminalId, billId), cookie,
            new { Method = "Bitcoin", Amount = 10m, IdempotencyKey = "unknown-1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("TENDER_METHOD_UNKNOWN", body.GetProperty("error").GetProperty("code").GetString());
    }

    // V1-RMD-264: manager-only manual resolution of an unconfirmed card payment.
    private static async Task<Guid> CreateUnsettledPaymentAsync(HttpClient client, Guid terminalId, Guid billId, string cookie, string key)
    {
        await PostAsync(client, TendersPath(terminalId, billId), cookie,
            new { Method = "BankCard", Amount = 40m, IdempotencyKey = key });
        var summary = await GetAsync(client, TendersPath(terminalId, billId), cookie);
        var body = await summary.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("unsettledPayment").GetProperty("paymentId").GetGuid();
    }

    private static string NotChargedPath(Guid terminalId, Guid billId, Guid paymentId)
        => $"{TendersPath(terminalId, billId)}unsettled/{paymentId:D}/not-charged";

    [Fact]
    public async Task AManagerCanMarkAnUnconfirmedCardPaymentNotChargedWhichUnlocksTheBillAndIsAudited()
    {
        var terminalId = Guid.NewGuid();
        var cashier = await _database.SeedCashierSessionAsync(terminalId, "rmd264-cashier-ok");
        var billId = await _database.SeedBillAsync(payable: 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var paymentId = await CreateUnsettledPaymentAsync(client, terminalId, billId, cashier, "rmd264-ok-1");
        var managerTerminal = Guid.NewGuid();
        var manager = await _database.SeedManagerSessionAsync(managerTerminal, "rmd264-manager-ok");

        var response = await PostAsync(client, NotChargedPath(managerTerminal, billId, paymentId), manager,
            new { Reason = "Müşterinin kartı çekilmedi, slip yok." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await GetAsync(client, TendersPath(managerTerminal, billId), manager);
        var summaryBody = await summary.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, summaryBody.GetProperty("unsettledPayment").ValueKind);
        Assert.Equal(0m, summaryBody.GetProperty("allocatedTotal").GetDecimal());
        Assert.Equal("Declined", await _database.PaymentStatusAsync(paymentId));
        Assert.Equal(1, await _database.AuditEventCountAsync("payment.manual-resolution.not-charged", paymentId));

        // The lock is really gone: a new tender is accepted again.
        var retry = await PostAsync(client, TendersPath(terminalId, billId), cashier,
            new { Method = "Eft", Amount = 25m, IdempotencyKey = "rmd264-ok-retry" });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }

    [Fact]
    public async Task APlainCashierCannotResolveAnUnconfirmedCardPayment()
    {
        var terminalId = Guid.NewGuid();
        var cashier = await _database.SeedCashierSessionAsync(terminalId, "rmd264-cashier-denied");
        var billId = await _database.SeedBillAsync(payable: 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var paymentId = await CreateUnsettledPaymentAsync(client, terminalId, billId, cashier, "rmd264-denied-1");

        var response = await PostAsync(client, NotChargedPath(terminalId, billId, paymentId), cashier,
            new { Reason = "Kasiyer kendi başına çözemez." });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Unknown", await _database.PaymentStatusAsync(paymentId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task AMissingReasonIsRejectedAndThePaymentStaysUnresolved(string? reason)
    {
        var terminalId = Guid.NewGuid();
        var cashier = await _database.SeedCashierSessionAsync(terminalId, "rmd264-cashier-reason");
        var billId = await _database.SeedBillAsync(payable: 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var paymentId = await CreateUnsettledPaymentAsync(client, terminalId, billId, cashier, "rmd264-reason-1");
        var managerTerminal = Guid.NewGuid();
        var manager = await _database.SeedManagerSessionAsync(managerTerminal, "rmd264-manager-reason-" + (reason is null ? "n" : "w"));

        var response = await PostAsync(client, NotChargedPath(managerTerminal, billId, paymentId), manager, new { Reason = reason });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("RESOLUTION_REASON_INVALID", body.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("Unknown", await _database.PaymentStatusAsync(paymentId));
    }

    [Fact]
    public async Task ResolvingTheSamePaymentTwiceIsAConflictAndWritesOnlyOneAuditEvent()
    {
        var terminalId = Guid.NewGuid();
        var cashier = await _database.SeedCashierSessionAsync(terminalId, "rmd264-cashier-twice");
        var billId = await _database.SeedBillAsync(payable: 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var paymentId = await CreateUnsettledPaymentAsync(client, terminalId, billId, cashier, "rmd264-twice-1");
        var managerTerminal = Guid.NewGuid();
        var manager = await _database.SeedManagerSessionAsync(managerTerminal, "rmd264-manager-twice");
        var path = NotChargedPath(managerTerminal, billId, paymentId);

        var first = await PostAsync(client, path, manager, new { Reason = "İlk çözüm." });
        var second = await PostAsync(client, path, manager, new { Reason = "İkinci çözüm." });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PAYMENT_NOT_RESOLVABLE", body.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(1, await _database.AuditEventCountAsync("payment.manual-resolution.not-charged", paymentId));
    }

    [Fact]
    public async Task APaymentOfAnotherBillCannotBeResolvedThroughThisBillsPath()
    {
        var terminalId = Guid.NewGuid();
        var cashier = await _database.SeedCashierSessionAsync(terminalId, "rmd264-cashier-other");
        var billId = await _database.SeedBillAsync(payable: 100m);
        var otherBillId = await _database.SeedBillAsync(payable: 100m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var paymentId = await CreateUnsettledPaymentAsync(client, terminalId, billId, cashier, "rmd264-other-1");
        var managerTerminal = Guid.NewGuid();
        var manager = await _database.SeedManagerSessionAsync(managerTerminal, "rmd264-manager-other");

        var response = await PostAsync(client, NotChargedPath(managerTerminal, otherBillId, paymentId), manager,
            new { Reason = "Yanlış hesap." });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Unknown", await _database.PaymentStatusAsync(paymentId));
    }

    [Fact]
    public async Task SummaryOnANonExistentBillIsNotFound()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "pui001-missing-bill");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var response = await GetAsync(client, TendersPath(terminalId, Guid.NewGuid()), cookie);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static string TendersPath(Guid terminalId, Guid billId)
        => $"/api/v1/terminals/{terminalId:D}/billing/bills/{billId:D}/tenders/";

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

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, string? cookie, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        AddTrustedForwarding(request);
        if (cookie is not null)
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string path, string? cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        AddTrustedForwarding(request);
        if (cookie is not null)
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return await client.SendAsync(request);
    }

    private static void AddTrustedForwarding(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.10");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
    }
}

internal sealed class PaymentTenderHttpTestDatabase
{
    private readonly string _databaseName = "alkaros_pui001_" + Guid.NewGuid().ToString("N")[..8];
    private NpgsqlDataSource? _dataSource;

    public string ConnectionString { get; private set; } = string.Empty;

    public NpgsqlDataSource DataSource => _dataSource ?? throw new InvalidOperationException("Not initialized.");

    public async Task InitializeAsync()
    {
        var maintenanceConnection = new NpgsqlConnectionStringBuilder
        {
            Host = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_HOST") ?? "localhost",
            Port = int.TryParse(Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_PORT"), out var port) ? port : 5432,
            Username = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_USER") ?? "postgres",
            Password = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_PASSWORD"),
            Database = "postgres",
        }.ConnectionString;
        await using (var maintenance = NpgsqlDataSource.Create(maintenanceConnection))
        {
            await ExecuteAsync(maintenance, $"DROP DATABASE IF EXISTS {_databaseName} WITH (FORCE);");
            await ExecuteAsync(maintenance, $"CREATE DATABASE {_databaseName};");
        }

        ConnectionString = new NpgsqlConnectionStringBuilder(maintenanceConnection) { Database = _databaseName }.ConnectionString;
        _dataSource = NpgsqlDataSource.Create(ConnectionString);
        await ApplyMigrationsAsync();
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
            _dataSource = null;
        }
        if (string.IsNullOrWhiteSpace(ConnectionString))
            return;
        var maintenanceConnection = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = "postgres" }.ConnectionString;
        await using var maintenance = NpgsqlDataSource.Create(maintenanceConnection);
        await ExecuteAsync(maintenance, $"DROP DATABASE IF EXISTS {_databaseName} WITH (FORCE);");
    }

    /// <summary>Seeds a real user + a real cashier device session bound to <paramref name="terminalId"/>, and returns the raw Cookie header value.</summary>
    public async Task<string> SeedCashierSessionAsync(Guid terminalId, string rawToken)
    {
        var userId = Guid.NewGuid();
        await using var command = DataSource.CreateCommand(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'x', 'PaymentTender Test Cashier', true);

            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, expires_at)
            VALUES (gen_random_uuid(), @user_id, @device_id, @token_hash, now() + interval '1 hour');
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("username", "pui001-cashier-" + userId.ToString("N")[..8]);
        command.Parameters.AddWithValue("device_id", $"cashier:{terminalId:D}");
        command.Parameters.AddWithValue("token_hash", DeviceSessionToken.Hash(rawToken));
        await command.ExecuteNonQueryAsync();
        return $"alkaros.cashier={rawToken}";
    }

    /// <summary>Seeds a user holding reconciliation.manage with a cashier device session on <paramref name="terminalId"/>.</summary>
    public async Task<string> SeedManagerSessionAsync(Guid terminalId, string rawToken)
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        await using var command = DataSource.CreateCommand(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'x', 'PaymentTender Test Manager', true);
            INSERT INTO identity.roles (role_id, code, name) VALUES (@role_id, @role_code, 'PaymentTender Test Manager Role');
            INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
            SELECT gen_random_uuid(), @role_id, permission_id FROM identity.permissions WHERE code = 'reconciliation.manage';
            INSERT INTO identity.user_roles (user_role_id, user_id, role_id) VALUES (gen_random_uuid(), @user_id, @role_id);
            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, expires_at)
            VALUES (gen_random_uuid(), @user_id, @device_id, @token_hash, now() + interval '1 hour');
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("role_id", roleId);
        command.Parameters.AddWithValue("role_code", "pui264-manager-" + roleId.ToString("N")[..8]);
        command.Parameters.AddWithValue("username", "pui264-manager-" + userId.ToString("N")[..8]);
        command.Parameters.AddWithValue("device_id", $"cashier:{terminalId:D}");
        command.Parameters.AddWithValue("token_hash", DeviceSessionToken.Hash(rawToken));
        await command.ExecuteNonQueryAsync();
        return $"alkaros.cashier={rawToken}";
    }

    public async Task<string> PaymentStatusAsync(Guid paymentId)
    {
        await using var command = DataSource.CreateCommand("SELECT status FROM payments.payments WHERE payment_id = @id;");
        command.Parameters.AddWithValue("id", paymentId);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    public async Task<long> AuditEventCountAsync(string eventName, Guid aggregateId)
    {
        await using var command = DataSource.CreateCommand(
            "SELECT count(*) FROM audit.audit_events WHERE event_name = @name AND aggregate_id = @id;");
        command.Parameters.AddWithValue("name", eventName);
        command.Parameters.AddWithValue("id", aggregateId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>Seeds a real, payable Bill (catalog product + table + order + bill) for a tender test.</summary>
    public async Task<Guid> SeedBillAsync(decimal payable)
    {
        var productId = Guid.NewGuid();
        await using (var command = DataSource.CreateCommand(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price)
            VALUES (@product_id, @sku, 'Test Item', 1, 1, @price);
            """))
        {
            command.Parameters.AddWithValue("product_id", productId);
            command.Parameters.AddWithValue("sku", "SKU-" + Guid.NewGuid().ToString("N")[..8]);
            command.Parameters.AddWithValue("price", payable);
            await command.ExecuteNonQueryAsync();
        }

        var tableId = Guid.NewGuid();
        await using (var command = DataSource.CreateCommand(
            """
            INSERT INTO table_mgmt.tables (table_id, table_number, capacity, active, current_status)
            VALUES (@table_id, @table_number, 4, true, 'Available');
            """))
        {
            command.Parameters.AddWithValue("table_id", tableId);
            command.Parameters.AddWithValue("table_number", "TBL-" + Guid.NewGuid().ToString("N")[..6]);
            await command.ExecuteNonQueryAsync();
        }

        var orderId = Guid.NewGuid();
        var item = new OrderItem(
            id: Guid.NewGuid(), orderId: orderId, productId: productId, productNameSnapshot: "Test Item",
            quantity: 1, unitPrice: payable, taxRate: 0m);
        var order = new Order(orderId, OrderSource.Cashier, "ORD-" + Guid.NewGuid().ToString("N")[..8], [item], tableId: tableId);
        var orders = new PostgresOrderRepository(DataSource);
        await orders.AddAsync(order);

        var billId = Guid.NewGuid();
        var billItem = BillItem.FromOrderItem(billId, order.Items[0]);
        var bill = new Bill(
            id: billId,
            billNumber: "BILL-" + Guid.NewGuid().ToString("N")[..8],
            items: [billItem],
            tableId: tableId,
            orderId: order.Id,
            status: BillState.Open,
            currencyCode: "TRY");
        var bills = new PostgresBillRepository(DataSource);
        await bills.AddAsync(bill);

        return billId;
    }

    private async Task ApplyMigrationsAsync()
    {
        var root = FindRepositoryRoot();
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(root, "database", "MigrationComposition", "order.json")));
        var migrationRoot = Path.Combine(root, "database", "migrations");
        foreach (var migration in manifest.RootElement.GetProperty("migrations").EnumerateArray())
        {
            var id = migration.GetProperty("id").GetString() ?? throw new InvalidOperationException("Migration ID is missing.");
            var files = Directory.GetFiles(migrationRoot, $"{id}-*.up.sql", SearchOption.AllDirectories);
            await ExecuteAsync(DataSource, await File.ReadAllTextAsync(files[0]));
        }
    }

    private static async Task ExecuteAsync(NpgsqlDataSource dataSource, string sql)
    {
        await using var command = dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ALKAROS.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
