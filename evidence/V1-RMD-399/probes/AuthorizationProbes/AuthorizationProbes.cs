using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Experience.Catalog;
using Microsoft.AspNetCore.Routing;
using Xunit;
using Xunit.Abstractions;

namespace ALKAROS.Audit.AuthorizationProbes;

/// <summary>
/// V1-RMD-399 probes. Every probe asserts what the system MUST do according to
/// docs/domain/authorization-model.md and the IAM task decisions; a failing probe is a finding.
/// Matrix row ids (A1..F3) refer to evidence/V1-RMD-399/coverage-matrix.md.
/// </summary>
public sealed partial class AuthorizationProbes : IClassFixture<ProbeHarness>
{
    private static readonly HttpStatusCode[] Rejected = [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden];
    private readonly ProbeHarness _h;
    private readonly ITestOutputHelper _output;

    public AuthorizationProbes(ProbeHarness harness, ITestOutputHelper output)
    {
        _h = harness;
        _output = output;
    }

    // ---- A: authentication and session lifecycle ----------------------------------------------------------

    [Fact]
    public async Task A1LoginLocksAccountAfterRepeatedFailures()
    {
        var (_, username) = await _h.SeedUserInRoleAsync("cashier");
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var (bad, _) = await _h.LoginAsync(Guid.NewGuid(), username, "wrong-password");
            Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        }

