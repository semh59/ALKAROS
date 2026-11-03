using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.Authorization.Catalog;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.CashSession.Tests;

/// <summary>
/// the cash-in/cash-out rules the PO decided on 2026-11-03 - a mandatory reason, a drawer ceiling,
/// manager approval above a threshold that is a manager setting, and a self-approval setting that is off by default.
/// </summary>
[Collection("CashSession HTTP")]
public sealed class CashMovementApprovalHttpTests : IAsyncLifetime
{
    private const string ThresholdKey = "cash.cash_out_approval_threshold";
    private const string SelfApprovalKey = "approvals.manager_self_approval_enabled";

    private static readonly string[] DecisionActions = ["approve", "reject", "cancel"];
    private static readonly string[] ManualMovementTypes = ["CashOut", "CashIn"];
    private static readonly string[] AuditedSteps =
    [
        "cash.movement_request.approved", "cash.movement_request.cancelled", "cash.movement_request.rejected",
        "cash.movement_request.requested", "cash.movement_request.requested", "cash.movement_request.requested",
    ];

    private readonly CashSessionHttpTestDatabase _database = new();
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"alkaros-rmd788-webroot-{Guid.NewGuid():N}");

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

    [Theory]
    [InlineData("In", null)]
    [InlineData("In", "   ")]
    [InlineData("Out", null)]
    [InlineData("Out", "   ")]
    public async Task AMovementWithoutAReasonIsRefusedAndRecordsNothing(string direction, string? notes)
    {
        await using var till = await TillAsync(opening: 200m);

        var response = await till.MoveAsync(direction, 10m, notes);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "CASH_MOVEMENT_REASON_INVALID", "açıklama");
        Assert.Equal(200m, await till.ExpectedCashAsync());
    }

    [Fact]
    public async Task AReasonLongerThanTheLimitIsRefused()
    {
        await using var till = await TillAsync(opening: 200m);

        var response = await till.MoveAsync("In", 10m, new string('x', 501));

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "CASH_MOVEMENT_REASON_INVALID", "500");
    }

    [Fact]
    public async Task ACashOutAboveWhatTheDrawerHoldsIsRefusedWhileTheExactBalanceGoesOut()
    {
        await using var till = await TillAsync(opening: 100m);

        var over = await till.MoveAsync("Out", 100.01m, "tedarikçi");
        await AssertErrorAsync(over, HttpStatusCode.Conflict, "CASH_OUT_EXCEEDS_DRAWER", "kasadaki");
        Assert.Equal(100m, await till.ExpectedCashAsync());

        var exact = await till.MoveAsync("Out", 100m, "tedarikçi");
        Assert.Equal(HttpStatusCode.Created, exact.StatusCode);
        Assert.Equal(0m, await till.ExpectedCashAsync());
    }

    [Fact]
    public async Task ConcurrentCashOutsCannotTogetherTakeMoreThanTheDrawerHolds()
    {
        // 700 TL in the drawer, four parallel 300 TL payouts: only two fit. A check that does not hold the session
        // row exclusively lets every request read 700 before any of them writes.
        await using var till = await TillAsync(opening: 700m);

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => till.MoveAsync("Out", 300m, "tedarikçi " + i)));

        Assert.Equal(2, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(2, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(100m, await till.ExpectedCashAsync());
    }

    [Fact]
    public async Task TheThresholdItselfNeedsNoApprovalAndOneKurusAboveItDoes()
    {
        await using var till = await TillAsync(opening: 2000m);

        var atThreshold = await till.MoveAsync("Out", 500m, "gider");
        var above = await till.MoveAsync("Out", 500.01m, "tedarikçi");

        Assert.Equal(HttpStatusCode.Created, atThreshold.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, above.StatusCode);
        var request = await above.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Requested", request.GetProperty("status").GetString());
        Assert.Equal(500.01m, request.GetProperty("amount").GetDecimal());
        Assert.Equal(1500m, await till.ExpectedCashAsync());
    }

    [Fact]
    public async Task TheThresholdIsAManagerSettingAndCashInNeverNeedsApproval()
    {
        await using var till = await TillAsync(opening: 2000m);
        await _database.SetSettingAsync(ThresholdKey, "Integer", "100");

        var smallOut = await till.MoveAsync("Out", 100m, "gider");
        var bigOut = await till.MoveAsync("Out", 150m, "gider");
        var bigIn = await till.MoveAsync("In", 5000m, "para eklendi");

        Assert.Equal(HttpStatusCode.Created, smallOut.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, bigOut.StatusCode);
        Assert.Equal(HttpStatusCode.Created, bigIn.StatusCode);
    }

    [Fact]
    public async Task AnotherManagerApprovingWritesTheLedgerRowAndMarksWhoApproved()
    {
        await using var till = await TillAsync(opening: 2000m);
        var requestId = await till.RequestAsync(800m, "tedarikçi");
        var manager = await till.ManagerAsync("approver");

        var approved = await till.DecideAsync(requestId, "approve", manager.Cookie);

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var body = await approved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Approved", body.GetProperty("status").GetString());
        Assert.False(body.GetProperty("selfApproved").GetBoolean());
        Assert.Equal(manager.UserId, body.GetProperty("decidedBy").GetGuid());
        Assert.Equal(1200m, await till.ExpectedCashAsync());
        var listing = await till.ListAsync();
        var movement = Assert.Single(listing.GetProperty("movements").EnumerateArray(), m => m.GetProperty("type").GetString() == "CashOut");
        Assert.Equal("tedarikçi", movement.GetProperty("notes").GetString());
        Assert.Equal(manager.UserId, movement.GetProperty("approvedBy").GetGuid());
        Assert.False(string.IsNullOrEmpty(movement.GetProperty("approvedByName").GetString()));
        Assert.Equal(await _database.UserIdForCookieAsync(till.Cashier), movement.GetProperty("recordedBy").GetGuid());
        Assert.False(movement.GetProperty("selfApproved").GetBoolean());
        Assert.Empty(listing.GetProperty("requests").EnumerateArray());
    }

    [Fact]
    public async Task AManagerCannotApproveTheirOwnRequestUnlessTheSettingIsOn()
    {
        await using var till = await TillAsync(opening: 2000m);
        var manager = await till.ManagerAsync("own-request");
        var requestId = await till.RequestAsync(800m, "avans", manager.Cookie);

        var refused = await till.DecideAsync(requestId, "approve", manager.Cookie);
        await AssertErrorAsync(refused, HttpStatusCode.Forbidden, "CASH_MOVEMENT_SELF_APPROVAL", "kendi");
        Assert.Equal(2000m, await till.ExpectedCashAsync());

        await _database.SetSettingAsync(SelfApprovalKey, "Boolean", "true");
        var approved = await till.DecideAsync(requestId, "approve", manager.Cookie);

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.True((await approved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("selfApproved").GetBoolean());
        var listing = await till.ListAsync(manager.Cookie);
        Assert.True(Assert.Single(listing.GetProperty("movements").EnumerateArray(), m => m.GetProperty("type").GetString() == "CashOut")
            .GetProperty("selfApproved").GetBoolean());
        Assert.Equal(1200m, await till.ExpectedCashAsync());
    }

    [Fact]
    public async Task OnlyAUserWithTheOverridePermissionCanDecide()
    {
        await using var till = await TillAsync(opening: 2000m);
        var requestId = await till.RequestAsync(800m, "tedarikçi");
        var otherCashier = await till.Cookie("plain-cashier", ApplicationPermissions.CashDrawer, ApplicationPermissions.PaymentsTake);

        var approve = await till.DecideAsync(requestId, "approve", otherCashier);
        var reject = await till.DecideAsync(requestId, "reject", otherCashier, new { Note = "hayır" });

        await AssertErrorAsync(approve, HttpStatusCode.Forbidden, "FORBIDDEN", "yetkiniz yok");
        await AssertErrorAsync(reject, HttpStatusCode.Forbidden, "FORBIDDEN", "yetkiniz yok");
        Assert.Equal(2000m, await till.ExpectedCashAsync());
        Assert.Single((await till.ListAsync()).GetProperty("requests").EnumerateArray());
    }

    [Fact]
    public async Task RejectingAndCancellingRecordNothingAndOnlyOpenRequestsCanBeDecided()
    {
        await using var till = await TillAsync(opening: 2000m);
        var manager = await till.ManagerAsync("rejecter");
        var rejectedId = await till.RequestAsync(800m, "tedarikçi");
        var cancelledId = await till.RequestAsync(900m, "avans");

        var rejected = await till.DecideAsync(rejectedId, "reject", manager.Cookie, new { Note = "uygun değil" });
        var cancelledByOther = await till.DecideAsync(cancelledId, "cancel", manager.Cookie);
        var cancelled = await till.DecideAsync(cancelledId, "cancel", till.Cashier);
        var approveRejected = await till.DecideAsync(rejectedId, "approve", manager.Cookie);

        Assert.Equal("Rejected", (await rejected.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        await AssertErrorAsync(cancelledByOther, HttpStatusCode.Forbidden, "FORBIDDEN", "yetkiniz yok");
        Assert.Equal("Cancelled", (await cancelled.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        await AssertErrorAsync(approveRejected, HttpStatusCode.Conflict, "CASH_MOVEMENT_REQUEST_STATE", "artık beklemiyor");
        Assert.Equal(2000m, await till.ExpectedCashAsync());
    }

    [Fact]
    public async Task ApprovalChecksTheDrawerAgainAtApprovalTime()
    {
        await using var till = await TillAsync(opening: 700m);
        var requestId = await till.RequestAsync(600m, "tedarikçi");
        Assert.Equal(HttpStatusCode.Created, (await till.MoveAsync("Out", 300m, "gider")).StatusCode);
        var manager = await till.ManagerAsync("late-approver");

        var approved = await till.DecideAsync(requestId, "approve", manager.Cookie);

        await AssertErrorAsync(approved, HttpStatusCode.Conflict, "CASH_OUT_EXCEEDS_DRAWER", "kasadaki");
        Assert.Equal(400m, await till.ExpectedCashAsync());
    }

    [Fact]
    public async Task AnOpenRequestBlocksStartingTheCount()
    {
        await using var till = await TillAsync(opening: 2000m);
        var requestId = await till.RequestAsync(800m, "tedarikçi");

        var blocked = await till.PostAsync("/start-count", new { });

        await AssertErrorAsync(blocked, HttpStatusCode.Conflict, "CASH_MOVEMENT_REQUESTS_PENDING", "onay bekleyen");
        await till.DecideAsync(requestId, "cancel", till.Cashier);
        Assert.Equal(HttpStatusCode.OK, (await till.PostAsync("/start-count", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await till.PostAsync("/close", new { ActualCash = 2000m })).StatusCode);
    }

    [Fact]
    public async Task ARequestCommittedInTheInstantTheCountStartedStillBlocksClosing()
    {
        // No request can be opened once the session left Open; this row stands for the one committed while the count started.
        await using var till = await TillAsync(opening: 2000m);
        await till.PostAsync("/start-count", new { });
        await _database.InsertRequestAsync(till.SessionId, await _database.UserIdForCookieAsync(till.Cashier));

        var blocked = await till.PostAsync("/close", new { ActualCash = 2000m });

        await AssertErrorAsync(blocked, HttpStatusCode.Conflict, "CASH_MOVEMENT_REQUESTS_PENDING", "onay bekleyen");
    }

    [Fact]
    public async Task NothingCanBeRequestedOnceTheCountStarted()
    {
        await using var till = await TillAsync(opening: 2000m);
        await till.PostAsync("/start-count", new { });

        var request = await till.MoveAsync("Out", 800m, "tedarikçi");

        await AssertErrorAsync(request, HttpStatusCode.Conflict, "CLOSED_CASH_SESSION", "açık değil");
    }

    [Fact]
    public async Task ARejectionNoteIsOptionalTrimmedAndLimited()
    {
        await using var till = await TillAsync(opening: 2000m);
        var manager = await till.ManagerAsync("noter");
        var first = await till.RequestAsync(800m, "avans");
        var second = await till.RequestAsync(700m, "avans 2");

        var tooLong = await till.DecideAsync(first, "reject", manager.Cookie, new { Note = new string('x', 501) });
        var blank = await till.DecideAsync(first, "reject", manager.Cookie, new { Note = "   " });
        var noBody = await till.DecideWithoutBodyAsync(second, "reject", manager.Cookie);

        await AssertErrorAsync(tooLong, HttpStatusCode.BadRequest, "VALIDATION_FAILED", "ret notu");
        Assert.Equal(HttpStatusCode.OK, blank.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await blank.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("decisionNote").ValueKind);
        Assert.Equal(HttpStatusCode.OK, noBody.StatusCode);
    }

    [Fact]
    public async Task AKeyReplayReturnsTheSameMovementOrRequestAndANoteEditIsAKeyReuse()
    {
        await using var till = await TillAsync(opening: 2000m);
        var directKey = Guid.NewGuid().ToString();
        var first = await till.MoveAsync("Out", 50m, "gider", directKey);
        var replay = await till.MoveAsync("Out", 50m, "gider", directKey);
        var editedNote = await till.MoveAsync("Out", 50m, "başka gider", directKey);
        var requestKey = Guid.NewGuid().ToString();
        var firstRequest = await till.MoveAsync("Out", 800m, "avans", requestKey);
        var replayedRequest = await till.MoveAsync("Out", 800m, "avans", requestKey);
        var editedRequest = await till.MoveAsync("Out", 800m, "başka avans", requestKey);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(
            (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(),
            (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        await AssertErrorAsync(editedNote, HttpStatusCode.Conflict, "IDEMPOTENCY_KEY_REUSED", "anahtar");
        Assert.Equal(HttpStatusCode.Accepted, firstRequest.StatusCode);
        var firstRequestId = (await firstRequest.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(firstRequestId, (await replayedRequest.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        await AssertErrorAsync(editedRequest, HttpStatusCode.Conflict, "IDEMPOTENCY_KEY_REUSED", "anahtar");
        Assert.Single((await till.ListAsync()).GetProperty("requests").EnumerateArray());
        Assert.Equal(1950m, await till.ExpectedCashAsync());

        var manager = await till.ManagerAsync("replay-approver");
        await till.DecideAsync(firstRequestId, "approve", manager.Cookie);
        var afterApproval = await till.MoveAsync("Out", 800m, "avans", requestKey);
        Assert.Equal(HttpStatusCode.OK, afterApproval.StatusCode);
        Assert.Equal("Approved", (await afterApproval.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal(1150m, await till.ExpectedCashAsync());
    }

    [Fact]
    public async Task TheListShowsMovementsRequestsTheThresholdAndTheSelfApprovalSetting()
    {
        await using var till = await TillAsync(opening: 2000m);
        await till.MoveAsync("In", 20m, "para eklendi");
        await till.MoveAsync("Out", 30m, "gider");
        await till.RequestAsync(800m, "avans");

        var listing = await till.ListAsync();

        Assert.Equal(500m, listing.GetProperty("approvalThreshold").GetDecimal());
        Assert.False(listing.GetProperty("movementsTruncated").GetBoolean());
        Assert.False(listing.GetProperty("selfApprovalEnabled").GetBoolean());
        var movements = listing.GetProperty("movements").EnumerateArray().ToArray();
        Assert.Equal(ManualMovementTypes, movements.Select(m => m.GetProperty("type").GetString()).ToArray());
        var cashierId = await _database.UserIdForCookieAsync(till.Cashier);
        Assert.All(movements, m =>
        {
            Assert.False(string.IsNullOrEmpty(m.GetProperty("recordedByName").GetString()));
            Assert.Equal(cashierId, m.GetProperty("recordedBy").GetGuid());
            Assert.Equal(JsonValueKind.Null, m.GetProperty("approvedBy").ValueKind);
            Assert.Equal(JsonValueKind.Null, m.GetProperty("approvedByName").ValueKind);
            Assert.False(m.GetProperty("selfApproved").GetBoolean());
            Assert.True(m.GetProperty("occurredAt").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddMinutes(-5));
        });
        var pending = Assert.Single(listing.GetProperty("requests").EnumerateArray());
        Assert.Equal(cashierId, pending.GetProperty("requestedBy").GetGuid());
        Assert.False(string.IsNullOrEmpty(pending.GetProperty("requestedByName").GetString()));
        Assert.Equal("avans", pending.GetProperty("reason").GetString());
        Assert.Equal(800m, pending.GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task EveryStepOfARequestWritesAnAuditEventInTheSameTransaction()
    {
        await using var till = await TillAsync(opening: 2000m);
        var manager = await till.ManagerAsync("audit-approver");
        var approvedId = await till.RequestAsync(800m, "avans");
        await till.DecideAsync(approvedId, "approve", manager.Cookie);
        var rejectedId = await till.RequestAsync(900m, "avans 2");
        await till.DecideAsync(rejectedId, "reject", manager.Cookie, new { Note = "hayır" });
        var cancelledId = await till.RequestAsync(700m, "avans 3");
        await till.DecideAsync(cancelledId, "cancel", till.Cashier);

        var events = await _database.AuditEventTypesAsync(approvedId, rejectedId, cancelledId);

        Assert.Equal(AuditedSteps, events);
    }

    [Fact]
    public async Task ATerminalOfAnotherDrawerCannotListOrDecideThisDrawersRequests()
    {
        await using var till = await TillAsync(opening: 2000m);
        var requestId = await till.RequestAsync(800m, "avans");
        var otherTerminal = Guid.NewGuid();
        var foreignManager = await _database.SeedCashierSessionWithPermissionsAsync(
            otherTerminal, "foreign-manager", ApplicationPermissions.CashDrawer, ApplicationPermissions.CashSessionOverride);
        var path = $"/api/v1/terminals/{otherTerminal:D}/cash-sessions/{till.SessionId:D}";

        var list = await till.GetAsync(path + "/cash-movements", foreignManager);
        var approve = await till.SendAsync(path + $"/cash-movement-requests/{requestId:D}/approve", foreignManager, new { });

        var reject = await till.SendAsync(path + $"/cash-movement-requests/{requestId:D}/reject", foreignManager, new { });
        var cancel = await till.SendAsync(path + $"/cash-movement-requests/{requestId:D}/cancel", foreignManager, new { });

        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, approve.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, reject.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
        Assert.Equal(2000m, await till.ExpectedCashAsync());
        Assert.Single((await till.ListAsync()).GetProperty("requests").EnumerateArray());
    }

    [Fact]
    public async Task ARequestOfAnotherSessionCannotBeDecidedThroughThisSessionsRoute()
    {
        await using var first = await TillAsync(opening: 2000m);
        await using var second = await TillAsync(opening: 2000m);
        var requestId = await first.RequestAsync(800m, "avans");
        var manager = await second.ManagerAsync("cross");

        foreach (var action in DecisionActions)
        {
            var cookie = action == "cancel" ? second.Cashier : manager.Cookie;
            await AssertErrorAsync(await second.DecideAsync(requestId, action, cookie), HttpStatusCode.NotFound, "CASH_MOVEMENT_REQUEST_NOT_FOUND", "bulunamadı");
            await AssertErrorAsync(await second.DecideAsync(Guid.NewGuid(), action, cookie), HttpStatusCode.NotFound, "CASH_MOVEMENT_REQUEST_NOT_FOUND", "bulunamadı");
        }

        Assert.Equal(2000m, await first.ExpectedCashAsync());
        Assert.Equal(2000m, await second.ExpectedCashAsync());
        Assert.Single((await first.ListAsync()).GetProperty("requests").EnumerateArray());
    }

    [Fact]
    public async Task ACashOutAboveBothTheThresholdAndTheDrawerIsRefusedAndNeverRequested()
    {
        await using var till = await TillAsync(opening: 700m);

        var response = await till.MoveAsync("Out", 900m, "tedarikçi");

        await AssertErrorAsync(response, HttpStatusCode.Conflict, "CASH_OUT_EXCEEDS_DRAWER", "kasadaki");
        Assert.Empty((await till.ListAsync()).GetProperty("requests").EnumerateArray());
    }

    [Fact]
    public async Task AnotherManagersApprovalIsNotASelfApprovalEvenWhileTheSettingIsOn()
    {
        await using var till = await TillAsync(opening: 2000m);
        await _database.SetSettingAsync(SelfApprovalKey, "Boolean", "true");
        var requestId = await till.RequestAsync(800m, "avans");
        var manager = await till.ManagerAsync("other-manager");

        var approved = await till.DecideAsync(requestId, "approve", manager.Cookie);

        Assert.False((await approved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("selfApproved").GetBoolean());
        var movement = Assert.Single((await till.ListAsync()).GetProperty("movements").EnumerateArray(), m => m.GetProperty("type").GetString() == "CashOut");
        Assert.False(movement.GetProperty("selfApproved").GetBoolean());
    }

    [Fact]
    public async Task ApprovalAcceptsExactlyWhatTheDrawerHoldsAndNotAKurusMore()
    {
        await using var till = await TillAsync(opening: 700m);
        var exact = await till.RequestAsync(600m, "tedarikçi");
        Assert.Equal(HttpStatusCode.Created, (await till.MoveAsync("Out", 100m, "gider")).StatusCode);
        var manager = await till.ManagerAsync("exact");

        var approved = await till.DecideAsync(exact, "approve", manager.Cookie);

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal(0m, await till.ExpectedCashAsync());
        var tooMuch = await till.MoveAsync("Out", 0.01m, "tedarikçi 2");
        await AssertErrorAsync(tooMuch, HttpStatusCode.Conflict, "CASH_OUT_EXCEEDS_DRAWER", "kasadaki");
    }

    [Fact]
    public async Task TheDatabaseRefusesAReasonlessMovementButOlderRowsStayReadable()
    {
        await using var till = await TillAsync(opening: 100m);
        Assert.False(await _database.IsReasonConstraintValidatedAsync());
        foreach (var notes in new[] { "NULL", "'  '", "'" + new string('x', 501) + "'" })
        {
            Assert.Equal("23514", await _database.SqlStateOfAsync(
                $"INSERT INTO cash.cash_transactions (cash_transaction_id, cash_session_id, type, direction, amount, notes, occurred_at) VALUES (gen_random_uuid(), '{till.SessionId}', 'CashOut', 'Out', 1, {notes}, now());"));
        }

        // A row written before the rule has no reason; it must still load (key replay reads it) instead of failing the rule.
        await _database.ExecuteAsync("ALTER TABLE cash.cash_transactions DROP CONSTRAINT ck_cash_transactions_movement_reason;");
        await _database.ExecuteAsync(
            $"INSERT INTO cash.cash_transactions (cash_transaction_id, cash_session_id, type, direction, amount, notes, occurred_at, idempotency_key) VALUES (gen_random_uuid(), '{till.SessionId}', 'CashIn', 'In', 5, NULL, now(), 'legacy-key');");

        var replay = await till.MoveAsync("In", 5m, "yeni açıklama", "legacy-key");
        var listing = await till.ListAsync();

        await AssertErrorAsync(replay, HttpStatusCode.Conflict, "IDEMPOTENCY_KEY_REUSED", "anahtar");
        Assert.Equal(JsonValueKind.Null, Assert.Single(listing.GetProperty("movements").EnumerateArray()).GetProperty("notes").ValueKind);
        Assert.Equal(105m, await till.ExpectedCashAsync());
    }

    [Fact]
    public async Task TheRequestTableRefusesInconsistentRows()
    {
        await using var till = await TillAsync(opening: 2000m);
        var user = await _database.UserIdForCookieAsync(till.Cashier);
        string Insert(string key, string reason, string status, string decidedBy, string decidedAt, string self, string transaction, string note) =>
            $"INSERT INTO cash.cash_movement_requests (request_id, cash_session_id, amount, reason, idempotency_key, status, requested_by, requested_at, decided_by, decided_at, self_approved, cash_transaction_id, decision_note) VALUES (gen_random_uuid(), '{till.SessionId}', 100, {reason}, '{key}', '{status}', '{user}', now(), {decidedBy}, {decidedAt}, {self}, {transaction}, {note});";

        Assert.Null(await _database.SqlStateOfAsync(Insert("ok", "'avans'", "Requested", "NULL", "NULL", "false", "NULL", "NULL")));
        Assert.Equal("23505", await _database.SqlStateOfAsync(Insert("ok", "'avans'", "Requested", "NULL", "NULL", "false", "NULL", "NULL")));
        Assert.Equal("23514", await _database.SqlStateOfAsync(Insert("k1", "'  '", "Requested", "NULL", "NULL", "false", "NULL", "NULL")));
        Assert.Equal("23514", await _database.SqlStateOfAsync(Insert("k2", "'avans'", "Approved", "NULL", "NULL", "false", "NULL", "NULL")));
        Assert.Equal("23514", await _database.SqlStateOfAsync(Insert("k3", "'avans'", "Requested", "NULL", "NULL", "true", "NULL", "NULL")));
        Assert.Equal("23514", await _database.SqlStateOfAsync(Insert("k4", "'avans'", "Rejected", $"'{user}'", "now()", "false", "NULL", "'  '")));
        Assert.Equal("23514", await _database.SqlStateOfAsync(Insert("k5", "'avans'", "Approved", $"'{user}'", "now()", "false", "NULL", "NULL")));

        var manager = await till.ManagerAsync("constraint");
        var approvedRequest = await till.RequestAsync(800m, "avans");
        await till.DecideAsync(approvedRequest, "approve", manager.Cookie);
        await using var command = _database.DataSource.CreateCommand(
            "SELECT cash_transaction_id FROM cash.cash_movement_requests WHERE request_id = @id;");
        command.Parameters.AddWithValue("id", approvedRequest);
        var ledgerRow = (Guid)(await command.ExecuteScalarAsync())!;
        Assert.Equal("23505", await _database.SqlStateOfAsync(Insert("k6", "'avans'", "Approved", $"'{user}'", "now()", "false", $"'{ledgerRow}'", "NULL")));
    }

    [Fact]
    public async Task TwoParallelCallsWithOneKeyRecordOneMovementOrOneRequest()
    {
        await using var till = await TillAsync(opening: 2000m);
        var directKey = Guid.NewGuid().ToString();
        var requestKey = Guid.NewGuid().ToString();

        var direct = await Task.WhenAll(till.MoveAsync("Out", 50m, "gider", directKey), till.MoveAsync("Out", 50m, "gider", directKey));
        var requests = await Task.WhenAll(till.MoveAsync("Out", 800m, "avans", requestKey), till.MoveAsync("Out", 800m, "avans", requestKey));

        Assert.All(direct, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        Assert.All(requests, r => Assert.Equal(HttpStatusCode.Accepted, r.StatusCode));
        Assert.Single(await Task.WhenAll(direct.Select(async r => (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid())).ContinueWith(t => t.Result.Distinct().ToArray()));
        Assert.Single(await Task.WhenAll(requests.Select(async r => (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid())).ContinueWith(t => t.Result.Distinct().ToArray()));
        Assert.Equal(1950m, await till.ExpectedCashAsync());
        Assert.Single((await till.ListAsync()).GetProperty("requests").EnumerateArray());
    }

    [Fact]
    public async Task ParallelDecisionsOnOneRequestApplyExactlyOnce()
    {
        await using var till = await TillAsync(opening: 2000m);
        var requestId = await till.RequestAsync(800m, "avans");
        var first = await till.ManagerAsync("parallel-1");
        var second = await till.ManagerAsync("parallel-2");

        var responses = await Task.WhenAll(
            till.DecideAsync(requestId, "approve", first.Cookie),
            till.DecideAsync(requestId, "approve", second.Cookie),
            till.DecideAsync(requestId, "cancel", till.Cashier));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(2, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        var approvedRows = await _database.CountLedgerRowsAsync(till.SessionId, "CashOut");
        Assert.True(approvedRows <= 1);
        Assert.Equal(2000m - (approvedRows * 800m), await till.ExpectedCashAsync());
    }

    [Fact]
    public async Task AnApprovalAndACashOutRunningTogetherNeverTakeMoreThanTheDrawerHolds()
    {
        await using var till = await TillAsync(opening: 700m);
        var requestId = await till.RequestAsync(600m, "tedarikçi");
        var manager = await till.ManagerAsync("together");

        var responses = await Task.WhenAll(till.DecideAsync(requestId, "approve", manager.Cookie), till.MoveAsync("Out", 300m, "gider"));

        Assert.Equal(1, responses.Count(r => r.IsSuccessStatusCode));
        var failed = responses.Single(r => !r.IsSuccessStatusCode);
        await AssertErrorAsync(failed, HttpStatusCode.Conflict, "CASH_OUT_EXCEEDS_DRAWER", "kasadaki");
        Assert.InRange(await till.ExpectedCashAsync(), 100m, 400m);
    }

    [Fact]
    public async Task AfterTheSessionLeftOpenApprovalIsRefusedButCancellingStillWorks()
    {
        await using var till = await TillAsync(opening: 2000m);
        var requestId = await till.RequestAsync(800m, "avans");
        var manager = await till.ManagerAsync("late-decider");
        await _database.SetSessionStatusAsync(till.SessionId, "Counting");

        var approve = await till.DecideAsync(requestId, "approve", manager.Cookie);
        var cancel = await till.DecideAsync(requestId, "cancel", till.Cashier);

        await AssertErrorAsync(approve, HttpStatusCode.Conflict, "CLOSED_CASH_SESSION", "açık değil");
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(0L, await _database.CountLedgerRowsAsync(till.SessionId, "CashOut"));
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    [InlineData("cancel")]
    public async Task ADecidedRequestAnswersConflictToEveryFurtherDecision(string firstDecision)
    {
        await using var till = await TillAsync(opening: 2000m);
        var manager = await till.ManagerAsync("again");
        var requestId = await till.RequestAsync(800m, "avans");
        Assert.Equal(HttpStatusCode.OK, (await till.DecideAsync(requestId, firstDecision, CookieFor(firstDecision, till, manager))).StatusCode);

        foreach (var action in DecisionActions)
        {
            await AssertErrorAsync(
                await till.DecideAsync(requestId, action, CookieFor(action, till, manager)),
                HttpStatusCode.Conflict, "CASH_MOVEMENT_REQUEST_STATE", "artık beklemiyor");
        }
    }

    [Fact]
    public async Task ARequestKeyReplaysOnlyForTheSameDirectionAmountAndTrimmedReason()
    {
        await using var till = await TillAsync(opening: 2000m);
        var manager = await till.ManagerAsync("keys");
        var key = Guid.NewGuid().ToString();
        var first = await till.MoveAsync("Out", 800m, "avans", key);
        var requestId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await AssertErrorAsync(await till.MoveAsync("Out", 801m, "avans", key), HttpStatusCode.Conflict, "IDEMPOTENCY_KEY_REUSED", "anahtar");
        await AssertErrorAsync(await till.MoveAsync("In", 800m, "avans", key), HttpStatusCode.Conflict, "IDEMPOTENCY_KEY_REUSED", "anahtar");
        var trimmed = await till.MoveAsync("Out", 800m, "  avans ", key);
        Assert.Equal(HttpStatusCode.Accepted, trimmed.StatusCode);
        Assert.Equal(requestId, (await trimmed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());

        await till.DecideAsync(requestId, "reject", manager.Cookie, new { Note = "hayır" });
        var afterRejection = await till.MoveAsync("Out", 800m, "avans", key);
        Assert.Equal(HttpStatusCode.OK, afterRejection.StatusCode);
        Assert.Equal("Rejected", (await afterRejection.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

        var directKey = Guid.NewGuid().ToString();
        var direct = await till.MoveAsync("Out", 50m, "gider", directKey);
        var directReplay = await till.MoveAsync("Out", 50m, "gider ", directKey);
        Assert.Equal(HttpStatusCode.Created, directReplay.StatusCode);
        Assert.Equal(
            (await direct.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(),
            (await directReplay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task TheAuditEventsNameWhoActedWhyAndTheStatesBeforeAndAfter()
    {
        await using var till = await TillAsync(opening: 2000m);
        var manager = await till.ManagerAsync("audit-content");
        var cashierId = await _database.UserIdForCookieAsync(till.Cashier);
        var requestId = await till.RequestAsync(800m, "avans");
        await till.DecideAsync(requestId, "reject", manager.Cookie, new { Note = "uygun değil" });

        var events = await _database.AuditEventsAsync(requestId);

        Assert.Equal(2, events.Length);
        Assert.Equal(new CashSessionHttpTestDatabase.AuditRow("cash.movement_request.requested", cashierId, "avans", "none", "Requested"), events[0]);
        Assert.Equal(new CashSessionHttpTestDatabase.AuditRow("cash.movement_request.rejected", manager.UserId, "uygun değil", "Requested", "Rejected"), events[1]);
    }

    [Theory]
    [InlineData("request")]
    [InlineData("approve")]
    [InlineData("reject")]
    [InlineData("cancel")]
    public async Task AFailedAuditWriteRollsBackTheWholeStep(string step)
    {
        await using var till = await TillAsync(opening: 2000m);
        var manager = await till.ManagerAsync("audit-rollback");
        var requestId = step == "request" ? Guid.Empty : await till.RequestAsync(800m, "avans");
        var eventName = step switch
        {
            "request" => "cash.movement_request.requested",
            "approve" => "cash.movement_request.approved",
            "reject" => "cash.movement_request.rejected",
            _ => "cash.movement_request.cancelled",
        };
        await _database.ExecuteAsync(
            $"""
            CREATE FUNCTION audit.rmd788_fail_audit() RETURNS trigger LANGUAGE plpgsql AS
            $$ BEGIN RAISE EXCEPTION 'audit write failed'; END $$;
            CREATE TRIGGER rmd788_fail_audit BEFORE INSERT ON audit.audit_events
            FOR EACH ROW WHEN (NEW.event_name = '{eventName}') EXECUTE FUNCTION audit.rmd788_fail_audit();
            """);
        try
        {
            var failed = step == "request"
                ? await till.MoveAsync("Out", 800m, "avans")
                : await till.DecideAsync(requestId, step, step == "cancel" ? till.Cashier : manager.Cookie);

            await AssertErrorAsync(failed, HttpStatusCode.InternalServerError, "INTERNAL_ERROR", "tamamlanamadı");
            Assert.DoesNotContain("audit write failed", await failed.Content.ReadAsStringAsync());
            var pending = (await till.ListAsync()).GetProperty("requests").EnumerateArray().ToArray();
            Assert.Equal(step == "request" ? 0 : 1, pending.Length);
            Assert.Equal(2000m, await till.ExpectedCashAsync());
            Assert.Equal(0L, await _database.CountLedgerRowsAsync(till.SessionId, "CashOut"));
        }
        finally
        {
            await _database.ExecuteAsync("DROP TRIGGER rmd788_fail_audit ON audit.audit_events; DROP FUNCTION audit.rmd788_fail_audit();");
        }
    }

    [Fact]
    public async Task TheThresholdSettingIsHonouredAtZeroAndFallsBackToTheDefaultWhenDeactivated()
    {
        await using var till = await TillAsync(opening: 2000m);
        await _database.SetSettingAsync(ThresholdKey, "Integer", "0");
        Assert.Equal(HttpStatusCode.Accepted, (await till.MoveAsync("Out", 0.01m, "gider")).StatusCode);

        await _database.SetSettingAsync(ThresholdKey, "Integer", "5000");
        Assert.Equal(HttpStatusCode.Created, (await till.MoveAsync("Out", 600m, "gider")).StatusCode);
        await _database.DeactivateSettingAsync(ThresholdKey);

        Assert.Equal(HttpStatusCode.Accepted, (await till.MoveAsync("Out", 600m, "gider 2")).StatusCode);
        Assert.Equal(500m, (await till.ListAsync()).GetProperty("approvalThreshold").GetDecimal());
    }

    [Fact]
    public async Task ADeactivatedSelfApprovalSettingCountsAsOff()
    {
        await using var till = await TillAsync(opening: 2000m);
        var manager = await till.ManagerAsync("inactive-switch");
        var requestId = await till.RequestAsync(800m, "avans", manager.Cookie);
        await _database.SetSettingAsync(SelfApprovalKey, "Boolean", "true");
        await _database.DeactivateSettingAsync(SelfApprovalKey);

        var refused = await till.DecideAsync(requestId, "approve", manager.Cookie);

        await AssertErrorAsync(refused, HttpStatusCode.Forbidden, "CASH_MOVEMENT_SELF_APPROVAL", "kendi");
        Assert.False((await till.ListAsync()).GetProperty("selfApprovalEnabled").GetBoolean());
    }

    [Fact]
    public async Task TheListHoldsTheNewestTwoHundredMovementsOnlyAndLeavesOtherEntryTypesOut()
    {
        await using var exact = await TillAsync(opening: 100m);
        await _database.InsertCashInsAsync(exact.SessionId, 200);
        var exactListing = await exact.ListAsync();
        Assert.Equal(200, exactListing.GetProperty("movements").GetArrayLength());
        Assert.False(exactListing.GetProperty("movementsTruncated").GetBoolean());

        await using var over = await TillAsync(opening: 100m);
        await _database.InsertCashInsAsync(over.SessionId, 201);
        var overListing = await over.ListAsync();
        var notes = overListing.GetProperty("movements").EnumerateArray().Select(m => m.GetProperty("notes").GetString()).ToArray();
        Assert.Equal("seed 201", notes[0]);
        Assert.DoesNotContain("seed 1", notes);
        Assert.True(overListing.GetProperty("movementsTruncated").GetBoolean());
    }

    private static string CookieFor(string action, Till till, Manager manager) => action == "cancel" ? till.Cashier : manager.Cookie;

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code, string messagePart)
    {
        Assert.Equal(status, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.Contains(messagePart, error.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task<Till> TillAsync(decimal opening)
    {
        var terminalId = Guid.NewGuid();
        var cashier = await _database.SeedCashierSessionAsync(terminalId, "till-" + Guid.NewGuid().ToString("N")[..8]);
        var app = DualScreenApplication.Build(new DualScreenOptions(
            _database.ConnectionString, _webRoot, "http://127.0.0.1:0", TrustedProxies: [System.Net.IPAddress.Loopback]));
        await app.StartAsync();
        var server = app.Services.GetRequiredService<IServer>();
        var client = new HttpClient { BaseAddress = new Uri(server.Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
        var till = new Till(app, client, _database, terminalId, cashier);
        var opened = await till.SendAsync($"/api/v1/terminals/{terminalId:D}/cash-sessions", cashier, new { OpeningBalance = opening });
        till.SessionId = (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cashSessionId").GetGuid();
        return till;
    }

    private sealed record Manager(string Cookie, Guid UserId);

    private sealed class Till(WebApplication app, HttpClient client, CashSessionHttpTestDatabase database, Guid terminalId, string cashier) : IAsyncDisposable
    {
        public Guid SessionId { get; set; }
        public string Cashier { get; } = cashier;
        private string SessionPath => $"/api/v1/terminals/{terminalId:D}/cash-sessions/{SessionId:D}";

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }

        public Task<string> Cookie(string token, params string[] permissions)
            => database.SeedCashierSessionWithPermissionsAsync(terminalId, token + "-" + Guid.NewGuid().ToString("N")[..6], permissions);

        public async Task<Manager> ManagerAsync(string token)
        {
            var cookie = await Cookie(token, ApplicationPermissions.CashDrawer, ApplicationPermissions.CashSessionOverride);
            return new Manager(cookie, await database.UserIdForCookieAsync(cookie));
        }

        public Task<HttpResponseMessage> SendAsync(string path, string cookie, object body)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            };
            return Send(request, cookie);
        }

        public Task<HttpResponseMessage> GetAsync(string path, string cookie) => Send(new HttpRequestMessage(HttpMethod.Get, path), cookie);

        public Task<HttpResponseMessage> PostAsync(string suffix, object body) => SendAsync(SessionPath + suffix, Cashier, body);

        public Task<HttpResponseMessage> MoveAsync(string direction, decimal amount, string? notes, string? key = null, string? cookie = null)
            => SendAsync(SessionPath + "/cash-movements", cookie ?? Cashier,
                new { Direction = direction, Amount = amount, Notes = notes, IdempotencyKey = key ?? Guid.NewGuid().ToString() });

        public async Task<Guid> RequestAsync(decimal amount, string reason, string? cookie = null)
        {
            var response = await MoveAsync("Out", amount, reason, cookie: cookie);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }

        public Task<HttpResponseMessage> DecideWithoutBodyAsync(Guid requestId, string action, string cookie)
            => Send(
                new HttpRequestMessage(HttpMethod.Post, SessionPath + $"/cash-movement-requests/{requestId:D}/{action}")
                {
                    Content = new StringContent(string.Empty, Encoding.UTF8, "application/json"),
                },
                cookie);

        public Task<HttpResponseMessage> DecideAsync(Guid requestId, string action, string cookie, object? body = null)
            => SendAsync(SessionPath + $"/cash-movement-requests/{requestId:D}/{action}", cookie, body ?? new { });

        public async Task<JsonElement> ListAsync(string? cookie = null)
        {
            var response = await GetAsync(SessionPath + "/cash-movements", cookie ?? Cashier);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }

        public async Task<decimal> ExpectedCashAsync()
        {
            var response = await GetAsync(SessionPath + "/expected-cash", Cashier);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("expectedCash").GetDecimal();
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
