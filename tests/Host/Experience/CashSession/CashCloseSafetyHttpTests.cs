using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Orders.OrderAggregate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.CashSession.Tests;

/// <summary>
/// the cash-close rules the PO decided on 2026-11-03: the close needs a recorded count, any difference needs an
/// explanation, the tolerance is a manager setting with a hard ceiling, open bills and uncertain payments are warned
/// about and acknowledged (never blocking), only the session's cashier or a manager closes it, and the person who
/// closed a session does not reconcile it unless the shared self-approval setting is on.
/// </summary>
[Collection("CashSession HTTP")]
public sealed class CashCloseSafetyHttpTests : IAsyncLifetime
{
    private const string ToleranceKey = "cash.close_variance_tolerance";
    private const string SelfApprovalKey = "approvals.manager_self_approval_enabled";

    private readonly CashSessionHttpTestDatabase _database = new();
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"alkaros-rmd790-webroot-{Guid.NewGuid():N}");

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
    public async Task ACloseWithoutARecordedCountIsRefusedAndTheSessionStaysInCounting()
    {
        await using var drawer = await DrawerAsync(opening: 100m);
        await drawer.StartCountAsync();

        var response = await drawer.CloseAsync(100m);

        await AssertErrorAsync(response, HttpStatusCode.Conflict, "CASH_COUNT_REQUIRED", "sayım");
        Assert.Equal("Counting", await drawer.StatusAsync());
    }

    [Fact]
    public async Task TheClosingAmountMustEqualTheLatestRecordedCount()
    {
        await using var drawer = await DrawerAsync(opening: 100m);
        await drawer.StartCountAsync();
        await drawer.CountAsync(90m);

        var mismatch = await drawer.CloseAsync(100m, explanation: "x");
        await AssertErrorAsync(mismatch, HttpStatusCode.Conflict, "CASH_COUNT_MISMATCH", "sayım");
        Assert.Equal("Counting", await drawer.StatusAsync());

        await drawer.CountAsync(100m);
        var latestWins = await drawer.CloseAsync(100m);
        Assert.Equal(HttpStatusCode.OK, latestWins.StatusCode);
    }

    [Theory]
    [InlineData(99.99, null)]
    [InlineData(99.99, "   ")]
    [InlineData(105, null)]
    [InlineData(40, "")]
    public async Task AnyDifferenceNeedsAnExplanationEvenInsideTheTolerance(double counted, string? explanation)
    {
        await using var drawer = await DrawerAsync(opening: 100m);
        await drawer.StartCountAsync();
        await drawer.CountAsync((decimal)counted);

        var response = await drawer.CloseAsync((decimal)counted, explanation: explanation);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "CASH_DIFFERENCE_EXPLANATION_REQUIRED", "açıklama");
        Assert.Equal("Counting", await drawer.StatusAsync());
    }

    [Fact]
    public async Task AnExplanationLongerThanTheLimitIsRefused()
    {
        await using var drawer = await DrawerAsync(opening: 100m);
        await drawer.StartCountAsync();
        await drawer.CountAsync(95m);

        var response = await drawer.CloseAsync(95m, explanation: new string('x', 501));

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "CASH_DIFFERENCE_EXPLANATION_REQUIRED", "500");
    }

    [Fact]
    public async Task AShortageIsPostedToTheLedgerAsAClosingDifferenceThatDoesNotChangeTheExpectedCash()
    {
        await using var drawer = await DrawerAsync(opening: 100m);
        await drawer.StartCountAsync();
        await drawer.CountAsync(87.5m);

        var response = await drawer.CloseAsync(87.5m, explanation: "  para üstü eksik verildi ");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = await _database.ClosingDifferenceRowsAsync(drawer.SessionId);
        var only = Assert.Single(row);
        Assert.Equal(("Out", 12.5m, "para üstü eksik verildi"), only);
        Assert.Equal("para üstü eksik verildi", await _database.DifferenceExplanationAsync(drawer.SessionId));
        Assert.Equal(100m, (await drawer.PreviewAsync()).GetProperty("expectedCash").GetDecimal());
        Assert.Empty((await drawer.ListAsync()).GetProperty("movements").EnumerateArray());
    }

    [Fact]
    public async Task AnOverageIsPostedAsAnIncomingClosingDifferenceAndAMatchingCountPostsNone()
    {
        await using var over = await DrawerAsync(opening: 100m);
        await over.StartCountAsync();
        await over.CountAsync(130m);
        Assert.Equal(HttpStatusCode.OK, (await over.CloseAsync(130m, explanation: "bahşiş karıştı")).StatusCode);
        Assert.Equal(("In", 30m, "bahşiş karıştı"), Assert.Single(await _database.ClosingDifferenceRowsAsync(over.SessionId)));

        await using var exact = await DrawerAsync(opening: 100m);
        await exact.StartCountAsync();
        await exact.CountAsync(100m);
        Assert.Equal(HttpStatusCode.OK, (await exact.CloseAsync(100m)).StatusCode);
        Assert.Empty(await _database.ClosingDifferenceRowsAsync(exact.SessionId));
    }

    [Fact]
    public async Task TheToleranceDefaultsToFiftyAndOneKurusAboveItNeedsAManagerOverride()
    {
        await using var atLimit = await DrawerAsync(opening: 100m);
        await atLimit.StartCountAsync();
        await atLimit.CountAsync(50m);
        Assert.Equal(HttpStatusCode.OK, (await atLimit.CloseAsync(50m, explanation: "eksik")).StatusCode);

        await using var above = await DrawerAsync(opening: 100m);
        await above.StartCountAsync();
        await above.CountAsync(49.99m);
        var refused = await above.CloseAsync(49.99m, explanation: "eksik");
        await AssertErrorAsync(refused, HttpStatusCode.Conflict, "CASH_VARIANCE_THRESHOLD_EXCEEDED", "yönetici");
    }

    [Fact]
    public async Task TheToleranceIsAManagerSettingAndTheHardCeilingHoldsEvenIfTheStoredValueIsLarger()
    {
        await _database.SetSettingAsync(ToleranceKey, "Integer", "10");
        await using var tight = await DrawerAsync(opening: 100m);
        await tight.StartCountAsync();
        await tight.CountAsync(89.99m);
        await AssertErrorAsync(await tight.CloseAsync(89.99m, explanation: "e"), HttpStatusCode.Conflict, "CASH_VARIANCE_THRESHOLD_EXCEEDED", "yönetici");
        await tight.CountAsync(90m);
        Assert.Equal(HttpStatusCode.OK, (await tight.CloseAsync(90m, explanation: "e")).StatusCode);

        // A value written around the validator (straight into the table) never widens the tolerance past 500 TL.
        await _database.SetSettingAsync(ToleranceKey, "Integer", "5000");
        await using var wide = await DrawerAsync(opening: 1000m);
        await wide.StartCountAsync();
        await wide.CountAsync(499.99m);
        await AssertErrorAsync(await wide.CloseAsync(499.99m, explanation: "e"), HttpStatusCode.Conflict, "CASH_VARIANCE_THRESHOLD_EXCEEDED", "yönetici");
        await wide.CountAsync(500m);
        Assert.Equal(HttpStatusCode.OK, (await wide.CloseAsync(500m, explanation: "e")).StatusCode);
        Assert.Equal(500m, (await wide.PreviewAsync()).GetProperty("varianceTolerance").GetDecimal());
    }

    [Fact]
    public async Task OpenBillsAndUncertainPaymentsAreCountedWarnedAboutAndAcknowledgedNeverBlocking()
    {
        await using var drawer = await DrawerAsync(opening: 100m);
        await _database.SeedBillAsync(40m);
        var paidBill = await _database.SeedBillAsync(30m);
        await _database.SetBillStatusAsync(paidBill, "Paid");
        await _database.InsertPaymentAsync(paidBill, "Unknown");
        await _database.InsertPaymentAsync(paidBill, "Pending");
        await _database.InsertPaymentAsync(paidBill, "ReconciliationRequired");
        await _database.InsertPaymentAsync(paidBill, "Approved");
        await _database.InsertPaymentAsync(paidBill, "Declined");
        var cancelledBill = await _database.SeedBillAsync(20m);
        await _database.SetBillStatusAsync(cancelledBill, "Cancelled");
        await drawer.StartCountAsync();
        await drawer.CountAsync(100m);

        var preview = await drawer.PreviewAsync();
        Assert.Equal(1, preview.GetProperty("openBillCount").GetInt32());
        Assert.Equal(3, preview.GetProperty("unsettledPaymentCount").GetInt32());

        var refused = await drawer.CloseAsync(100m);
        await AssertErrorAsync(refused, HttpStatusCode.Conflict, "CASH_CLOSE_UNSETTLED_NOT_ACKNOWLEDGED", "hesap");
        Assert.Equal("Counting", await drawer.StatusAsync());

        var acknowledged = await drawer.CloseAsync(100m, acknowledge: true);
        Assert.Equal(HttpStatusCode.OK, acknowledged.StatusCode);
        var audit = Assert.Single(await _database.CloseAuditAsync(drawer.SessionId));
        Assert.Equal(1, audit.GetProperty("openBills").GetInt32());
        Assert.Equal(3, audit.GetProperty("unsettledPayments").GetInt32());
        Assert.True(audit.GetProperty("unsettledAcknowledged").GetBoolean());
    }

    [Fact]
    public async Task ASentOrderWithoutAnyBillIsAnOpenCheckThatWarnsTheCloseButADraftACompletedACancelledOrAnAlreadyBilledOneIsNot()
    {
        await using var drawer = await DrawerAsync(opening: 100m);
        await _database.SeedOrderWithoutBillAsync(OrderSource.Cashier, "Submitted");
        await _database.SeedOrderWithoutBillAsync(OrderSource.Waiter, "Submitted");
        await _database.SeedOrderWithoutBillAsync(OrderSource.Nfc, "Accepted");
        await _database.SeedOrderWithoutBillAsync(OrderSource.Qr, "Served");
        await _database.SeedOrderWithoutBillAsync(OrderSource.Cashier, "Completed");
        await _database.SeedOrderWithoutBillAsync(OrderSource.Cashier, "Draft");
        await _database.SeedOrderWithoutBillAsync(OrderSource.Online, "Submitted");
        await _database.SeedOrderWithoutBillAsync(OrderSource.Cashier, "Preparing");
        await _database.SeedOrderWithoutBillAsync(OrderSource.Waiter, "Ready");
        await _database.SeedOrderWithoutBillAsync(OrderSource.Online, "Completed");
        await _database.SeedOrderWithoutBillAsync(OrderSource.Online, "Cancelled");
        await _database.SeedOrderWithoutBillAsync(OrderSource.Cashier, "Cancelled");
        var billed = await _database.SeedBillAsync(40m);
        await _database.SetOrderStatusOfBillAsync(billed, "Submitted");
        await drawer.StartCountAsync();
        await drawer.CountAsync(100m);

        var preview = await drawer.PreviewAsync();
        Assert.Equal(8, preview.GetProperty("openBillCount").GetInt32());

        var refused = await drawer.CloseAsync(100m);
        await AssertErrorAsync(refused, HttpStatusCode.Conflict, "CASH_CLOSE_UNSETTLED_NOT_ACKNOWLEDGED", "hesap");
        Assert.Equal("Counting", await drawer.StatusAsync());
        Assert.Equal(HttpStatusCode.OK, (await drawer.CloseAsync(100m, acknowledge: true)).StatusCode);
    }

    [Fact]
    public async Task ACloseWithNothingUnsettledNeedsNoAcknowledgementAndTheAuditSaysSo()
    {
        await using var drawer = await DrawerAsync(opening: 100m);
        await drawer.StartCountAsync();
        await drawer.CountAsync(98m);

        var response = await drawer.CloseAsync(98m, explanation: "bozuk para");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var audit = Assert.Single(await _database.CloseAuditAsync(drawer.SessionId));
        Assert.Equal(0, audit.GetProperty("openBills").GetInt32());
        Assert.Equal(0, audit.GetProperty("unsettledPayments").GetInt32());
        Assert.False(audit.GetProperty("unsettledAcknowledged").GetBoolean());
        Assert.Equal(-2m, audit.GetProperty("difference").GetDecimal());
        Assert.Equal("bozuk para", audit.GetProperty("differenceExplanation").GetString());
        Assert.Equal(await drawer.OwnerIdAsync(), audit.GetProperty("closedBy").GetGuid());
    }

    [Fact]
    public async Task OnlyTheSessionsCashierOrAManagerMayCountAndCloseItAndTheOwnersOwnCloseWritesNoDenial()
    {
        await using var drawer = await DrawerAsync(opening: 100m);
        var stranger = await drawer.CookieAsync("stranger", ApplicationPermissions.CashDrawer);

        await AssertErrorAsync(await drawer.PostAsync("/start-count", new { }, stranger), HttpStatusCode.Forbidden, "CASH_SESSION_OWNER_REQUIRED", "açan");
        Assert.Equal("Open", await drawer.StatusAsync());

        await drawer.StartCountAsync();
        await AssertErrorAsync(await drawer.PostAsync("/counts", new { CountedAmount = 100m }, stranger), HttpStatusCode.Forbidden, "CASH_SESSION_OWNER_REQUIRED", "açan");
        await drawer.CountAsync(100m);
        await AssertErrorAsync(await drawer.CloseAsync(100m, cookie: stranger), HttpStatusCode.Forbidden, "CASH_SESSION_OWNER_REQUIRED", "açan");
        Assert.Equal("Counting", await drawer.StatusAsync());

        Assert.Equal(HttpStatusCode.OK, (await drawer.CloseAsync(100m)).StatusCode);
        Assert.Equal(0, await _database.DenialCountAsync(await drawer.OwnerIdAsync()));
    }

    [Fact]
    public async Task AnotherCashierOnTheTerminalLearnsWhoseDrawerSessionIsOpenButAWaiterDoesNot()
    {
        await using var drawer = await DrawerAsync(opening: 100m);
        var stranger = await drawer.CookieAsync("stranger", ApplicationPermissions.CashDrawer);
        var waiter = await drawer.CookieAsync("waiter", ApplicationPermissions.OrdersCreate);
        await RenameUserAsync(await drawer.OwnerIdAsync(), "Drawer Owner");
        await RenameUserAsync(await _database.UserIdForCookieAsync(stranger), "Someone Else");

        var named = await drawer.ActiveOwnerAsync(stranger);
        Assert.Equal(HttpStatusCode.OK, named.StatusCode);
        Assert.Equal("Drawer Owner", (await named.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("displayName").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await drawer.ActiveOwnerAsync(waiter)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await drawer.ActiveOwnerAsync("")).StatusCode);

        await drawer.StartCountAsync();
        await drawer.CountAsync(100m);
        Assert.Equal(HttpStatusCode.OK, (await drawer.CloseAsync(100m)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await drawer.ActiveOwnerAsync(stranger)).StatusCode);
    }

    [Fact]
    public async Task AManagerMayStartCountAndCloseAnotherCashiersSession()
    {
        await using var drawer = await DrawerAsync(opening: 100m);
        var manager = await drawer.CookieAsync("manager", ApplicationPermissions.CashDrawer, ApplicationPermissions.CashSessionOverride);

        Assert.Equal(HttpStatusCode.OK, (await drawer.PostAsync("/start-count", new { }, manager)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await drawer.PostAsync("/counts", new { CountedAmount = 100m }, manager)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await drawer.CloseAsync(100m, cookie: manager)).StatusCode);
    }

    [Fact]
    public async Task WhoClosedASessionCannotReconcileItUnlessTheSharedSelfApprovalSettingIsOn()
    {
        await using var drawer = await DrawerAsync(opening: 100m);
        var closer = await drawer.ManagerAsync("closer");
        var other = await drawer.ManagerAsync("other");
        await drawer.PostAsync("/start-count", new { }, closer.Cookie);
        await drawer.PostAsync("/counts", new { CountedAmount = 100m }, closer.Cookie);
        Assert.Equal(HttpStatusCode.OK, (await drawer.CloseAsync(100m, cookie: closer.Cookie)).StatusCode);

        var byCloser = await drawer.PostAsync("/reconcile", new { Notes = "kendi" }, closer.Cookie);
        await AssertErrorAsync(byCloser, HttpStatusCode.Forbidden, "CASH_RECONCILE_SELF_NOT_ALLOWED", "kapat");

        var byOther = await drawer.PostAsync("/reconcile", new { Notes = "kontrol edildi" }, other.Cookie);
        Assert.Equal(HttpStatusCode.OK, byOther.StatusCode);
        Assert.False(await _database.ReconciledSelfApprovedAsync(drawer.SessionId));
        Assert.Contains("cash.session.reconciled", await _database.AuditEventTypesAsync(drawer.SessionId));
    }

    [Fact]
    public async Task WithTheSharedSelfApprovalSettingOnTheCloserMayReconcileAndTheRecordIsMarked()
    {
        await _database.SetSettingAsync(SelfApprovalKey, "Boolean", "true");
        await using var drawer = await DrawerAsync(opening: 100m);
        var manager = await drawer.ManagerAsync("sole-manager");
        await drawer.PostAsync("/start-count", new { }, manager.Cookie);
        await drawer.PostAsync("/counts", new { CountedAmount = 100m }, manager.Cookie);
        await drawer.CloseAsync(100m, cookie: manager.Cookie);

        var reconciled = await drawer.PostAsync("/reconcile", new { Notes = "tek yönetici" }, manager.Cookie);

        Assert.Equal(HttpStatusCode.OK, reconciled.StatusCode);
        Assert.True(await _database.ReconciledSelfApprovedAsync(drawer.SessionId));
        var audit = Assert.Single(await _database.ReconcileAuditAsync(drawer.SessionId));
        Assert.True(audit.GetProperty("selfApproved").GetBoolean());
        Assert.Equal(manager.UserId, audit.GetProperty("reconciledBy").GetGuid());
    }

    [Fact]
    public async Task TheSessionsOwnCashierWhoIsAlsoAManagerCannotReconcileItWithoutTheSetting()
    {
        await using var drawer = await DrawerAsync(opening: 100m, ownerPermissions: [ApplicationPermissions.CashDrawer, ApplicationPermissions.CashSessionOverride]);
        await drawer.StartCountAsync();
        await drawer.CountAsync(100m);
        await drawer.CloseAsync(100m);

        var response = await drawer.PostAsync("/reconcile", new { Notes = "kendi" }, drawer.Owner);

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, "CASH_RECONCILE_SELF_NOT_ALLOWED", "kapat");
    }

    [Fact]
    public async Task AManagerWhoOnlyCountedTheDrawerCannotReconcileItEither()
    {
        await using var drawer = await DrawerAsync(opening: 100m);
        var counter = await drawer.ManagerAsync("counter");
        var closer = await drawer.ManagerAsync("closer");
        var other = await drawer.ManagerAsync("other");
        await drawer.PostAsync("/start-count", new { }, counter.Cookie);
        await drawer.PostAsync("/counts", new { CountedAmount = 100m }, counter.Cookie);
        Assert.Equal(HttpStatusCode.OK, (await drawer.CloseAsync(100m, cookie: closer.Cookie)).StatusCode);

        await AssertErrorAsync(
            await drawer.PostAsync("/reconcile", new { Notes = "saydım" }, counter.Cookie),
            HttpStatusCode.Forbidden, "CASH_RECONCILE_SELF_NOT_ALLOWED", "kapat");
        Assert.Equal(HttpStatusCode.OK, (await drawer.PostAsync("/reconcile", new { Notes = "kontrol" }, other.Cookie)).StatusCode);
    }

    [Fact]
    public async Task ASupervisorOverrideIsAuditedAndMarkedWhenTheSessionsOwnCashierGaveIt()
    {
        await using var byManager = await DrawerAsync(opening: 100m);
        var manager = await byManager.ManagerAsync("override-manager");
        await byManager.PostAsync("/start-count", new { }, manager.Cookie);
        await byManager.PostAsync("/counts", new { CountedAmount = 20m }, manager.Cookie);
        Assert.Equal(
            HttpStatusCode.OK,
            (await byManager.PostAsync("/close", new { ActualCash = 20m, DifferenceExplanation = "e", IsSupervisorOverride = true, OverrideReason = "sayım doğrulandı" }, manager.Cookie)).StatusCode);
        var managerAudit = Assert.Single(await _database.CloseAuditAsync(byManager.SessionId));
        Assert.True(managerAudit.GetProperty("supervisorOverride").GetBoolean());
        Assert.False(managerAudit.GetProperty("overrideBySessionCashier").GetBoolean());

        await using var own = await DrawerAsync(opening: 100m, ownerPermissions: [ApplicationPermissions.CashDrawer, ApplicationPermissions.CashSessionOverride]);
        await own.StartCountAsync();
        await own.CountAsync(20m);
        Assert.Equal(
            HttpStatusCode.OK,
            (await own.PostAsync("/close", new { ActualCash = 20m, DifferenceExplanation = "e", IsSupervisorOverride = true, OverrideReason = "kendim" })).StatusCode);
        Assert.True(Assert.Single(await _database.CloseAuditAsync(own.SessionId)).GetProperty("overrideBySessionCashier").GetBoolean());
    }

    [Fact]
    public async Task ACashierWithoutTheOverridePermissionCannotSelfDeclareAnOverrideOnClose()
    {
        await using var drawer = await DrawerAsync(opening: 100m);
        await drawer.StartCountAsync();
        await drawer.CountAsync(20m);

        var response = await drawer.PostAsync("/close", new { ActualCash = 20m, DifferenceExplanation = "e", IsSupervisorOverride = true, OverrideReason = "x" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Counting", await drawer.StatusAsync());
    }

    [Fact]
    public async Task ASessionOfAnotherTerminalIsNotFoundOnThisTerminalsRoutes()
    {
        await using var first = await DrawerAsync(opening: 100m);
        await using var second = await DrawerAsync(opening: 100m);

        var response = await second.SendAsync(second.PathOf(first.SessionId) + "/start-count", second.Owner, new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Open", await first.StatusAsync());
    }

    [Fact]
    public async Task ACountNoteLongerThanFiveHundredCharactersIsRefused()
    {
        await using var drawer = await DrawerAsync(opening: 100m);
        await drawer.StartCountAsync();

        var response = await drawer.PostAsync("/counts", new { CountedAmount = 100m, Notes = new string('x', 501) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TwoParallelClosesCloseTheSessionOnceAndPostTheDifferenceOnce()
    {
        await using var drawer = await DrawerAsync(opening: 100m);
        await drawer.StartCountAsync();
        await drawer.CountAsync(90m);

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => drawer.CloseAsync(90m, explanation: "eksik")));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Single(await _database.ClosingDifferenceRowsAsync(drawer.SessionId));
        Assert.Single(await _database.CloseAuditAsync(drawer.SessionId));
    }

    [Fact]
    public async Task ACountRecordedWhileTheSessionIsClosingEitherCountsOrIsRefusedNeverLandsBehindTheClose()
    {
        // The closed session's actual cash must always equal the newest recorded count: a count written between the
        // close's read of the latest count and its write would leave a count the close never saw.
        for (var round = 0; round < 6; round++)
        {
            await using var drawer = await DrawerAsync(opening: 100m);
            await drawer.StartCountAsync();
            await drawer.CountAsync(100m);

            var close = drawer.CloseAsync(100m);
            var late = drawer.PostAsync("/counts", new { CountedAmount = 250m }, drawer.Owner);
            await Task.WhenAll(close, late);

            var closed = (await close).StatusCode == HttpStatusCode.OK;
            if (closed)
            {
                Assert.Equal(HttpStatusCode.Conflict, (await late).StatusCode);
                Assert.Equal(100m, await _database.LatestCountAsync(drawer.SessionId));
            }
            else
            {
                Assert.Equal(HttpStatusCode.Conflict, (await close).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await late).StatusCode);
                Assert.Equal("Counting", await drawer.StatusAsync());
            }
        }
    }

    [Fact]
    public async Task ABlankExplanationCannotBeStoredOnAClosedSessionWithADifference()
    {
        await using var drawer = await DrawerAsync(opening: 100m);
        await drawer.StartCountAsync();
        await drawer.CountAsync(95m);
        await drawer.CloseAsync(95m, explanation: "eksik");

        var blank = await _database.SqlStateOfAsync(
            $"UPDATE cash.cash_sessions SET difference_explanation = '  ' WHERE cash_session_id = '{drawer.SessionId}';");
        var cleared = await _database.SqlStateOfAsync(
            $"UPDATE cash.cash_sessions SET difference_explanation = NULL WHERE cash_session_id = '{drawer.SessionId}';");

        Assert.Equal(PostgresErrorCodes.CheckViolation, blank);
        Assert.Equal(PostgresErrorCodes.CheckViolation, cleared);
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code, string messagePart)
    {
        Assert.Equal(status, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.Contains(messagePart, error.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task RenameUserAsync(Guid userId, string displayName)
    {
        await using var command = _database.DataSource.CreateCommand("UPDATE identity.users SET display_name = @name WHERE user_id = @id;");
        command.Parameters.AddWithValue("name", displayName);
        command.Parameters.AddWithValue("id", userId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Drawer> DrawerAsync(decimal opening, string[]? ownerPermissions = null)
    {
        var terminalId = Guid.NewGuid();
        var token = "drawer-" + Guid.NewGuid().ToString("N")[..8];
        var cashier = ownerPermissions is null
            ? await _database.SeedCashierSessionAsync(terminalId, token)
            : await _database.SeedCashierSessionWithPermissionsAsync(terminalId, token, ownerPermissions);
        var app = DualScreenApplication.Build(new DualScreenOptions(
            _database.ConnectionString, _webRoot, "http://127.0.0.1:0", TrustedProxies: [System.Net.IPAddress.Loopback]));
        await app.StartAsync();
        var server = app.Services.GetRequiredService<IServer>();
        var client = new HttpClient { BaseAddress = new Uri(server.Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
        var drawer = new Drawer(app, client, _database, terminalId, cashier);
        var opened = await drawer.SendAsync($"/api/v1/terminals/{terminalId:D}/cash-sessions", cashier, new { OpeningBalance = opening });
        drawer.SessionId = (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cashSessionId").GetGuid();
        return drawer;
    }

    private sealed record Manager(string Cookie, Guid UserId);

    private sealed class Drawer(WebApplication app, HttpClient client, CashSessionHttpTestDatabase database, Guid terminalId, string owner) : IAsyncDisposable
    {
        public Guid SessionId { get; set; }
        public string Owner { get; } = owner;
        private string SessionPath => $"/api/v1/terminals/{terminalId:D}/cash-sessions/{SessionId:D}";

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }

        public Task<string> CookieAsync(string token, params string[] permissions)
            => database.SeedCashierSessionWithPermissionsAsync(terminalId, token + "-" + Guid.NewGuid().ToString("N")[..6], permissions);

        public async Task<Manager> ManagerAsync(string token)
        {
            var cookie = await CookieAsync(token, ApplicationPermissions.CashDrawer, ApplicationPermissions.CashSessionOverride);
            return new Manager(cookie, await database.UserIdForCookieAsync(cookie));
        }

        public Task<Guid> OwnerIdAsync() => database.UserIdForCookieAsync(Owner);

        public Task<HttpResponseMessage> SendAsync(string path, string cookie, object body)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            };
            return Send(request, cookie);
        }

        public Task<HttpResponseMessage> PostAsync(string suffix, object body, string? cookie = null) => SendAsync(SessionPath + suffix, cookie ?? Owner, body);

        public async Task StartCountAsync() => Assert.Equal(HttpStatusCode.OK, (await PostAsync("/start-count", new { })).StatusCode);

        public async Task CountAsync(decimal amount) => Assert.Equal(HttpStatusCode.OK, (await PostAsync("/counts", new { CountedAmount = amount })).StatusCode);

        public Task<HttpResponseMessage> CloseAsync(decimal actual, string? explanation = null, bool acknowledge = false, string? cookie = null)
            => PostAsync("/close", new { ActualCash = actual, DifferenceExplanation = explanation, AcknowledgeUnsettled = acknowledge }, cookie);

        public Task<HttpResponseMessage> ActiveOwnerAsync(string cookie)
            => Send(new HttpRequestMessage(HttpMethod.Get, $"/api/v1/terminals/{terminalId:D}/cash-sessions/active/owner"), cookie);

        public string PathOf(Guid sessionId) => $"/api/v1/terminals/{terminalId:D}/cash-sessions/{sessionId:D}";

        public Task<string> StatusAsync() => database.SessionStatusAsync(SessionId);

        public async Task<JsonElement> PreviewAsync()
        {
            var response = await Send(new HttpRequestMessage(HttpMethod.Get, SessionPath + "/expected-cash"), Owner);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }

        public async Task<JsonElement> ListAsync()
        {
            var response = await Send(new HttpRequestMessage(HttpMethod.Get, SessionPath + "/cash-movements"), Owner);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }

        private Task<HttpResponseMessage> Send(HttpRequestMessage request, string cookie)
        {
            request.Headers.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.10");
            request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
            return client.SendAsync(request);
        }
    }
}
