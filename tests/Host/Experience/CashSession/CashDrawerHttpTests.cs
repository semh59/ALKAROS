using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.Authorization.Catalog;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.CashSession.Tests;

/// <summary>
/// The physical cash drawer: a cash payment opens it, "no sale" opens it with a mandatory reason, every opening is
/// audited and counted on the closing record. A local TCP listener stands in for the receipt printer, so the bytes
/// the drawer would get are checked for real.
/// </summary>
[Collection("CashSession HTTP")]
public sealed class CashDrawerHttpTests : IAsyncLifetime
{
    private static readonly byte[] Pulse = [0x1B, 0x70, 0x00, 0x19, 0xFA];
    private readonly CashSessionHttpTestDatabase _database = new();
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"alkaros-rmd1134-webroot-{Guid.NewGuid():N}");

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
    public async Task ACashPaymentOpensTheDrawerOnceAndTheOpeningIsAudited()
    {
        await using var rig = await RigAsync();

        var tender = await rig.TenderAsync(80m, 100m, "tender-1");

        Assert.Equal(HttpStatusCode.OK, tender.StatusCode);
        Assert.Equal([Pulse], await rig.KicksAsync(1));
        var opened = Assert.Single(await rig.DrawerAuditAsync(), row => row.Event == "cash.drawer.opened");
        Assert.Equal("Sale", opened.Kind);
        Assert.Equal(rig.UserId, opened.Actor);
    }

    [Fact]
    public async Task AReplayedCashPaymentDoesNotOpenTheDrawerAgain()
    {
        await using var rig = await RigAsync();
        var billId = await _database.SeedBillAsync(payable: 80m);

        Assert.Equal(HttpStatusCode.OK, (await rig.TenderAsync(80m, 100m, "tender-replay", billId)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await rig.TenderAsync(80m, 100m, "tender-replay", billId)).StatusCode);

        Assert.Single(await rig.KicksAsync(1));
        await Task.Delay(300);
        Assert.Single(rig.ReceivedKicks);
        Assert.Single(await rig.DrawerAuditAsync(), row => row.Event == "cash.drawer.opened");
    }

    [Fact]
    public async Task ACashPaymentWithTheDrawerOffTakesTheMoneyAndSendsNothing()
    {
        await using var rig = await RigAsync(enabled: false);

        var tender = await rig.TenderAsync(80m, 100m, "tender-off");

        Assert.Equal(HttpStatusCode.OK, tender.StatusCode);
        await Task.Delay(300);
        Assert.Empty(rig.ReceivedKicks);
        Assert.Empty(await rig.DrawerAuditAsync());
    }

    [Fact]
    public async Task APrinterThatCannotBeReachedNeverFailsTheCashPaymentAndTheFailureIsRecorded()
    {
        await using var rig = await RigAsync(address: $"127.0.0.1:{FreePort()}");

        var tender = await rig.TenderAsync(80m, 100m, "tender-down");

        Assert.Equal(HttpStatusCode.OK, tender.StatusCode);
        Assert.Equal(80m, (await tender.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("approvedAmount").GetDecimal());
        var audit = await rig.DrawerAuditAsync();
        Assert.Equal(["cash.drawer.kick-failed"], audit.Select(row => row.Event));
        Assert.Equal("Sale", audit[0].Kind);
    }

    [Fact]
    public async Task TheCashPaymentAnswerSaysWhetherTheDrawerOpened()
    {
        await using var delivered = await RigAsync();
        var opened = await (await delivered.TenderAsync(80m, 100m, "answer-opened")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(opened.GetProperty("drawerOpened").GetBoolean());
        Assert.Equal(80m, opened.GetProperty("approvedAmount").GetDecimal());
        Assert.Equal(20m, opened.GetProperty("changeAmount").GetDecimal());

        await using var down = await RigAsync(address: $"127.0.0.1:{FreePort()}");
        var failed = await (await down.TenderAsync(80m, 100m, "answer-failed")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(failed.GetProperty("drawerOpened").GetBoolean());

        await using var off = await RigAsync(enabled: false);
        var untried = await (await off.TenderAsync(80m, 100m, "answer-off")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, untried.GetProperty("drawerOpened").ValueKind);
    }

    [Fact]
    public async Task AReplayedCashPaymentAnswersWithoutTryingTheDrawer()
    {
        await using var rig = await RigAsync();
        var billId = await _database.SeedBillAsync(payable: 80m);
        await rig.TenderAsync(80m, 100m, "answer-replay", billId);

        var replay = await (await rig.TenderAsync(80m, 100m, "answer-replay", billId)).Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(replay.GetProperty("wasReplayed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, replay.GetProperty("drawerOpened").ValueKind);
    }

    [Fact]
    public async Task APrinterThatNeverAnswersHoldsTheCashPaymentForAboutASecondAtMost()
    {
        await using var rig = await RigAsync(address: "192.0.2.1:9100");
        await rig.TenderAsync(80m, 100m, "slow-warmup");

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var tender = await rig.TenderAsync(80m, 100m, "slow-printer");
        watch.Stop();

        Assert.Equal(HttpStatusCode.OK, tender.StatusCode);
        Assert.InRange(watch.Elapsed, TimeSpan.Zero, TimeSpan.FromMilliseconds(1700));
        Assert.False((await tender.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("drawerOpened").GetBoolean());
        Assert.Equal(2, (await rig.DrawerAuditAsync()).Count(row => row.Event == "cash.drawer.kick-failed"));
    }

    [Fact]
    public async Task NoSaleOpensTheDrawerRecordsTheReasonAndMovesNoMoney()
    {
        await using var rig = await RigAsync();
        var expectedBefore = await rig.ExpectedCashAsync();

        var response = await rig.NoSaleAsync("ns-1", "ChangeCoins", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("delivered").GetBoolean());
        Assert.Equal(1, body.GetProperty("noSaleCount").GetInt32());
        Assert.False(body.GetProperty("replayed").GetBoolean());
        Assert.Equal([Pulse], await rig.KicksAsync(1));
        var opened = Assert.Single(await rig.DrawerAuditAsync());
        Assert.Equal(("cash.drawer.opened", "NoSale", "Bozuk para", rig.UserId), (opened.Event, opened.Kind, opened.Reason, opened.Actor));
        Assert.Equal(expectedBefore, await rig.ExpectedCashAsync());
        Assert.Equal(0L, await _database.CountLedgerRowsAsync(rig.SessionId, "Sale"));
    }

    [Theory]
    [InlineData("Count", null, "Sayım")]
    [InlineData("Count", "gün sonu", "Sayım: gün sonu")]
    [InlineData("Other", "kasa tamiri", "Diğer: kasa tamiri")]
    public async Task EveryListedReasonIsAcceptedAndOtherCarriesItsNote(string code, string? note, string reason)
    {
        await using var rig = await RigAsync();

        var response = await rig.NoSaleAsync("ns-reasons", code, note);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(reason, Assert.Single(await rig.DrawerAuditAsync()).Reason);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("Whatever", null)]
    [InlineData("Whatever", "bir not")]
    [InlineData("Other", null)]
    [InlineData("Other", "   ")]
    [InlineData("ChangeCoins", "x")]
    public async Task NoSaleWithoutAValidReasonIsRefusedAndTheDrawerStaysShut(string? code, string? note)
    {
        await using var rig = await RigAsync();
        var tooLong = code == "ChangeCoins" ? new string('n', 201) : note;

        var response = await rig.NoSaleAsync("ns-bad", code, tooLong);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "CASH_DRAWER_REASON_INVALID", "gerekçe");
        await Task.Delay(300);
        Assert.Empty(rig.ReceivedKicks);
        Assert.Empty(await rig.DrawerAuditAsync());
    }

    [Fact]
    public async Task NoSaleNeedsItsOwnPermissionNotJustTheDrawerOne()
    {
        await using var rig = await RigAsync(permissions: [ApplicationPermissions.CashDrawer, ApplicationPermissions.PaymentsTake]);

        var response = await rig.NoSaleAsync("ns-denied", "Count", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await Task.Delay(300);
        Assert.Empty(rig.ReceivedKicks);
        Assert.DoesNotContain(await rig.DrawerAuditAsync(), row => row.Event == "cash.drawer.opened");
    }

    [Fact]
    public async Task NoSaleWithTheDrawerOffIsRefusedInTurkish()
    {
        await using var rig = await RigAsync(enabled: false);

        var response = await rig.NoSaleAsync("ns-off", "Count", null);

        await AssertErrorAsync(response, HttpStatusCode.Conflict, "CASH_DRAWER_DISABLED", "çekmece");
        Assert.Empty(await rig.DrawerAuditAsync());
    }

    [Fact]
    public async Task NoSaleOnAnotherCashiersSessionIsRefused()
    {
        await using var rig = await RigAsync();
        var other = await _database.SeedCashierSessionWithPermissionsAsync(
            rig.TerminalId, "other-" + Guid.NewGuid().ToString("N")[..6],
            ApplicationPermissions.CashDrawer, ApplicationPermissions.CashDrawerKick);

        var response = await rig.NoSaleAsync("ns-other", "Count", null, other);

        await AssertErrorAsync(response, HttpStatusCode.Conflict, "CASH_SESSION_NOT_OWNED", "kasa");
        await Task.Delay(300);
        Assert.Empty(rig.ReceivedKicks);
    }

    [Fact]
    public async Task NoSaleWhileTheCashIsBeingCountedIsAllowedButNotOnAClosedSession()
    {
        await using var rig = await RigAsync();
        Assert.Equal(HttpStatusCode.OK, (await rig.PostAsync("/start-count", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await rig.NoSaleAsync("ns-count", "Count", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await rig.PostAsync("/counts", new { CountedAmount = 100m })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await rig.PostAsync("/close", new { ActualCash = 100m })).StatusCode);

        var late = await rig.NoSaleAsync("ns-late", "Count", null);

        await AssertErrorAsync(late, HttpStatusCode.Conflict, "CASH_DRAWER_SESSION_NOT_OPEN", "kapanmış");
        Assert.Single(await rig.KicksAsync(1));
        Assert.Single(await rig.DrawerAuditAsync());
    }

    [Fact]
    public async Task TwoNoSalesAtOnceAreCountedOneAfterTheOther()
    {
        await using var rig = await RigAsync();

        var responses = await Task.WhenAll(Enumerable.Range(1, 4).Select(i => rig.NoSaleAsync("ns-par-" + i, "Count", null)));

        var counts = new List<int>();
        foreach (var response in responses)
            counts.Add((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("noSaleCount").GetInt32());
        Assert.Equal([1, 2, 3, 4], counts.Order());
    }

    [Fact]
    public async Task ARepeatedNoSaleKeyOpensTheDrawerAndWritesTheRecordOnlyOnce()
    {
        await using var rig = await RigAsync();

        var first = await rig.NoSaleAsync("ns-same", "Count", null);
        var second = await rig.NoSaleAsync("ns-same", "Count", null);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var replay = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(replay.GetProperty("replayed").GetBoolean());
        Assert.Equal(1, replay.GetProperty("noSaleCount").GetInt32());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Single(await rig.KicksAsync(1));
        await Task.Delay(300);
        Assert.Single(rig.ReceivedKicks);
        Assert.Single(await rig.DrawerAuditAsync());
    }

    [Fact]
    public async Task ANoSaleToAPrinterThatCannotBeReachedKeepsTheRecordAndSaysTheDrawerDidNotOpen()
    {
        await using var rig = await RigAsync(address: $"127.0.0.1:{FreePort()}");

        var response = await rig.NoSaleAsync("ns-down", "Count", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("delivered").GetBoolean());
        Assert.Equal(["cash.drawer.kick-failed", "cash.drawer.opened"], (await rig.DrawerAuditAsync()).Select(row => row.Event).Order());
    }

    [Fact]
    public async Task FailedNoSalePulsesAreNeitherCountedNorWarnedAboutButAreCountedApart()
    {
        await using var rig = await RigAsync(address: $"127.0.0.1:{FreePort()}");

        for (var i = 1; i <= 5; i++)
        {
            var body = await (await rig.NoSaleAsync("ns-fail-" + i, "Count", null)).Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(body.GetProperty("delivered").GetBoolean());
            Assert.Equal(0, body.GetProperty("noSaleCount").GetInt32());
            Assert.Equal(JsonValueKind.Null, body.GetProperty("warning").ValueKind);
        }

        Assert.Equal(0, (await rig.StatusAsync()).GetProperty("noSaleCount").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await rig.PostAsync("/start-count", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await rig.PostAsync("/counts", new { CountedAmount = 100m })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await rig.PostAsync("/close", new { ActualCash = 100m })).StatusCode);
        var state = await rig.ClosedStateAsync();
        Assert.Equal((0, 5), (state.GetProperty("drawerNoSaleCount").GetInt32(), state.GetProperty("drawerKickFailedCount").GetInt32()));
    }

    [Fact]
    public async Task ADeliveredNoSaleAfterFailedOnesIsCountedOnceAndTheFifthDeliveredOneWarns()
    {
        await using var rig = await RigAsync();
        await _database.SetSettingAsync("cash.drawer_printer_address", "String", $"127.0.0.1:{FreePort()}");
        for (var i = 1; i <= 3; i++)
            await rig.NoSaleAsync("ns-lost-" + i, "Count", null);
        await _database.SetSettingAsync("cash.drawer_printer_address", "String", $"127.0.0.1:{rig.PrinterPort}");

        var counts = new List<int>();
        var warnings = new List<JsonValueKind>();
        for (var i = 1; i <= 5; i++)
        {
            var body = await (await rig.NoSaleAsync("ns-sent-" + i, "Count", null)).Content.ReadFromJsonAsync<JsonElement>();
            counts.Add(body.GetProperty("noSaleCount").GetInt32());
            warnings.Add(body.GetProperty("warning").ValueKind);
        }

        Assert.Equal([1, 2, 3, 4, 5], counts);
        Assert.Equal([JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.String], warnings);
    }

    [Fact]
    public async Task AFailedNoSaleAfterFourDeliveredOnesKeepsTheCountAndRaisesNoWarning()
    {
        await using var rig = await RigAsync();
        for (var i = 1; i <= 4; i++)
            await rig.NoSaleAsync("ns-four-" + i, "Count", null);
        await _database.SetSettingAsync("cash.drawer_printer_address", "String", $"127.0.0.1:{FreePort()}");

        var failed = await (await rig.NoSaleAsync("ns-fifth-lost", "Count", null)).Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(failed.GetProperty("delivered").GetBoolean());
        Assert.Equal(4, failed.GetProperty("noSaleCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, failed.GetProperty("warning").ValueKind);
    }

    [Fact]
    public async Task ARepeatedFailedNoSaleKeyChangesNoCount()
    {
        await using var rig = await RigAsync(address: $"127.0.0.1:{FreePort()}");

        await rig.NoSaleAsync("ns-fail-same", "Count", null);
        var replay = await (await rig.NoSaleAsync("ns-fail-same", "Count", null)).Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(replay.GetProperty("replayed").GetBoolean());
        Assert.False(replay.GetProperty("delivered").GetBoolean());
        Assert.Equal(0, replay.GetProperty("noSaleCount").GetInt32());
        Assert.Equal(0, (await rig.StatusAsync()).GetProperty("noSaleCount").GetInt32());
    }

    [Fact]
    public async Task TheFifthNoSaleInASessionCarriesAWarning()
    {
        await using var rig = await RigAsync();

        var warnings = new List<string?>();
        for (var i = 1; i <= 5; i++)
        {
            var response = await rig.NoSaleAsync("ns-many-" + i, "Count", null);
            warnings.Add((await response.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("warning", out var warning) && warning.ValueKind == JsonValueKind.String ? warning.GetString() : null);
        }

        Assert.All(warnings.Take(4), Assert.Null);
        Assert.Contains("5", warnings[4]);
    }

    [Fact]
    public async Task TheClosingRecordCountsEveryDrawerOpeningOfTheSession()
    {
        await using var rig = await RigAsync();
        Assert.Equal(HttpStatusCode.OK, (await rig.TenderAsync(80m, 100m, "tender-close")).StatusCode);
        await rig.KicksAsync(1);
        Assert.Equal(HttpStatusCode.OK, (await rig.NoSaleAsync("ns-c1", "Count", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await rig.NoSaleAsync("ns-c2", "ChangeCoins", null)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await rig.PostAsync("/start-count", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await rig.PostAsync("/counts", new { CountedAmount = 180m })).StatusCode);
        var closed = await rig.PostAsync("/close", new { ActualCash = 180m });
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);

        var state = await rig.ClosedStateAsync();
        Assert.Equal((1, 2, 0), (state.GetProperty("drawerSaleOpenCount").GetInt32(), state.GetProperty("drawerNoSaleCount").GetInt32(), state.GetProperty("drawerKickFailedCount").GetInt32()));
    }

    [Fact]
    public async Task TheCloseAnswerCarriesTheSameDrawerCountsAsTheClosingRecord()
    {
        await using var rig = await RigAsync();
        Assert.Equal(HttpStatusCode.OK, (await rig.TenderAsync(80m, 100m, "tender-answer")).StatusCode);
        await rig.KicksAsync(1);
        Assert.Equal(HttpStatusCode.OK, (await rig.NoSaleAsync("ns-a1", "Count", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await rig.NoSaleAsync("ns-a2", "ChangeCoins", null)).StatusCode);
        await _database.SetSettingAsync("cash.drawer_printer_address", "String", $"127.0.0.1:{FreePort()}");
        Assert.False((await (await rig.NoSaleAsync("ns-a3", "Count", null)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("delivered").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await rig.PostAsync("/start-count", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await rig.PostAsync("/counts", new { CountedAmount = 180m })).StatusCode);

        var closed = await (await rig.PostAsync("/close", new { ActualCash = 180m })).Content.ReadFromJsonAsync<JsonElement>();

        var drawer = closed.GetProperty("drawer");
        Assert.Equal((1, 2, 1), (drawer.GetProperty("saleOpenCount").GetInt32(), drawer.GetProperty("noSaleCount").GetInt32(), drawer.GetProperty("kickFailedCount").GetInt32()));
        var record = await rig.ClosedStateAsync();
        Assert.Equal((1, 2, 1), (record.GetProperty("drawerSaleOpenCount").GetInt32(), record.GetProperty("drawerNoSaleCount").GetInt32(), record.GetProperty("drawerKickFailedCount").GetInt32()));
        Assert.Equal(0m, closed.GetProperty("difference").GetDecimal());
    }

    [Fact]
    public async Task TheCloseAnswerSaysZeroDrawerOpeningsWhenTheDrawerIsOff()
    {
        await using var rig = await RigAsync(enabled: false);
        Assert.Equal(HttpStatusCode.OK, (await rig.PostAsync("/start-count", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await rig.PostAsync("/counts", new { CountedAmount = 100m })).StatusCode);

        var closed = await (await rig.PostAsync("/close", new { ActualCash = 100m })).Content.ReadFromJsonAsync<JsonElement>();

        var drawer = closed.GetProperty("drawer");
        Assert.Equal((0, 0, 0), (drawer.GetProperty("saleOpenCount").GetInt32(), drawer.GetProperty("noSaleCount").GetInt32(), drawer.GetProperty("kickFailedCount").GetInt32()));
    }

    [Fact]
    public async Task TheStatusSaysWhetherTheButtonShouldShowAndOffersTheReasons()
    {
        await using var on = await RigAsync();
        await on.NoSaleAsync("ns-s1", "Count", null);
        var enabled = await on.StatusAsync();
        Assert.True(enabled.GetProperty("enabled").GetBoolean());
        Assert.True(enabled.GetProperty("canKick").GetBoolean());
        Assert.Equal(1, enabled.GetProperty("noSaleCount").GetInt32());
        Assert.Equal(5, enabled.GetProperty("warningCount").GetInt32());
        Assert.Equal(["ChangeCoins:Bozuk para", "Count:Sayım", "Other:Diğer"], enabled.GetProperty("reasons").EnumerateArray().Select(r => $"{r.GetProperty("code").GetString()}:{r.GetProperty("name").GetString()}"));

        await using var off = await RigAsync(enabled: false, permissions: [ApplicationPermissions.CashDrawer, ApplicationPermissions.PaymentsTake]);
        var disabled = await off.StatusAsync();
        Assert.False(disabled.GetProperty("enabled").GetBoolean());
        Assert.False(disabled.GetProperty("canKick").GetBoolean());
    }

    private async Task<Rig> RigAsync(bool enabled = true, string? address = null, string[]? permissions = null)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        await _database.SetSettingAsync("cash.drawer_kick_enabled", "Boolean", enabled ? "true" : "false");
        await _database.SetSettingAsync("cash.drawer_printer_address", "String", address ?? $"127.0.0.1:{port}");
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedCashierSessionWithPermissionsAsync(
            terminalId, "drawer-" + Guid.NewGuid().ToString("N")[..8],
            permissions ?? [ApplicationPermissions.CashDrawer, ApplicationPermissions.PaymentsTake, ApplicationPermissions.CashDrawerKick]);
        var app = DualScreenApplication.Build(new DualScreenOptions(
            _database.ConnectionString, _webRoot, "http://127.0.0.1:0", TrustedProxies: [IPAddress.Loopback]));
        await app.StartAsync();
        var server = app.Services.GetRequiredService<IServer>();
        var client = new HttpClient { BaseAddress = new Uri(server.Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
        var rig = new Rig(app, client, listener, _database, terminalId, cookie);
        rig.UserId = await _database.UserIdForCookieAsync(cookie);
        var opened = await rig.SendAsync($"/api/v1/terminals/{terminalId:D}/cash-sessions", cookie, new { OpeningBalance = 100m });
        rig.SessionId = (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cashSessionId").GetGuid();
        return rig;
    }

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code, string messagePart)
    {
        Assert.Equal(status, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.Contains(messagePart, error.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    private sealed record DrawerAuditRow(string Event, string? Kind, string? Reason, Guid? Actor);

    private sealed class Rig : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly HttpClient _client;
        private readonly TcpListener _listener;
        private readonly CashSessionHttpTestDatabase _database;
        private readonly string _owner;
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _accepting;

        public Rig(WebApplication app, HttpClient client, TcpListener listener, CashSessionHttpTestDatabase database, Guid terminalId, string owner)
        {
            (_app, _client, _listener, _database, TerminalId, _owner) = (app, client, listener, database, terminalId, owner);
            _accepting = AcceptAsync();
        }

        public Guid TerminalId { get; }
        public int PrinterPort => ((IPEndPoint)_listener.LocalEndpoint).Port;
        public Guid SessionId { get; set; }
        public Guid UserId { get; set; }
        public ConcurrentQueue<byte[]> ReceivedKicks { get; } = new();

        private string SessionPath => $"/api/v1/terminals/{TerminalId:D}/cash-sessions/{SessionId:D}";

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            _listener.Stop();
            try { await _accepting; } catch (Exception) when (_stop.IsCancellationRequested) { }
            _client.Dispose();
            await _app.DisposeAsync();
        }

        public async Task<byte[][]> KicksAsync(int expected)
        {
            for (var i = 0; i < 100 && ReceivedKicks.Count < expected; i++)
                await Task.Delay(50);
            return [.. ReceivedKicks];
        }

        public Task<HttpResponseMessage> SendAsync(string path, string cookie, object body)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            };
            return Send(request, cookie);
        }

        public Task<HttpResponseMessage> PostAsync(string suffix, object body) => SendAsync(SessionPath + suffix, _owner, body);

        public async Task<Guid> NewBillAsync(decimal payable) => await _database.SeedBillAsync(payable);

        public async Task<HttpResponseMessage> TenderAsync(decimal due, decimal tendered, string key, Guid? billId = null)
            => await PostAsync("/cash-tender", new { BillId = billId ?? await NewBillAsync(due), AmountDue = due, TenderedAmount = tendered, IdempotencyKey = key });

        public Task<HttpResponseMessage> NoSaleAsync(string key, string? reasonCode, string? note, string? cookie = null)
            => SendAsync(SessionPath + "/cash-drawer/no-sale", cookie ?? _owner, new { IdempotencyKey = key, ReasonCode = reasonCode, Note = note });

        public async Task<JsonElement> StatusAsync()
        {
            var response = await Send(new HttpRequestMessage(HttpMethod.Get, SessionPath + "/cash-drawer"), _owner);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }

        public async Task<decimal> ExpectedCashAsync()
        {
            var response = await Send(new HttpRequestMessage(HttpMethod.Get, SessionPath + "/expected-cash"), _owner);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("expectedCash").GetDecimal();
        }

        public async Task<DrawerAuditRow[]> DrawerAuditAsync()
        {
            await using var command = _database.DataSource.CreateCommand(
                """
                SELECT event_name, after_state_json->>'kind', reason, actor_id FROM audit.audit_events
                WHERE aggregate_id = @id AND event_name LIKE 'cash.drawer.%' ORDER BY occurred_at;
                """);
            command.Parameters.AddWithValue("id", SessionId);
            var rows = new List<DrawerAuditRow>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                rows.Add(new DrawerAuditRow(reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetGuid(3)));
            return [.. rows];
        }

        public async Task<JsonElement> ClosedStateAsync()
        {
            await using var command = _database.DataSource.CreateCommand(
                "SELECT after_state_json::text FROM audit.audit_events WHERE aggregate_id = @id AND event_name = 'cash.session.closed';");
            command.Parameters.AddWithValue("id", SessionId);
            return JsonDocument.Parse((string)(await command.ExecuteScalarAsync())!).RootElement.Clone();
        }

        private async Task AcceptAsync()
        {
            while (!_stop.IsCancellationRequested)
            {
                using var connection = await _listener.AcceptTcpClientAsync(_stop.Token);
                using var buffer = new MemoryStream();
                await connection.GetStream().CopyToAsync(buffer, _stop.Token);
                ReceivedKicks.Enqueue(buffer.ToArray());
            }
        }

        private Task<HttpResponseMessage> Send(HttpRequestMessage request, string cookie)
        {
            request.Headers.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.10");
            request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
            return _client.SendAsync(request);
        }
    }
}
