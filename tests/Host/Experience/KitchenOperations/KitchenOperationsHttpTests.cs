using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.Experience.KitchenOperations;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Settings.KitchenLiveSync;
using ALKAROS.Settings.TypedSettings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ALKAROS.Host.Experience.KitchenOperations.Tests;

[Collection("Kitchen operations PostgreSQL HTTP")]
public sealed class KitchenOperationsHttpTests : IAsyncLifetime
{
    private readonly KitchenOperationsTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task SessionAndCapabilityBoundariesReturn401And403()
    {
        var terminalId = Guid.NewGuid();
        var readOnlyCookie = await _database.SeedSessionAsync(terminalId, []);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var missing = await client.GetAsync(Prefix(terminalId) + "/tickets?stationId=hot-line");
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

        using var readableRequest = Request(
            HttpMethod.Get,
            Prefix(terminalId) + "/tickets?stationId=hot-line",
            readOnlyCookie);
        using var readable = await client.SendAsync(readableRequest);
        Assert.Equal(HttpStatusCode.OK, readable.StatusCode);

        var seed = await _database.SeedKitchenGraphAsync();
        using var deniedRequest = JsonRequest(
            HttpMethod.Post,
            $"{Prefix(terminalId)}/tickets/{seed.TicketId:D}/transition",
            readOnlyCookie,
            new TransitionKitchenTicketV1("Accepted", 1));
        using var denied = await client.SendAsync(deniedRequest);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var error = await denied.Content.ReadFromJsonAsync<KitchenOperationsErrorEnvelopeV1>();
        Assert.Equal("FORBIDDEN", error!.Error.Code);
        // V1-IAM-028: a non-Cancelled ticket transition ("Accepted" here) now
        // checks kitchen.advance, not orders.send — the permission split's
        // whole point is that these are no longer the same check.
        Assert.Equal(
            1L,
            await _database.ScalarAsync<long>(
                "SELECT count(*) FROM identity.denial_events WHERE permission_code = 'kitchen.advance';"));
    }

    [Fact]
    public async Task TicketItemLifecycleUsesAuthoritativeVersionsAndRejectsStaleWrites()
    {
        var terminalId = Guid.NewGuid();
        // V1-IAM-028: every real FOH role holds both grants (orders.send for
        // cancel, kitchen.advance for forward progress) — this lifecycle test
        // exercises both kinds of transition, so it seeds a full session
        // rather than the narrow kitchen-staff one.
        var cookie = await _database.SeedSessionAsync(
            terminalId,
            [KitchenOperationsEndpoints.TicketMutationPermission, ApplicationPermissions.KitchenAdvance]);
        var seed = await _database.SeedKitchenGraphAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var tickets = await GetAsync<KitchenTicketV1[]>(
            client, Prefix(terminalId) + "/tickets?stationId=hot-line", cookie);
        var ticket = Assert.Single(tickets!);
        Assert.Equal(seed.TicketId, ticket.Id);
        Assert.Equal("Queued", ticket.Status);
        var ticketJson = await GetStringAsync(
            client, Prefix(terminalId) + $"/tickets/{seed.TicketId:D}", cookie);
        // The waiter-entered line note is a preparation instruction and is
        // exposed to the kitchen (PO:2026-09-01, V1-RMD-082).
        Assert.Contains("az tuz, acisiz", ticketJson, StringComparison.Ordinal);
        Assert.Contains("Extra herbs", ticketJson, StringComparison.Ordinal);
        // Sensitive kitchen resources still stay out of the ticket DTO.
        Assert.DoesNotContain("secret-payload", ticketJson, StringComparison.Ordinal);

        var accepted = await PostAsync<KitchenTicketV1>(
            client,
            $"{Prefix(terminalId)}/tickets/{seed.TicketId:D}/transition",
            cookie,
            new TransitionKitchenTicketV1("Accepted", 1));
        Assert.Equal(2, accepted.RowVersion);
        // Regression assertion for an independent audit finding (2026-09-05):
        // a pure ticket-level transition does not touch any item, so the
        // item's own row_version must stay unchanged (was 1, not bumped to
        // 2) — PostgresKitchenTicketRepository.SaveAsync used to bump every
        // item's row_version on every save regardless of whether that item
        // actually changed.
        Assert.Equal(1, Assert.Single(accepted.Items).RowVersion);

        var preparing = await PostAsync<KitchenTicketV1>(
            client,
            $"{Prefix(terminalId)}/tickets/{seed.TicketId:D}/items/{seed.ItemId:D}/transition",
            cookie,
            new TransitionKitchenItemV1("Preparing", 2, 1));
        Assert.Equal(3, preparing.RowVersion);
        Assert.Equal("Preparing", Assert.Single(preparing.Items).Status);
        Assert.Equal(2, Assert.Single(preparing.Items).RowVersion);

        using var staleRequest = JsonRequest(
            HttpMethod.Post,
            $"{Prefix(terminalId)}/tickets/{seed.TicketId:D}/transition",
            cookie,
            new TransitionKitchenTicketV1("Ready", 2));
        using var stale = await client.SendAsync(staleRequest);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(
            "CONCURRENT_MODIFICATION",
            (await stale.Content.ReadFromJsonAsync<KitchenOperationsErrorEnvelopeV1>())!.Error.Code);

        var readyItem = await PostAsync<KitchenTicketV1>(
            client,
            $"{Prefix(terminalId)}/tickets/{seed.TicketId:D}/items/{seed.ItemId:D}/transition",
            cookie,
            new TransitionKitchenItemV1("Ready", 3, 2));
        Assert.Equal(4, readyItem.RowVersion);

        var readyTicket = await PostAsync<KitchenTicketV1>(
            client,
            $"{Prefix(terminalId)}/tickets/{seed.TicketId:D}/transition",
            cookie,
            new TransitionKitchenTicketV1("Ready", 4));
        Assert.Equal("Ready", readyTicket.Status);
    }