        var (afterLockout, cookies) = await _h.LoginAsync(Guid.NewGuid(), username, ProbeHarness.ProbePassword);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLockout.StatusCode);
        Assert.DoesNotContain(DualScreenApplication.CashierCookieName, cookies, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A2LogoutEndsSessionOnNextRequest()
    {
        var terminalId = Guid.NewGuid();
        var (_, username) = await _h.SeedUserInRoleAsync("cashier");
        var (login, cookies) = await _h.LoginAsync(terminalId, username, ProbeHarness.ProbePassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _h.GetAsync($"/api/v1/auth/session?terminalId={terminalId:D}", cookies)).StatusCode);

        var logout = await _h.PostAsync($"/api/v1/auth/logout?terminalId={terminalId:D}", cookies, new { });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _h.GetAsync($"/api/v1/auth/session?terminalId={terminalId:D}", cookies)).StatusCode);
    }

    [Fact]
    public async Task A3LoginSessionLivesAtMostEightHours()
    {
        // V1-IAM-031 (Semih, 2026-09-15): a device session lives 8 hours, not 12.
        var terminalId = Guid.NewGuid();
        var (userId, username) = await _h.SeedUserInRoleAsync("waiter");
        var (login, _) = await _h.LoginAsync(terminalId, username, ProbeHarness.ProbePassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var lifetimeHours = await _h.ScalarAsync<decimal>(
            """
            SELECT EXTRACT(EPOCH FROM (expires_at - now())) / 3600.0
            FROM identity.device_sessions WHERE user_id = @id AND device_id = @device AND revoked_at IS NULL;
            """,
            ("id", userId), ("device", $"cashier:{terminalId:D}"));
        _output.WriteLine($"cashier-device session lifetime after real login: {lifetimeHours:F2} h");
        Assert.True(lifetimeHours <= 8.05m, $"Session issued by /api/v1/auth/login lives {lifetimeHours:F2} h; V1-IAM-031 decided 8 h.");
    }

    [Fact]
    public async Task A4DisplaySessionCannotMutateCashierResources()
    {
        var terminalId = Guid.NewGuid();
        var displayCookie = await _h.SeedDisplaySessionAsync(terminalId);
        var response = await _h.PostAsync(
            $"/api/v1/terminals/{terminalId:D}/cash-sessions", displayCookie, new { OpeningBalance = 100m });
        Assert.Contains(response.StatusCode, Rejected);
    }

    [Fact]
    public async Task A5ManagerRevokeSessionsEndsVictimSessionOnNextRequest()
    {
        var managerTerminal = Guid.NewGuid();
        var (_, managerName) = await _h.SeedUserInRoleAsync("manager");
        var (managerLogin, managerCookies) = await _h.LoginAsync(managerTerminal, managerName, ProbeHarness.ProbePassword);
        Assert.Equal(HttpStatusCode.OK, managerLogin.StatusCode);

        var victimTerminal = Guid.NewGuid();
        var (victimId, victimName) = await _h.SeedUserInRoleAsync("waiter");
        var (_, victimCookies) = await _h.LoginAsync(victimTerminal, victimName, ProbeHarness.ProbePassword);
        Assert.Equal(HttpStatusCode.OK, (await _h.GetAsync($"/api/v1/auth/session?terminalId={victimTerminal:D}", victimCookies)).StatusCode);

        var revoke = await _h.PostAsync($"/api/v1/management/security/users/{victimId:D}/revoke-sessions", managerCookies, new { });
        _output.WriteLine($"revoke-sessions by a real manager login: {(int)revoke.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _h.GetAsync($"/api/v1/auth/session?terminalId={victimTerminal:D}", victimCookies)).StatusCode);
    }

    [Fact]
    public async Task A5DeactivatedUserSessionIsRejected()
    {
        var terminalId = Guid.NewGuid();
        var (userId, cookie) = await _h.SeedCashierAsync(terminalId, "orders.create");
        Assert.Equal(HttpStatusCode.OK, (await _h.GetAsync($"/api/v1/auth/session?terminalId={terminalId:D}", cookie)).StatusCode);

        await _h.ExecuteAsync("UPDATE identity.users SET active = false WHERE user_id = @id;", ("id", userId));
        Assert.Equal(HttpStatusCode.Unauthorized, (await _h.GetAsync($"/api/v1/auth/session?terminalId={terminalId:D}", cookie)).StatusCode);
    }

    // ---- B: role and permission catalog ---------------------------------------------------------------------

    [Fact]
    public async Task B1SeededRoleMatrixMatchesDecisionRecord()
    {
        // docs/domain/authorization-model.md §3 (FOH roles; "grant" = not held outright), §3.1, §3.2.
        string[] foh = ["orders.create", "orders.send", "tables.status", "tables.reserve", "tables.transfer", "tables.merge",
            "floorplan.manage", "bills.split", "bills.void", "bills.comp", "bills.discount", "cash.drawer", "reports.view",
            "catalog.manage", "kitchen.advance"];
        var expected = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["waiter"] = ["orders.create", "orders.send", "tables.status", "kitchen.advance"],
            ["cashier"] = ["orders.create", "orders.send", "tables.status", "tables.reserve", "tables.transfer", "tables.merge",
                "bills.split", "cash.drawer", "kitchen.advance"],
            ["supervisor"] = ["orders.create", "orders.send", "tables.status", "tables.reserve", "tables.transfer", "tables.merge",
                "floorplan.manage", "bills.split", "bills.void", "bills.comp", "bills.discount", "cash.drawer", "reports.view",
                "kitchen.advance"],
            ["manager"] = foh,
            ["kitchen-staff"] = ["kitchen.advance"],
            ["kitchen-chef"] = ["orders.send", "kitchen.advance"],
        };

        var mismatches = new List<string>();
        foreach (var (role, held) in expected)
        {
            var actual = await HeldAsync(role);
            foreach (var code in foh)
            {
                var should = held.Contains(code, StringComparer.Ordinal);
                var does = actual.Contains(code);
                if (should != does)
                    mismatches.Add($"{role}: {code} expected {(should ? "held" : "not held")}, seed says {(does ? "held" : "not held")}");
            }
        }

        var kitchenStaff = await HeldAsync("kitchen-staff");
        if (kitchenStaff.Count != 1)
            mismatches.Add($"kitchen-staff holds [{string.Join(", ", kitchenStaff.Order(StringComparer.Ordinal))}]; §3.1 says only kitchen.advance");

        foreach (var mismatch in mismatches)
            _output.WriteLine(mismatch);
        Assert.Empty(mismatches);
    }

    [Fact]
    public async Task B2RevokedPermissionTakesEffectOnNextRequest()
    {
        var terminalId = Guid.NewGuid();
        var userId = await _h.SeedUserWithCustomRoleAsync("reports.view");
        var managerCookie = await _h.SeedSessionAsync(userId, $"supervisor:{terminalId:D}", CatalogManagementEndpoints.ManagerCookieName);
        Assert.Equal(HttpStatusCode.OK, (await _h.GetAsync("/api/v1/management/authorization/pending-grants", managerCookie)).StatusCode);

        await _h.ExecuteAsync(
            """
            DELETE FROM identity.role_permissions rp USING identity.user_roles ur, identity.permissions p
            WHERE rp.role_id = ur.role_id AND ur.user_id = @id AND rp.permission_id = p.permission_id AND p.code = 'reports.view';
            """,
            ("id", userId));
        Assert.Equal(HttpStatusCode.Forbidden, (await _h.GetAsync("/api/v1/management/authorization/pending-grants", managerCookie)).StatusCode);
    }

    // ---- C: grants, delegation, four-eyes, offline authority -----------------------------------------------

    [Fact]
    public async Task C1RequesterCannotApproveOwnGrant()
    {
        var terminalId = Guid.NewGuid();
        var managerId = await _h.SeedUserWithCustomRoleAsync("reports.view", "bills.comp");
        var managerCookie = await _h.SeedSessionAsync(managerId, $"manager:{terminalId:D}", CatalogManagementEndpoints.ManagerCookieName);
        var grantId = Guid.NewGuid();
        await _h.ExecuteAsync(
            """
            INSERT INTO identity.authorization_grants (grant_id, idempotency_key, permission_code, requester_user_id, requester_role_code, amount, reason_code)
            VALUES (@grant, @key, 'bills.comp', @requester, 'waiter', 100, 'CustomerSatisfaction');
            """,
            ("grant", grantId), ("key", "rmd399-" + Guid.NewGuid().ToString("N")), ("requester", managerId));

        var approve = await _h.PostAsync($"/api/v1/management/authorization/grants/{grantId:D}/approve", managerCookie, new { });
        _output.WriteLine($"self-approve status: {(int)approve.StatusCode}");
        Assert.False(approve.IsSuccessStatusCode);
        Assert.Equal("pending", await _h.ScalarAsync<string>("SELECT status FROM identity.authorization_grants WHERE grant_id = @id;", ("id", grantId)));
    }

    [Fact]
    public async Task C3WaiterCompOnAnotherWaitersCheckIsRefused()
    {
        var terminalId = Guid.NewGuid();
        var (otherWaiter, _) = await _h.SeedUserInRoleAsync("waiter");
        var (_, username) = await _h.SeedUserInRoleAsync("waiter");
        var (_, cookies) = await _h.LoginAsync(terminalId, username, ProbeHarness.ProbePassword);
        var (orderId, itemId, rowVersion) = await _h.SeedOrderAsync(otherWaiter);

        var comp = await CompAsync(terminalId, orderId, itemId, rowVersion, cookies);
        Assert.Equal(HttpStatusCode.Forbidden, comp.StatusCode);
    }

    [Fact]
    public async Task C3WaiterCompOnUnassignedCheckIsNotEscalated()
    {
        // Model §3 decision 1: a waiter may comp only a check they serve; a check they do not serve never reaches a manager.
        var terminalId = Guid.NewGuid();
        var (_, username) = await _h.SeedUserInRoleAsync("waiter");
        var (_, cookies) = await _h.LoginAsync(terminalId, username, ProbeHarness.ProbePassword);
        var (orderId, itemId, rowVersion) = await _h.SeedOrderAsync(servingUserId: null);

        var comp = await CompAsync(terminalId, orderId, itemId, rowVersion, cookies);
        var grant = await _h.ScalarAsync<string>(
            "SELECT status || '/' || COALESCE(policy_path, '-') FROM identity.authorization_grants WHERE subject_id = @id;", ("id", itemId));
        _output.WriteLine($"waiter comp on an unassigned check: HTTP {(int)comp.StatusCode}, grant {grant}");
        Assert.StartsWith("denied/", grant, StringComparison.Ordinal);
    }

    [Fact]
    public void C4DelegationCanBeCreatedThroughSomeRoute()
    {
        // Model §1/§4 step 2 and V1-IAM-021: a manager grants a time-boxed delegation. Some HTTP route must create one.
        var creators = Routes()
            .Where(route => route.Methods.Contains("POST") && route.Pattern.Contains("delegation", StringComparison.OrdinalIgnoreCase)
                && !route.Pattern.EndsWith("/revoke", StringComparison.Ordinal))
            .Select(route => route.Pattern)
            .ToList();
        _output.WriteLine($"delegation-creating routes: [{string.Join(", ", creators)}]");
        Assert.NotEmpty(creators);
    }

    [Fact]
    public async Task C5UserCannotClearOwnTightening()
    {
        var terminalId = Guid.NewGuid();
        var managerId = await _h.SeedUserWithCustomRoleAsync("reports.view");
        var managerCookie = await _h.SeedSessionAsync(managerId, $"manager:{terminalId:D}", CatalogManagementEndpoints.ManagerCookieName);
        var tighteningId = Guid.NewGuid();
        await _h.ExecuteAsync(
            """
            INSERT INTO identity.behavioural_tightenings (tightening_id, user_id, permission_code, recent_count, baseline_per_window, trigger_ratio)
            VALUES (@id, @user, 'bills.comp', 9, 1, 3);
            """,
            ("id", tighteningId), ("user", managerId));

        var clear = await _h.PostAsync($"/api/v1/management/authorization/behavioural-tightenings/{tighteningId:D}/clear", managerCookie, new { });
        Assert.False(clear.IsSuccessStatusCode);
        Assert.Equal(0L, await _h.ScalarAsync<long>(
            "SELECT count(*) FROM identity.behavioural_tightenings WHERE tightening_id = @id AND cleared_at IS NOT NULL;", ("id", tighteningId)));
    }

    [Fact]
    public async Task C6SupervisorLoginReachesDecisionSurface()
    {
        // Model §4 step 3 + V1-IAM-020: pending grants go to every on-shift manager / supervisor device.
        var terminalId = Guid.NewGuid();
        var (_, username) = await _h.SeedUserInRoleAsync("supervisor");
        var (login, cookies) = await _h.LoginAsync(terminalId, username, ProbeHarness.ProbePassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var pending = await _h.GetAsync("/api/v1/management/authorization/pending-grants", cookies);
        _output.WriteLine($"supervisor (real login) -> pending-grants: {(int)pending.StatusCode}; cookies set: {string.Join(", ", cookies.Split("; ").Select(c => c.Split('=')[0]))}");
        Assert.Equal(HttpStatusCode.OK, pending.StatusCode);
    }

    [Fact]
    public async Task C7OfflineReconciliationUsesTheCallersRealRole()
    {
        // Model §5: every offline action is re-validated against the LIVE policy of the requester.
        await _h.ExecuteAsync(
            """
            INSERT INTO identity.authorization_policies (permission_code, role_code, mode, limit_amount, max_count, window_seconds)
            VALUES ('bills.void', 'waiter', 'auto_within', 500, 5, 3600)
            ON CONFLICT (permission_code, role_code) DO UPDATE SET mode = 'auto_within', limit_amount = 500, max_count = 5, window_seconds = 3600;
            """);
        var terminalId = Guid.NewGuid();
        var (userId, username) = await _h.SeedUserInRoleAsync("waiter");
        var (login, cookies) = await _h.LoginAsync(terminalId, username, ProbeHarness.ProbePassword);
        var budgetId = (await ProbeHarness.JsonAsync(login)).GetProperty("offlineBudget").GetProperty("budgetId").GetGuid();

        // The manager now tightens the waiter policy to always_deny; the device reconnects afterwards.
        await _h.ExecuteAsync(
            "UPDATE identity.authorization_policies SET mode = 'always_deny', limit_amount = NULL, max_count = NULL, window_seconds = NULL WHERE permission_code = 'bills.void' AND role_code = 'waiter';");

        var honest = await ReconcileAsync(terminalId, cookies, budgetId, userId, "waiter", "bills.void", subjectServingUserId: userId);
        var forged = await ReconcileAsync(terminalId, cookies, budgetId, userId, "manager", "bills.void", subjectServingUserId: userId);
        await _h.ExecuteAsync("DELETE FROM identity.authorization_policies WHERE permission_code = 'bills.void' AND role_code = 'waiter';");
        _output.WriteLine($"honest role -> {honest}; forged role 'manager' -> {forged}");
        Assert.Equal("Denied", honest);
        Assert.Equal("Denied", forged);
    }

    [Fact]
    public async Task C7OfflineReconciliationAppliesOwnCheck()
    {
        await _h.ExecuteAsync(
            """
            INSERT INTO identity.authorization_policies (permission_code, role_code, mode, limit_amount, max_count, window_seconds)
            VALUES ('bills.comp', 'waiter', 'auto_within', 500, 5, 3600)
            ON CONFLICT (permission_code, role_code) DO UPDATE SET mode = 'auto_within', limit_amount = 500, max_count = 5, window_seconds = 3600;
            """);
        var terminalId = Guid.NewGuid();
        var (otherWaiter, _) = await _h.SeedUserInRoleAsync("waiter");
        var (userId, username) = await _h.SeedUserInRoleAsync("waiter");
        var (login, cookies) = await _h.LoginAsync(terminalId, username, ProbeHarness.ProbePassword);
        var budgetId = (await ProbeHarness.JsonAsync(login)).GetProperty("offlineBudget").GetProperty("budgetId").GetGuid();

        var status = await ReconcileAsync(terminalId, cookies, budgetId, userId, "waiter", "bills.comp", subjectServingUserId: otherWaiter);
        await _h.ExecuteAsync("DELETE FROM identity.authorization_policies WHERE permission_code = 'bills.comp' AND role_code = 'waiter';");
        _output.WriteLine($"offline comp on another waiter's check -> {status}");
        Assert.Equal("Denied", status);
    }

    [Fact]
    public async Task C7OfflineBudgetOfAnotherUserIsRejected()
    {
        var terminalId = Guid.NewGuid();
        var (_, ownerName) = await _h.SeedUserInRoleAsync("waiter");
        var (ownerLogin, _) = await _h.LoginAsync(Guid.NewGuid(), ownerName, ProbeHarness.ProbePassword);
        var ownerBudget = (await ProbeHarness.JsonAsync(ownerLogin)).GetProperty("offlineBudget").GetProperty("budgetId").GetGuid();

        var (attackerId, attackerName) = await _h.SeedUserInRoleAsync("waiter");
        var (_, cookies) = await _h.LoginAsync(terminalId, attackerName, ProbeHarness.ProbePassword);
        var response = await _h.PostAsync($"/api/v1/terminals/{terminalId:D}/offline-reconciliation", cookies, new
        {
            BudgetId = ownerBudget,
            Actions = new[] { OfflineAction(attackerId, "waiter", "bills.comp", attackerId) },
        });
        Assert.False(response.IsSuccessStatusCode);
    }

    // ---- D: endpoint permission and session type -----------------------------------------------------------

    // Routes a JSON "{}" request cannot reach (multipart body, required query parameter, validation before the
    // service-level permission check). D1SecondPass / D2SecondPass send well-formed requests to each of them.
    private static readonly HashSet<string> SecondPassRoutes = new(StringComparer.Ordinal)
    {
        "PUT /api/v1/management/business-identity/logo",
        "PUT /api/v1/management/customer-display/screensaver",
        "DELETE /api/v1/terminals/{terminalId:guid}/orders/{orderId:guid}/items/{itemId:guid}",
        "DELETE /api/v1/terminals/{terminalId:guid}/push/subscriptions",
        "POST /api/v1/management/roles/permissions",
        "POST /api/v1/management/roles/roles",
        "POST /api/v1/management/roles/roles/{roleId:guid}/permissions",
        "POST /api/v1/management/users/",
    };

    [Fact]
    public async Task D1EveryNonPublicRouteRejectsAnonymousCaller()
    {
        // Public by design: health, login, display pairing hand-shake, customer QR/NFC surfaces, signed platform webhooks.
        string[] publicPrefixes = ["/health", "/api/v1/auth/login", "/api/v1/customer-displays/pairing-requests",
            "/api/v1/qr", "/api/v1/nfc", "/api/v1/integrations/", "/api/{**path}"];
        var open = new List<string>();
        foreach (var route in Routes())
        {
            foreach (var method in route.Methods)
            {
                var response = await _h.SendAsync(new HttpMethod(method), Materialize(route.Pattern, Guid.NewGuid()), null, method == "GET" ? null : new { });
                var status = (int)response.StatusCode;
                var isPublic = publicPrefixes.Any(prefix => route.Pattern.StartsWith(prefix, StringComparison.Ordinal));
                var secondPass = SecondPassRoutes.Contains($"{method} {route.Pattern}");
                _output.WriteLine($"D1\t{method}\t{route.Pattern}\t{status}\t{(isPublic ? "public" : secondPass ? "second-pass" : "private")}");
                if (!isPublic && !secondPass && status is not (401 or 403))
                    open.Add($"{method} {route.Pattern} -> {status}");
            }
        }

        foreach (var entry in open)
            _output.WriteLine("D1-OPEN\t" + entry);
        Assert.Empty(open);
    }

    [Fact]
    public async Task D2EveryMutationRouteRejectsPermissionlessSession()
    {
        // Session-only by design: the caller's own session/PIN, display pairing and display revoke
        // (V1-IAM-024 "customer-display pairing ile display-session revoke yalnız oturuma"), grant-class
        // requests that must reach the policy engine (comp: V1-BIL-005), and offline reconciliation (V1-IAM-025 C3).
        // Grant-class routes: the caller's own permission decides between "apply" and "raise a grant" (model §3/§4:
        // comp V1-BIL-005, discount V1-IAM-026, void-sent V1-IAM-027). Own-scope routes act only on the caller's
        // own device or note (push subscription V1-WTR-009, handoff note V1-RMD-110, help request V1-RMD-289).
        string[] sessionOnly = ["/api/v1/auth/", "/api/v1/terminals/{terminalId:guid}/pairings/approve",
            "/api/v1/terminals/{terminalId:guid}/display-sessions/revoke", "/items/{itemId:guid}/comp",
            "/offline-reconciliation", "/items/{itemId:guid}/void-sent", "/billing/bills/{billId:guid}/discount",
            "/push/subscriptions", "/orders/handoff-note/pop", "/help-requests"];
        string[] publicPrefixes = ["/health", "/api/v1/auth/login", "/api/v1/customer-displays/", "/api/v1/qr", "/api/v1/nfc",
            "/api/v1/integrations/", "/api/{**path}"];
        var passed = new List<string>();
        foreach (var route in Routes())
        {
            if (publicPrefixes.Any(prefix => route.Pattern.StartsWith(prefix, StringComparison.Ordinal)))
                continue;
            foreach (var method in route.Methods.Where(method => method != "GET"))
            {
                var terminalId = Guid.NewGuid();
                var (userId, cashierCookie) = await _h.SeedCashierAsync(terminalId);
                var managerCookie = await _h.SeedSessionAsync(userId, $"manager:{terminalId:D}", CatalogManagementEndpoints.ManagerCookieName);
                var response = await _h.SendAsync(new HttpMethod(method), Materialize(route.Pattern, terminalId),
                    $"{cashierCookie}; {managerCookie}", new { });
                var status = (int)response.StatusCode;
                var documented = sessionOnly.Any(marker => route.Pattern.Contains(marker, StringComparison.Ordinal));
                var secondPass = SecondPassRoutes.Contains($"{method} {route.Pattern}");
                _output.WriteLine($"D2\t{method}\t{route.Pattern}\t{status}\t{(documented ? "session-only-by-design" : secondPass ? "second-pass" : "must-need-permission")}");
                if (!documented && !secondPass && status is not (401 or 403))
                    passed.Add($"{method} {route.Pattern} -> {status}");
            }
        }

        foreach (var entry in passed)
            _output.WriteLine("D2-PASSED-GUARD\t" + entry);
        Assert.Empty(passed);
    }

    [Theory]
    [InlineData("waiter")]
    [InlineData("kitchen-staff")]
    public async Task D3RoleWithoutCashDrawerCannotOperateTheDrawer(string roleCode)
    {
        // Model §2/§3: cash.drawer guards drawer open / count; waiter ❌, kitchen-staff holds only kitchen.advance.
        var terminalId = Guid.NewGuid();
        var (_, username) = await _h.SeedUserInRoleAsync(roleCode);
        var (login, cookies) = await _h.LoginAsync(terminalId, username, ProbeHarness.ProbePassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var open = await _h.PostAsync($"/api/v1/terminals/{terminalId:D}/cash-sessions", cookies, new { OpeningBalance = 100m });
        _output.WriteLine($"{roleCode} opens a cash session: {(int)open.StatusCode}");
        Assert.Contains(open.StatusCode, Rejected);
    }

    [Fact]
    public async Task D3WaiterCannotReserveOrSplit()
    {
        var terminalId = Guid.NewGuid();
        var (_, username) = await _h.SeedUserInRoleAsync("waiter");
        var (_, cookies) = await _h.LoginAsync(terminalId, username, ProbeHarness.ProbePassword);

        var reserve = await _h.PostAsync($"/api/v1/terminals/{terminalId:D}/table-management/reservations", cookies, new { });
        var split = await _h.SendAsync(HttpMethod.Put, $"/api/v1/terminals/{terminalId:D}/billing/bills/{Guid.NewGuid():D}/split-design/equal", cookies, new { });
        Assert.Contains(reserve.StatusCode, Rejected);
        Assert.Contains(split.StatusCode, Rejected);
    }

    [Fact]
    public async Task D1SecondPassBindingArtifactsAreGuarded()
    {
        // D1 first pass could not reach these handlers (multipart body / required query parameter); send a well-formed request.
        var terminalId = Guid.NewGuid();
        var statuses = new List<string>();
        foreach (var path in new[] { "/api/v1/management/business-identity/logo", "/api/v1/management/customer-display/screensaver" })
            statuses.Add($"PUT {path} -> {(int)(await UploadPngAsync(path, null)).StatusCode}");

        statuses.Add($"DELETE item -> {(int)(await _h.SendAsync(HttpMethod.Delete,
            $"/api/v1/terminals/{terminalId:D}/orders/{Guid.NewGuid():D}/items/{Guid.NewGuid():D}?expectedRevision=1", null)).StatusCode}");
        statuses.Add($"DELETE push -> {(int)(await _h.SendAsync(HttpMethod.Delete,
            $"/api/v1/terminals/{terminalId:D}/push/subscriptions?endpoint=https%3A%2F%2Fpush.example%2Fx", null)).StatusCode}");
        foreach (var status in statuses)
            _output.WriteLine(status);
        Assert.All(statuses, status => Assert.True(status.EndsWith("401", StringComparison.Ordinal) || status.EndsWith("403", StringComparison.Ordinal), status));
    }

    [Fact]
    public async Task D2SecondPassPermissionlessSessionIsRefused()
    {
        // D2 first pass hit request validation (400) before the service-level permission check; send valid bodies.
        var terminalId = Guid.NewGuid();
        var userId = await _h.SeedUserWithCustomRoleAsync();
        var managerCookie = await _h.SeedSessionAsync(userId, $"manager:{terminalId:D}", CatalogManagementEndpoints.ManagerCookieName);
        var managerRole = await _h.ScalarAsync<Guid>("SELECT role_id FROM identity.roles WHERE code = 'manager';");
        var calls = new (string Path, object Body)[]
        {
            ("/api/v1/management/roles/permissions", new { Code = "rmd399.probe", Name = "Probe" }),
            ("/api/v1/management/roles/roles", new { Code = "rmd399-probe", Name = "Probe" }),
            ($"/api/v1/management/roles/roles/{managerRole:D}/permissions", new { PermissionCode = "catalog.manage" }),
            ($"/api/v1/management/roles/roles/{managerRole:D}/users/{userId:D}", new { }),
            ("/api/v1/management/users", new { Username = "rmd399-new", Password = "Probe-Password-399", DisplayName = "Probe" }),
        };
        var statuses = new List<string>();
        foreach (var (path, body) in calls)
            statuses.Add($"{path} -> {(int)(await _h.PostAsync(path, managerCookie, body)).StatusCode}");
        var (_, cashierCookie) = await _h.SeedCashierAsync(terminalId);
        statuses.Add($"DELETE item -> {(int)(await _h.SendAsync(HttpMethod.Delete,
            $"/api/v1/terminals/{terminalId:D}/orders/{Guid.NewGuid():D}/items/{Guid.NewGuid():D}?expectedRevision=1", cashierCookie)).StatusCode}");
        foreach (var path in new[] { "/api/v1/management/business-identity/logo", "/api/v1/management/customer-display/screensaver" })
            statuses.Add($"PUT {path} -> {(int)(await UploadPngAsync(path, managerCookie)).StatusCode}");
        foreach (var status in statuses)
            _output.WriteLine(status);
        Assert.All(statuses, status => Assert.EndsWith("403", status, StringComparison.Ordinal));
        Assert.Equal(0L, await _h.ScalarAsync<long>(
            "SELECT count(*) FROM identity.user_roles WHERE user_id = @id AND role_id = @role;", ("id", userId), ("role", managerRole)));
    }

    // ---- E: resource ownership -----------------------------------------------------------------------------

    [Fact]
    public async Task E1SessionForTerminalACannotActOnTerminalB()
    {
        var terminalA = Guid.NewGuid();
        var (_, cookie) = await _h.SeedCashierAsync(terminalA, "orders.create", "cash.drawer");
        var terminalB = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Unauthorized, (await _h.GetAsync($"/api/v1/terminals/{terminalB:D}/orders/active", cookie)).StatusCode);
        var open = await _h.PostAsync($"/api/v1/terminals/{terminalB:D}/cash-sessions", cookie, new { OpeningBalance = 10m });
        Assert.Contains(open.StatusCode, Rejected);
    }

    // ---- helpers -------------------------------------------------------------------------------------------

    private async Task<HashSet<string>> HeldAsync(string roleCode)
    {
        var held = new HashSet<string>(StringComparer.Ordinal);
        await using var command = _h.DataSource.CreateCommand(
            """
            SELECT p.code FROM identity.roles r
            JOIN identity.role_permissions rp ON rp.role_id = r.role_id
            JOIN identity.permissions p ON p.permission_id = rp.permission_id
            WHERE r.code = @code;
            """);
        command.Parameters.AddWithValue("code", roleCode);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            held.Add(reader.GetString(0));
        return held;
    }

    private async Task<HttpResponseMessage> UploadPngAsync(string path, string? cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, path);
        var form = new MultipartFormDataContent();
        var png = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        png.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(png, "file", "probe.png");
        request.Content = form;
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.77");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        if (cookie is not null)
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return await _h.Client.SendAsync(request);
    }

    private Task<HttpResponseMessage> CompAsync(Guid terminalId, Guid orderId, Guid itemId, long rowVersion, string cookies)
        => _h.PostAsync($"/api/v1/terminals/{terminalId:D}/orders/{orderId:D}/items/{itemId:D}/comp", cookies, new
        {
            IdempotencyKey = "rmd399-" + Guid.NewGuid().ToString("N"),
            ExpectedRowVersion = rowVersion,
            ReasonCode = "CustomerSatisfaction",
        });

    private static object OfflineAction(Guid requester, string roleCode, string permission, Guid servingUserId) => new
    {
        IdempotencyKey = "rmd399-offline-" + Guid.NewGuid().ToString("N"),
        PermissionCode = permission,
        RequesterUserId = requester,
        RequesterRoleCode = roleCode,
        ReasonCode = "CustomerSatisfaction",
        Amount = 50m,
        OfflineAuthorizedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        SubjectType = "OrderItem",
        SubjectId = Guid.NewGuid(),
        SubjectServingUserId = servingUserId,
    };

    private async Task<string> ReconcileAsync(
        Guid terminalId, string cookies, Guid budgetId, Guid requester, string roleCode, string permission, Guid subjectServingUserId)
    {
        var response = await _h.PostAsync($"/api/v1/terminals/{terminalId:D}/offline-reconciliation", cookies, new
        {
            BudgetId = budgetId,
            Actions = new[] { OfflineAction(requester, roleCode, permission, subjectServingUserId) },
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"reconciliation failed: {(int)response.StatusCode} {body}");
        using var document = JsonDocument.Parse(body);
        var results = document.RootElement.GetProperty("results");
        return results[0].GetProperty("status").ToString();
    }

    private sealed record RouteInfo(string Pattern, IReadOnlyList<string> Methods);

    private List<RouteInfo> Routes()
        => ((IEndpointRouteBuilder)_h.App).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => (Pattern: "/" + (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/'),
                Methods: endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods))
            .Where(route => route.Methods is { Count: > 0 }
                && (route.Pattern.StartsWith("/api/", StringComparison.Ordinal) || route.Pattern.StartsWith("/health", StringComparison.Ordinal)))
            .Select(route => new RouteInfo(route.Pattern, route.Methods!.ToList()))
            .OrderBy(route => route.Pattern, StringComparer.Ordinal)
            .ToList();

    private static string Materialize(string pattern, Guid terminalId)
    {
        var path = ParameterPattern().Replace(pattern, match =>
        {
            var name = match.Groups["name"].Value;
            var constraint = match.Groups["constraint"].Value;
            if (name == "terminalId")
                return terminalId.ToString("D");
            if (constraint.Contains("guid", StringComparison.Ordinal))
                return Guid.NewGuid().ToString("D");
            if (constraint.Contains("int", StringComparison.Ordinal))
                return "1";
            return name.Contains("date", StringComparison.OrdinalIgnoreCase) ? "2026-09-28" : "probe";
        });
        var separator = path.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return new StringBuilder(path).Append(separator).Append("terminalId=").Append(terminalId.ToString("D")).ToString();
    }

    [GeneratedRegex(@"\{\*?(?<name>[A-Za-z0-9_]+)(?::(?<constraint>[^}]+))?\}")]
    private static partial Regex ParameterPattern();
}
