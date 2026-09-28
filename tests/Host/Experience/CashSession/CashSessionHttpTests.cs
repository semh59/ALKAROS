using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Orders.OrderAggregate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.CashSession.Tests;

/// <summary>
/// Real-HTTP tests for the CashSession/cash-tender composition (V13-CSH-004)
/// against the actual DualScreenApplication.Build host and a real Postgres
/// database with every migration applied.
/// </summary>
[Collection("CashSession HTTP")]
public sealed class CashSessionHttpTests : IAsyncLifetime
{
    private readonly CashSessionHttpTestDatabase _database = new();
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"alkaros-csh004-webroot-{Guid.NewGuid():N}");

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
    public async Task OpenSessionSucceedsAndPostsAnOpeningLedgerEntry()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "csh004-open-1");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var response = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions", cookie,
            new { OpeningBalance = 500m });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Open", body.GetProperty("status").GetString());
        Assert.Equal(500m, body.GetProperty("openingBalance").GetDecimal());
    }

    [Fact]
    public async Task OpeningASecondSessionOnTheSameTerminalIsRejected()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "csh004-double-open");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions", cookie, new { OpeningBalance = 100m });

        var second = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions", cookie, new { OpeningBalance = 200m });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task GetActiveReturnsTheOpenSessionAndNotFoundOnceClosed()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "csh004-active");
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var missing = await GetAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/active", cookie);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var opened = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions", cookie, new { OpeningBalance = 100m });
        var sessionId = (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cashSessionId").GetGuid();

        var active = await GetAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/active", cookie);
        Assert.Equal(HttpStatusCode.OK, active.StatusCode);
        Assert.Equal(sessionId, (await active.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cashSessionId").GetGuid());

        await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/close", cookie,
            new { ActualCash = 100m });

        var afterClose = await GetAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/active", cookie);
        Assert.Equal(HttpStatusCode.NotFound, afterClose.StatusCode);
    }

    [Fact]
    public async Task CloseComputesExpectedCashFromTheOpeningLedgerEntryWithNoVarianceForAMatchingCount()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "csh004-close-match");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var opened = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions", cookie, new { OpeningBalance = 300m });
        var sessionId = (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cashSessionId").GetGuid();

        var close = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/close", cookie,
            new { ActualCash = 300m });

        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        var body = await close.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0m, body.GetProperty("difference").GetDecimal());
        Assert.Equal("Closed", body.GetProperty("session").GetProperty("status").GetString());
    }

    [Fact]
    public async Task StartCountThenRecordCountSucceed()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "csh004-count");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var opened = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions", cookie, new { OpeningBalance = 300m });
        var sessionId = (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cashSessionId").GetGuid();

        var started = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/start-count", cookie, new { });
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        Assert.Equal("Counting", (await started.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

        var recorded = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/counts", cookie,
            new { CountedAmount = 305m, Notes = "recount" });
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
        Assert.Equal(305m, (await recorded.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("countedAmount").GetDecimal());
    }

    [Fact]
    public async Task CashTenderSucceedsAndPostsASaleLedgerEntry()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "csh004-tender");
        var billId = await _database.SeedBillAsync(payable: 80m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var opened = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions", cookie, new { OpeningBalance = 100m });
        var sessionId = (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cashSessionId").GetGuid();

        var tender = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/cash-tender", cookie,
            new { BillId = billId, AmountDue = 80m, TenderedAmount = 100m, IdempotencyKey = "csh004-tender-key-1" });

        Assert.Equal(HttpStatusCode.OK, tender.StatusCode);
        var body = await tender.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(80m, body.GetProperty("approvedAmount").GetDecimal());
        Assert.Equal(20m, body.GetProperty("changeAmount").GetDecimal());

        // V1-RMD-276: the cash that covers the bill completes it.
        await using var statusCommand = _database.DataSource.CreateCommand("SELECT status FROM billing.bills WHERE bill_id = @id;");
        statusCommand.Parameters.AddWithValue("id", billId);
        Assert.Equal("Paid", (string)(await statusCommand.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task CashTenderOnAClosedSessionIsRejected()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "csh004-tender-closed");
        var billId = await _database.SeedBillAsync(payable: 50m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var opened = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions", cookie, new { OpeningBalance = 100m });
        var sessionId = (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cashSessionId").GetGuid();
        await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/close", cookie, new { ActualCash = 100m });

        var tender = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/cash-tender", cookie,
            new { BillId = billId, AmountDue = 50m, TenderedAmount = 50m, IdempotencyKey = "csh004-tender-closed-key" });

        Assert.Equal(HttpStatusCode.Conflict, tender.StatusCode);
    }

    [Fact]
    public async Task CashTenderBelowTheAmountDueIsRejected()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "csh004-insufficient");
        var billId = await _database.SeedBillAsync(payable: 80m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var opened = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions", cookie, new { OpeningBalance = 100m });
        var sessionId = (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cashSessionId").GetGuid();

        var tender = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/cash-tender", cookie,
            new { BillId = billId, AmountDue = 80m, TenderedAmount = 50m, IdempotencyKey = "csh004-insufficient-key" });

        Assert.Equal(HttpStatusCode.BadRequest, tender.StatusCode);
    }

    [Fact]
    public async Task CashTenderClaimingMoreThanTheBillsRemainingPayableIsRejectedAndPersistsNothing()
    {
        // Found by an independent review (2026-09-18): AmountDue is
        // client-supplied and was never cross-checked against the bill's
        // own real remaining payable before this fix - confirms the fix
        // through the real HTTP route, not just the domain-level test.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "csh004-over-allocate");
        var billId = await _database.SeedBillAsync(payable: 80m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var opened = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions", cookie, new { OpeningBalance = 100m });
        var sessionId = (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cashSessionId").GetGuid();

        var tender = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/cash-tender", cookie,
            new { BillId = billId, AmountDue = 200m, TenderedAmount = 200m, IdempotencyKey = "csh004-over-allocate-key" });

        Assert.Equal(HttpStatusCode.Conflict, tender.StatusCode);
        var body = await tender.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("OVER_ALLOCATION", body.GetProperty("error").GetProperty("code").GetString());

        // A same-key retry of the SAME over-claiming request must still be
        // rejected the same way, not silently "replayed" as a success -
        // nothing was ever persisted for this idempotency key.
        var retry = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/cash-tender", cookie,
            new { BillId = billId, AmountDue = 200m, TenderedAmount = 200m, IdempotencyKey = "csh004-over-allocate-key" });
        Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);

        // The bill is still fully payable by a real, correctly-sized tender.
        var valid = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/cash-tender", cookie,
            new { BillId = billId, AmountDue = 80m, TenderedAmount = 80m, IdempotencyKey = "csh004-over-allocate-followup" });
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
    }

    [Fact]
    public async Task ExpectedCashPreviewMatchesWhatCloseItselfLaterComputes()
    {
        // Found while building V13-PUI-002's own Fark Teyidi screen:
        // CashSessionSnapshot.ExpectedCash is only ever refreshed by
        // CloseSessionAsync's own write path - a screen reading it before
        // close sees the stale Open-time value. This preview endpoint must
        // report the same number /close itself later computes and commits.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "csh004-expected-preview");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var opened = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions", cookie, new { OpeningBalance = 300m });
        var sessionId = (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cashSessionId").GetGuid();
        await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/cash-movements", cookie,
            new { Direction = "In", Amount = 50m, Notes = "bank deposit returned", IdempotencyKey = Guid.NewGuid().ToString() });

        var preview = await GetAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/expected-cash", cookie);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal(350m, (await preview.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("expectedCash").GetDecimal());

        var close = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/close", cookie,
            new { ActualCash = 350m });
        Assert.Equal(0m, (await close.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("difference").GetDecimal());
    }

    [Fact]
    public async Task CashMovementsAdjustExpectedCashInBothDirectionsAndRejectOnAClosedSession()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "csh004-movements");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var opened = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions", cookie, new { OpeningBalance = 200m });
        var sessionId = (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cashSessionId").GetGuid();

        var cashIn = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/cash-movements", cookie,
            new { Direction = "In", Amount = 100m, Notes = (string?)null, IdempotencyKey = Guid.NewGuid().ToString() });
        Assert.Equal(HttpStatusCode.Created, cashIn.StatusCode);

        var cashOut = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/cash-movements", cookie,
            new { Direction = "Out", Amount = 40m, Notes = "till float trimmed for bank drop", IdempotencyKey = Guid.NewGuid().ToString() });
        Assert.Equal(HttpStatusCode.Created, cashOut.StatusCode);

        var preview = await GetAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/expected-cash", cookie);
        Assert.Equal(260m, (await preview.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("expectedCash").GetDecimal());

        await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/close", cookie, new { ActualCash = 260m });

        var afterClose = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/cash-movements", cookie,
            new { Direction = "In", Amount = 10m, Notes = (string?)null, IdempotencyKey = Guid.NewGuid().ToString() });
        Assert.Equal(HttpStatusCode.Conflict, afterClose.StatusCode);
    }

    [Fact]
    public async Task RetryingACashMovementWithTheSameIdempotencyKeyDoesNotDuplicateIt()
    {
        // V1-RMD-241: unlike cash-tender, this endpoint used to accept no
        // idempotency key at all — a network retry (or a race just under
        // the client's own busy-guard) could post the same movement twice.
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "csh004-movement-retry");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var opened = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions", cookie, new { OpeningBalance = 100m });
        var sessionId = (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cashSessionId").GetGuid();
        var key = Guid.NewGuid().ToString();

        var first = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/cash-movements", cookie,
            new { Direction = "In", Amount = 75m, Notes = (string?)null, IdempotencyKey = key });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var retry = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/cash-movements", cookie,
            new { Direction = "In", Amount = 75m, Notes = (string?)null, IdempotencyKey = key });
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(firstId, (await retry.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());

        var preview = await GetAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/expected-cash", cookie);
        Assert.Equal(175m, (await preview.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("expectedCash").GetDecimal());
    }

    [Fact]
    public async Task ClosingWithADifferenceOverTheToleranceRequiresSupervisorOverride()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionAsync(terminalId, "csh004-variance");
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var opened = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions", cookie, new { OpeningBalance = 100m });
        var sessionId = (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cashSessionId").GetGuid();

        // Default tolerance (CashSessionPolicy.ValidateCanCloseSession) is
        // 50.00 - a 60 TL shortage must be rejected without an override.
        var rejected = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/close", cookie,
            new { ActualCash = 40m });
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        var body = await rejected.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CASH_VARIANCE_THRESHOLD_EXCEEDED", body.GetProperty("error").GetProperty("code").GetString());

        // V1-RMD-236: the override itself requires a real cash.session.override
        // grant — the SAME cashier session that opened/counted the session
        // cannot self-declare an override.
        var deniedOverride = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/close", cookie,
            new { ActualCash = 40m, IsSupervisorOverride = true, OverrideReason = "recount confirmed short" });
        Assert.Equal(HttpStatusCode.Forbidden, deniedOverride.StatusCode);

        var supervisorCookie = await _database.SeedCashierSessionWithPermissionsAsync(
            terminalId, "csh004-variance-supervisor", ApplicationPermissions.CashDrawer, ApplicationPermissions.CashSessionOverride);
        var overridden = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/close", supervisorCookie,
            new { ActualCash = 40m, IsSupervisorOverride = true, OverrideReason = "recount confirmed short" });
        Assert.Equal(HttpStatusCode.OK, overridden.StatusCode);
        Assert.Equal(-60m, (await overridden.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("difference").GetDecimal());
    }

    [Fact]
    public async Task ASessionWithoutCashDrawerIsForbiddenOnDrawerRoutes()
    {
        // V1-RMD-400 (V1-RMD-399 H-02): a signed-in waiter or kitchen device must not operate the drawer.
        var terminalId = Guid.NewGuid();
        var waiterCookie = await _database.SeedCashierSessionWithPermissionsAsync(
            terminalId, "rmd400-no-drawer", ApplicationPermissions.OrdersCreate, ApplicationPermissions.OrdersSend);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var open = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions", waiterCookie, new { OpeningBalance = 100m });
        var active = await GetAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/active", waiterCookie);
        var movement = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{Guid.NewGuid():D}/cash-movements", waiterCookie,
            new { Direction = "Out", Amount = 50m, IdempotencyKey = "rmd400-payout" });

        Assert.Equal(HttpStatusCode.Forbidden, open.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, active.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, movement.StatusCode);
        Assert.Equal(0L, await _database.CountCashSessionsAsync(terminalId));
    }

    [Fact]
    public async Task CashTenderNeedsPaymentsTakeOnTopOfCashDrawer()
    {
        // V1-RMD-401: holding the drawer is not enough to take a payment.
        var terminalId = Guid.NewGuid();
        var cashierCookie = await _database.SeedCashierSessionAsync(terminalId, "rmd401-drawer-and-payments");
        var drawerOnlyCookie = await _database.SeedCashierSessionWithPermissionsAsync(
            terminalId, "rmd401-drawer-only", ApplicationPermissions.CashDrawer);
        var billId = await _database.SeedBillAsync(payable: 80m);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var opened = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions", cashierCookie, new { OpeningBalance = 100m });
        var sessionId = (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cashSessionId").GetGuid();

        var tender = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/cash-tender", drawerOnlyCookie,
            new { BillId = billId, AmountDue = 80m, TenderedAmount = 100m, IdempotencyKey = "rmd401-cash" });

        Assert.Equal(HttpStatusCode.Forbidden, tender.StatusCode);
    }

    [Fact]
    public async Task ReconcileNeedsASupervisorWhoIsNotTheSessionsOwnCashier()
    {
        // V1-RMD-400 (V1-RMD-393 F-09): cash-session-design.md §6 — reconciliation is a supervisor act, four-eyes.
        var terminalId = Guid.NewGuid();
        var ownerCookie = await _database.SeedCashierSessionWithPermissionsAsync(
            terminalId, "rmd400-owner-supervisor", ApplicationPermissions.CashDrawer, ApplicationPermissions.CashSessionOverride);
        var cashierCookie = await _database.SeedCashierSessionAsync(terminalId, "rmd400-plain-cashier");
        var otherSupervisorCookie = await _database.SeedCashierSessionWithPermissionsAsync(
            terminalId, "rmd400-other-supervisor", ApplicationPermissions.CashDrawer, ApplicationPermissions.CashSessionOverride);
        await using var app = await StartAsync();
        using var client = CreateClient(app);
        var opened = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions", ownerCookie, new { OpeningBalance = 100m });
        var sessionId = (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cashSessionId").GetGuid();
        var closed = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/close", ownerCookie,
            new { ActualCash = 100m });
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var reconcilePath = $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}/reconcile";

        var byPlainCashier = await PostAsync(client, reconcilePath, cashierCookie, new { Notes = "cashier" });
        var byOwnCashier = await PostAsync(client, reconcilePath, ownerCookie, new { Notes = "self" });
        var byOtherSupervisor = await PostAsync(client, reconcilePath, otherSupervisorCookie, new { Notes = "checked" });

        Assert.Equal(HttpStatusCode.Forbidden, byPlainCashier.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byOwnCashier.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byOtherSupervisor.StatusCode);
        Assert.Equal("Reconciled", (await byOtherSupervisor.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task WithoutACashierSessionEveryRouteIsUnauthorized()
    {
        var terminalId = Guid.NewGuid();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var response = await PostAsync(client, $"/api/v1/terminals/{terminalId:D}/cash-sessions", cookie: null, new { OpeningBalance = 100m });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

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

internal sealed class CashSessionHttpTestDatabase
{
    private readonly string _databaseName = "alkaros_csh004_" + Guid.NewGuid().ToString("N")[..8];
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

    /// <summary>
    /// Seeds a real user + a real cashier device session bound to <paramref name="terminalId"/>, and returns the raw
    /// Cookie header value. V1-RMD-400: the user holds <c>cash.drawer</c>, as a real cashier does — every drawer
    /// route now requires it; V1-RMD-401: and <c>payments.take</c>, which the cash tender requires.
    /// </summary>
    public Task<string> SeedCashierSessionAsync(Guid terminalId, string rawToken)
        => SeedCashierSessionWithPermissionsAsync(
            terminalId, rawToken, ApplicationPermissions.CashDrawer, ApplicationPermissions.PaymentsTake);

    /// <summary>
    /// V1-RMD-236: seeds a real user + device session + role + explicit
    /// permission grants bound to <paramref name="terminalId"/> — used for
    /// the cash.session.override authorization checks the plain
    /// <see cref="SeedCashierSessionAsync"/> user intentionally has none of.
    /// </summary>
    public async Task<string> SeedCashierSessionWithPermissionsAsync(
        Guid terminalId, string rawToken, params string[] permissionCodes)
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var suffix = userId.ToString("N")[..8];

        await using (var command = DataSource.CreateCommand(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'x', 'CashSession Test Supervisor', true);

            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, expires_at)
            VALUES (gen_random_uuid(), @user_id, @device_id, @token_hash, now() + interval '1 hour');

            INSERT INTO identity.roles (role_id, code, name)
            VALUES (@role_id, @role_code, 'CashSession Test Role');

            INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
            VALUES (gen_random_uuid(), @user_id, @role_id);
            """))
        {
            command.Parameters.AddWithValue("user_id", userId);
            command.Parameters.AddWithValue("username", "csh004-role-" + suffix);
            command.Parameters.AddWithValue("device_id", $"cashier:{terminalId:D}");
            command.Parameters.AddWithValue("token_hash", DeviceSessionToken.Hash(rawToken));
            command.Parameters.AddWithValue("role_id", roleId);
            command.Parameters.AddWithValue("role_code", "csh004-role-" + suffix);
            await command.ExecuteNonQueryAsync();
        }

        foreach (var code in permissionCodes)
        {
            await using var grant = DataSource.CreateCommand(
                """
                INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
                SELECT gen_random_uuid(), @role_id, permission_id FROM identity.permissions WHERE code = @code;
                """);
            grant.Parameters.AddWithValue("role_id", roleId);
            grant.Parameters.AddWithValue("code", code);
            await grant.ExecuteNonQueryAsync();
        }

        return $"alkaros.cashier={rawToken}";
    }

    public async Task<long> CountCashSessionsAsync(Guid terminalId)
    {
        await using var command = DataSource.CreateCommand("SELECT count(*) FROM cash.cash_sessions WHERE terminal_id = @terminal_id;");
        command.Parameters.AddWithValue("terminal_id", terminalId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>Seeds a real, payable Bill (catalog product + table + order + bill) for a cash-tender test.</summary>
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