    // V1-KIT-007: kitchen staff no longer has to call the ticket-level
    // "Accepted" transition before advancing an item — starting the item
    // directly from a Queued ticket must implicitly accept the ticket in
    // the same request, not leave it stuck on "Queued" forever.
    //
    // V1-IAM-028: this session deliberately holds ONLY kitchen.advance (the
    // "Mutfak Personeli" line-cook grant), not orders.send — proving a
    // narrow session can advance an item forward but is refused a Cancelled
    // transition, which still requires orders.send (Mutfak Şefi/FOH roles).
    [Fact]
    public async Task KitchenAdvanceOnlySessionCanAdvanceButNotCancel()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedSessionAsync(
            terminalId,
            [ApplicationPermissions.KitchenAdvance]);
        var seed = await _database.SeedKitchenGraphAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var ticket = await GetAsync<KitchenTicketV1>(
            client, Prefix(terminalId) + $"/tickets/{seed.TicketId:D}", cookie);
        Assert.Equal("Queued", ticket!.Status);

        var preparing = await PostAsync<KitchenTicketV1>(
            client,
            $"{Prefix(terminalId)}/tickets/{seed.TicketId:D}/items/{seed.ItemId:D}/transition",
            cookie,
            new TransitionKitchenItemV1("Preparing", 1, 1));

        Assert.Equal("Preparing", preparing.Status);
        Assert.Equal("Preparing", Assert.Single(preparing.Items).Status);

        using var cancelRequest = JsonRequest(
            HttpMethod.Post,
            $"{Prefix(terminalId)}/tickets/{seed.TicketId:D}/transition",
            cookie,
            new TransitionKitchenTicketV1("Cancelled", preparing.RowVersion));
        using var cancelDenied = await client.SendAsync(cancelRequest);
        Assert.Equal(HttpStatusCode.Forbidden, cancelDenied.StatusCode);
    }

    // V1-KIT-009: undo is a forward-step correction, not a cancel — a
    // kitchen-staff-only (kitchen.advance) session must be able to fix its
    // own misclick without needing orders.send.
    [Fact]
    public async Task KitchenAdvanceOnlySessionCanUndoItsOwnMistake()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedSessionAsync(
            terminalId,
            [ApplicationPermissions.KitchenAdvance]);
        var seed = await _database.SeedKitchenGraphAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var preparing = await PostAsync<KitchenTicketV1>(
            client,
            $"{Prefix(terminalId)}/tickets/{seed.TicketId:D}/items/{seed.ItemId:D}/transition",
            cookie,
            new TransitionKitchenItemV1("Preparing", 1, 1));
        var preparingItem = Assert.Single(preparing.Items);
        Assert.Equal("Preparing", preparingItem.Status);

        var undone = await PostAsync<KitchenTicketV1>(
            client,
            $"{Prefix(terminalId)}/tickets/{seed.TicketId:D}/items/{seed.ItemId:D}/undo",
            cookie,
            new UndoKitchenItemV1(preparing.RowVersion, preparingItem.RowVersion));

        Assert.Equal("Queued", Assert.Single(undone.Items).Status);
    }

    [Fact]
    public async Task UndoFromQueuedIsRejectedThereIsNothingToReverse()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedSessionAsync(
            terminalId,
            [ApplicationPermissions.KitchenAdvance]);
        var seed = await _database.SeedKitchenGraphAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var undoRequest = JsonRequest(
            HttpMethod.Post,
            $"{Prefix(terminalId)}/tickets/{seed.TicketId:D}/items/{seed.ItemId:D}/undo",
            cookie,
            new UndoKitchenItemV1(1, 1));
        using var response = await client.SendAsync(undoRequest);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "DOMAIN_CONFLICT",
            (await response.Content.ReadFromJsonAsync<KitchenOperationsErrorEnvelopeV1>())!.Error.Code);
    }

    // Independent review (2026-09-13): the permission gate's "is this a
    // Cancelled request" check and the domain's eventual enum parse
    // (Enum.TryParse, which documentedly trims whitespace) must agree — a
    // kitchen.advance-only session must not be able to bypass the
    // orders.send requirement for Cancelled just by padding the target
    // state with whitespace.
    [Theory]
    [InlineData("Cancelled ")]
    [InlineData(" Cancelled")]
    [InlineData("CANCELLED")]
    public async Task KitchenAdvanceOnlySessionCannotCancelViaWhitespaceOrCaseVariants(string targetState)
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedSessionAsync(
            terminalId,
            [ApplicationPermissions.KitchenAdvance]);
        var seed = await _database.SeedKitchenGraphAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var cancelRequest = JsonRequest(
            HttpMethod.Post,
            $"{Prefix(terminalId)}/tickets/{seed.TicketId:D}/transition",
            cookie,
            new TransitionKitchenTicketV1(targetState, 1));
        using var cancelDenied = await client.SendAsync(cancelRequest);
        Assert.Equal(HttpStatusCode.Forbidden, cancelDenied.StatusCode);
    }

    // V1-KIT-010: kitchen.live_sync_enabled silently changed behavior
    // (waiter ready-notifications, KitchenState mirroring) with no way for
    // the Kitchen screen itself to see it — an independent review
    // (2026-09-13) found this. GetLiveSyncStatusAsync must report the same
    // value TransitionItemAsync itself already reads before publishing.
    [Fact]
    public async Task LiveSyncStatusReflectsTheDeploymentSettingDefaultOffThenOn()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedSessionAsync(terminalId, []);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var initial = await GetAsync<LiveSyncStatusV1>(client, Prefix(terminalId) + "/operations/live-sync", cookie);
        Assert.False(initial!.Enabled);

        var settings = new SettingsService(new PostgresSettingsRepository(_database.DataSource, new SettingValidator()));
        var record = await settings.GetRecordAsync(KitchenLiveSyncSetting.Key);
        await settings.SetValueAsync(KitchenLiveSyncSetting.Key, true, record!.RowVersion);

        var afterEnabled = await GetAsync<LiveSyncStatusV1>(client, Prefix(terminalId) + "/operations/live-sync", cookie);
        Assert.True(afterEnabled!.Enabled);
    }

    [Fact]
    public async Task UnknownDeliveryRequiresReasonedReprintApprovalAndNeverAutoPrints()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedSessionAsync(
            terminalId,
            [KitchenOperationsEndpoints.ReprintPermission]);
        var seed = await _database.SeedKitchenGraphAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var unknown = await GetAsync<PhysicalPrintDeliveryV1[]>(
            client, Prefix(terminalId) + "/deliveries/unknown", cookie);
        var delivery = Assert.Single(unknown!);
        Assert.Equal(seed.DeliveryId, delivery.Id);
        Assert.Equal("Unknown", delivery.Status);
        var serialized = await GetStringAsync(
            client, Prefix(terminalId) + $"/deliveries/{seed.DeliveryId:D}", cookie);
        Assert.DoesNotContain("secret-payload", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("PayloadSnapshot", serialized, StringComparison.Ordinal);

        using var missingReason = JsonRequest(
            HttpMethod.Post,
            $"{Prefix(terminalId)}/deliveries/{seed.DeliveryId:D}/reprint-approval",
            cookie,
            new ReprintDecisionV1(""));
        using var invalid = await client.SendAsync(missingReason);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var approved = await PostAsync<PhysicalPrintDeliveryV1>(
            client,
            $"{Prefix(terminalId)}/deliveries/{seed.DeliveryId:D}/reprint-approval",
            cookie,
            new ReprintDecisionV1("Station checked; original ticket was not printed."));
        Assert.Equal("ReprintApproved", approved.Status);
        Assert.True(approved.IsReprint);
        Assert.Equal(
            "ReprintApproved",
            await _database.ScalarAsync<string>(
                "SELECT status FROM kitchen.physical_print_deliveries WHERE id = '" + seed.DeliveryId + "';"));
    }

    [Fact]
    public async Task HealthBackupPrinterRouteAndAuditSurfacesAreMinimizedAndFailClosed()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedSessionAsync(
            terminalId,
            [
                KitchenOperationsEndpoints.RoutingMutationPermission,
                KitchenOperationsEndpoints.BackupPermission,
                ApplicationPermissions.ReportsView,
            ]);
        var readOnlyCookie = await _database.SeedSessionAsync(terminalId, []);
        var seed = await _database.SeedKitchenGraphAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var health = await GetAsync<HealthSnapshotV1>(
            client, Prefix(terminalId) + "/operations/health/latest", cookie);
        Assert.Equal("Unhealthy", health!.DiskStatus);
        Assert.Equal("Unhealthy", health.LastBackupStatus);

        var backups = await GetAsync<BackupV1[]>(
            client, Prefix(terminalId) + "/operations/backups/recent", cookie);
        var backup = Assert.Single(backups!);
        Assert.Equal("Failed", backup.Status);
        var backupJson = await GetStringAsync(
            client, Prefix(terminalId) + "/operations/backups/recent", cookie);
        Assert.DoesNotContain("FilePath", backupJson, StringComparison.Ordinal);
        Assert.DoesNotContain("ChecksumSha256", backupJson, StringComparison.Ordinal);

        using var backupRequest = JsonRequest(
            HttpMethod.Post,
            Prefix(terminalId) + "/operations/backups",
            cookie,
            new StartBackupV1("Full"));
        using var backupResponse = await client.SendAsync(backupRequest);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, backupResponse.StatusCode);
        Assert.Equal(
            "BACKUP_NOT_CONFIGURED",
            (await backupResponse.Content.ReadFromJsonAsync<KitchenOperationsErrorEnvelopeV1>())!.Error.Code);

        var printers = await GetAsync<PrinterV1[]>(
            client, Prefix(terminalId) + "/printers", cookie);
        Assert.Contains(printers!, printer => printer.Id == seed.PrinterId);
        var routes = await GetAsync<PrinterRouteV1[]>(
            client, Prefix(terminalId) + "/routes", cookie);
        Assert.Contains(routes!, route => route.Id == seed.RouteId);

        // V1-RMD-116: the audit trail crosses every module's aggregates
        // (void/comp/discount decisions included), so a plain authenticated
        // read (no permission at all) must not reach it.
        using var deniedAggregateRequest = Request(
            HttpMethod.Get,
            Prefix(terminalId) + $"/audit/aggregate/KitchenTicket/{seed.TicketId:D}",
            readOnlyCookie);
        using var deniedAggregate = await client.SendAsync(deniedAggregateRequest);
        Assert.Equal(HttpStatusCode.Forbidden, deniedAggregate.StatusCode);

        var audit = await GetAsync<AuditEventV1[]>(
            client, Prefix(terminalId) + $"/audit/aggregate/KitchenTicket/{seed.TicketId:D}", cookie);
        var auditEvent = Assert.Single(audit!);
        Assert.Equal("KitchenTicketTransitioned", auditEvent.EventName);
        var auditJson = await GetStringAsync(
            client, Prefix(terminalId) + $"/audit/aggregate/KitchenTicket/{seed.TicketId:D}", cookie);
        Assert.DoesNotContain("beforeStateJson", auditJson, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", auditJson, StringComparison.Ordinal);

        using var deniedCorrelationRequest = Request(
            HttpMethod.Get,
            Prefix(terminalId) + $"/audit/correlation/{auditEvent.CorrelationId}",
            readOnlyCookie);
        using var deniedCorrelation = await client.SendAsync(deniedCorrelationRequest);
        Assert.Equal(HttpStatusCode.Forbidden, deniedCorrelation.StatusCode);

        var byCorrelation = await GetAsync<AuditEventV1[]>(
            client, Prefix(terminalId) + $"/audit/correlation/{auditEvent.CorrelationId}", cookie);
        Assert.Contains(byCorrelation!, e => e.Id == auditEvent.Id);
    }

    /// <summary>
    /// V1-WTR-025's E2E audit (2026-09-12): PUT /routes/{routeId} could only
    /// ever edit a route that already existed - nothing anywhere could
    /// create the first one, so a category-level route (e.g. "the whole
    /// Izgara group goes to the grill printer") could never actually be
    /// configured through the API at all.
    /// </summary>
    [Fact]
    public async Task CreatingACategoryRouteSucceedsAndIsThenListedAndEditable()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedSessionAsync(
            terminalId,
            [KitchenOperationsEndpoints.RoutingMutationPermission]);
        var readOnlyCookie = await _database.SeedSessionAsync(terminalId, []);
        var seed = await _database.SeedKitchenGraphAsync();
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        var routeId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        using var deniedRequest = JsonRequest(
            HttpMethod.Post,
            $"{Prefix(terminalId)}/routes/{routeId:D}",
            readOnlyCookie,
            new UpdatePrinterRouteV1("Category", seed.PrinterId, CategoryId: categoryId));
        using var denied = await client.SendAsync(deniedRequest);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var created = await PostAsync<PrinterRouteV1>(
            client,
            $"{Prefix(terminalId)}/routes/{routeId:D}",
            cookie,
            new UpdatePrinterRouteV1("Category", seed.PrinterId, CategoryId: categoryId));
        Assert.Equal("Category", created.RouteLevel);
        Assert.Equal(categoryId, created.CategoryId);
        Assert.True(created.IsActive);

        var routes = await GetAsync<PrinterRouteV1[]>(
            client, Prefix(terminalId) + "/routes", cookie);
        Assert.Contains(routes!, route => route.Id == routeId && route.CategoryId == categoryId);

        // The same endpoint that created it can still edit it afterwards -
        // deactivating, same as PUT already could for a pre-seeded route.
        var deactivated = await PutAsync<PrinterRouteV1>(
            client,
            $"{Prefix(terminalId)}/routes/{routeId:D}",
            cookie,
            new UpdatePrinterRouteV1("Category", seed.PrinterId, CategoryId: categoryId, IsActive: false));
        Assert.False(deactivated.IsActive);
    }

    /// <summary>
    /// V1-KIT-008: suspending a product that was still on active sale
    /// (IsAvailable = true) is this task's "plan aykırı" rule, and must
    /// write an already-resolved, informational identity.authorization_grants
    /// row so a manager can find it later — deliberately not a live push
    /// (see the task's Goal), so this is checked against the table directly
    /// rather than any notification endpoint.
    /// </summary>
    [Fact]
    public async Task SuspendingAnAvailableProductRequiresTheSuspendPermissionAndRaisesAPlanConflictGrant()
    {
        var terminalId = Guid.NewGuid();
        var suspendCookie = await _database.SeedSessionAsync(
            terminalId, [KitchenOperationsEndpoints.AvailabilitySuspendPermission]);
        // V1-IAM-028: kitchen.advance is a real, separate grant from
        // kitchen.availability.suspend — a line-cook-only session must not
        // be able to 86 a product just because it can advance tickets.
        var advanceOnlyCookie = await _database.SeedSessionAsync(
            terminalId, [ApplicationPermissions.KitchenAdvance]);
        var productId = await SeedProductAsync(isAvailable: true);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var deniedRequest = Request(
            HttpMethod.Post, $"{Prefix(terminalId)}/products/{productId:D}/suspend", advanceOnlyCookie);
        using var denied = await client.SendAsync(deniedRequest);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        using var suspendRequest = Request(
            HttpMethod.Post, $"{Prefix(terminalId)}/products/{productId:D}/suspend", suspendCookie);
        using var response = await client.SendAsync(suspendRequest);
        Assert.True(
            response.IsSuccessStatusCode,
            $"POST suspend returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var result = await response.Content.ReadFromJsonAsync<ProductAvailabilitySuspendedV1>();
        Assert.NotNull(result);
        Assert.Equal(productId, result!.ProductId);
        Assert.False(result.IsAvailable);
        Assert.True(result.PlanConflict);

        Assert.False(await _database.ScalarAsync<bool>(
            $"SELECT is_available FROM catalog.products WHERE product_id = '{productId:D}';"));
        Assert.Equal(
            1L,
            await _database.ScalarAsync<long>(
                $"""
                SELECT count(*) FROM identity.authorization_grants
                WHERE subject_type = 'product' AND subject_id = '{productId:D}'
                  AND permission_code = 'kitchen.availability.suspend'
                  AND reason_code = 'kitchen.availability.suspend.plan-conflict'
                  AND status = 'granted' AND policy_path = 'auto'
                  AND approver_user_id IS NULL AND resolved_at IS NOT NULL;
                """));
    }

    /// <summary>
    /// V1-KIT-008 scope item 4: an already-suspended product being 86'd
    /// again (idempotent retry, e.g. a double-tap) is not a plan conflict —
    /// only the first, genuinely-conflicting suspend raises a grant row.
    /// </summary>
    [Fact]
    public async Task SuspendingAnAlreadySuspendedProductIsIdempotentAndRaisesNoSecondGrant()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedSessionAsync(
            terminalId, [KitchenOperationsEndpoints.AvailabilitySuspendPermission]);
        var productId = await SeedProductAsync(isAvailable: false);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = Request(
            HttpMethod.Post, $"{Prefix(terminalId)}/products/{productId:D}/suspend", cookie);
        using var response = await client.SendAsync(request);
        Assert.True(
            response.IsSuccessStatusCode,
            $"POST suspend returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var result = await response.Content.ReadFromJsonAsync<ProductAvailabilitySuspendedV1>();
        Assert.NotNull(result);
        Assert.False(result!.IsAvailable);
        Assert.False(result.PlanConflict);

        Assert.Equal(
            0L,
            await _database.ScalarAsync<long>(
                $"SELECT count(*) FROM identity.authorization_grants WHERE subject_id = '{productId:D}';"));
    }

    [Fact]
    public async Task SuspendingAMissingProductReturnsNotFound()
    {
        var terminalId = Guid.NewGuid();
        var cookie = await _database.SeedSessionAsync(
            terminalId, [KitchenOperationsEndpoints.AvailabilitySuspendPermission]);
        await using var app = await StartAsync();
        using var client = CreateClient(app);

        using var request = Request(
            HttpMethod.Post, $"{Prefix(terminalId)}/products/{Guid.NewGuid():D}/suspend", cookie);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<Guid> SeedProductAsync(bool isAvailable)
    {
        var productId = Guid.NewGuid();
        await using var command = _database.DataSource.CreateCommand(
            $"""
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price, is_available)
            VALUES ('{productId:D}', 'SKU-{productId:N}', '86 test product', 1, 1, 30.00, {(isAvailable ? "TRUE" : "FALSE")});
            """);
        await command.ExecuteNonQueryAsync();
        return productId;
    }

    private async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddKitchenOperationsExperience();
        var app = builder.Build();
        app.MapKitchenOperationsApi();
        await app.StartAsync();
        return app;
    }

    private static async Task<TResponse> PostAsync<TResponse>(
        HttpClient client,
        string path,
        string cookie,
        object body)
    {
        using var request = JsonRequest(HttpMethod.Post, path, cookie, body);
        using var response = await client.SendAsync(request);
        Assert.True(
            response.IsSuccessStatusCode,
            $"POST {path} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<TResponse>())!;
    }

    private static async Task<TResponse> PutAsync<TResponse>(
        HttpClient client,
        string path,
        string cookie,
        object body)
    {
        using var request = JsonRequest(HttpMethod.Put, path, cookie, body);
        using var response = await client.SendAsync(request);
        Assert.True(
            response.IsSuccessStatusCode,
            $"PUT {path} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<TResponse>())!;
    }

    private static async Task<TResponse> GetAsync<TResponse>(
        HttpClient client,
        string path,
        string cookie)
    {
        using var request = Request(HttpMethod.Get, path, cookie);
        using var response = await client.SendAsync(request);
        Assert.True(
            response.IsSuccessStatusCode,
            $"GET {path} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<TResponse>())!;
    }

    private static async Task<string> GetStringAsync(
        HttpClient client,
        string path,
        string cookie)
    {
        using var request = Request(HttpMethod.Get, path, cookie);
        using var response = await client.SendAsync(request);
        Assert.True(
            response.IsSuccessStatusCode,
            $"GET {path} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadAsStringAsync();
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, string cookie)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return request;
    }

    private static HttpRequestMessage JsonRequest(
        HttpMethod method,
        string path,
        string cookie,
        object body)
    {
        var request = Request(method, path, cookie);
        request.Content = JsonContent.Create(body);
        return request;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }

    private static string Prefix(Guid terminalId)
        => $"/api/v1/terminals/{terminalId:D}/kitchen-operations";
}

[CollectionDefinition("Kitchen operations PostgreSQL HTTP", DisableParallelization = true)]
public sealed class KitchenOperationsPostgresqlDefinition;
