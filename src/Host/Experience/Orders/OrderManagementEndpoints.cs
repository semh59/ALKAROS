using ALKAROS.Audit.EventStore;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Experience.Orders.OrderStockConsumption;
using ALKAROS.Host.Experience.Orders.PendingOrderConfirmation;
using ALKAROS.Host.Experience.Orders.SentItemVoid;
using ALKAROS.Host.Experience.Orders.SubmissionStockConsumption;
using ALKAROS.Host.Experience.Orders.TableDraft;
using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.CrossChannelReservation;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.MovementReversal;
using ALKAROS.Inventory.ModifierStock;
using ALKAROS.Inventory.PortionReservations.CancellationEffects;
using ALKAROS.Inventory.PortionReservations.Lifecycle;
using ALKAROS.Inventory.ReservationBalanceProjection;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Inventory.Transactions;
using ALKAROS.Inventory.WasteRecording;
using ALKAROS.Settings.GarsonFeatureToggles;
using ALKAROS.Settings.TypedSettings;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Behavioural;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Identity.Authorization.Delegations;
using ALKAROS.Identity.Authorization.Grants;
using ALKAROS.Identity.Authorization.PersonalBudgets;
using ALKAROS.Identity.Authorization.Policies;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.Measurements;
using ALKAROS.Orders.ItemExceptions;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Orders.SubmitOrder;
using ALKAROS.Recipes.CatalogMapping;
using ALKAROS.Recipes.TheoreticalConsumption;
using ALKAROS.Recipes.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data.Common;
using System.Text.Json;

namespace ALKAROS.Host.Experience.Orders;

public static class OrderManagementEndpoints
{
    public const string RoutePrefix = "/api/v1/terminals/{terminalId:guid}/orders";
    public const string CashierCookieName = DualScreenApplication.CashierCookieName;

    public static IServiceCollection AddOrderManagementExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // V1-ORD-005: was missing — every endpoint in this group takes a
        // DualScreenStore parameter, but nothing registered it here (it only
        // worked when the full Host composition happened to register it
        // first). Minimal API can't infer an unregistered service parameter
        // and fails route building with "Failure to infer one or more
        // parameters", so a standalone host for this module never started.
        services.TryAddSingleton<DualScreenStore>();
        services.TryAddSingleton<IOrderRepository, PostgresOrderRepository>();
        // Refactor step 1/7 (docs/engineering/garson-refactor-plan.md,
        // 2026-09-12): extracted out of OrderManagementStore so every
        // order-reading surface shares one Order -> OrderDto projection.
        services.TryAddSingleton<OrderDtoAssembler>();
        // Refactor step 2/7 (docs/engineering/garson-refactor-plan.md).
        services.TryAddSingleton<ShiftSummaryStore>();
        // Refactor step 3/7 (docs/engineering/garson-refactor-plan.md).
        services.TryAddSingleton<CashierHandoffStore>();
        // V1-RMD-287: no-op until the Host's live-connection registration replaces it.
        services.TryAddSingleton<ICashierQueueAnnouncer, NoOpCashierQueueAnnouncer>();
        // V1-RMD-282: closes the order once its check is paid.
        services.TryAddSingleton<OrderSettlementService>();
        // Refactor step 4/7 (docs/engineering/garson-refactor-plan.md).
        services.TryAddSingleton<TableDraftService>();
        // Refactor step 5/7 (docs/engineering/garson-refactor-plan.md).
        services.TryAddSingleton<OrderSubmissionCoordinator>();
        // Refactor step 6/7 (docs/engineering/garson-refactor-plan.md):
        // OrderManagementStore's own final, no-longer-worth-a-better-name
        // remainder — GetOrderByIdAsync/GetActiveOrderByTableIdAsync/
        // TransferServingUserAsync/GetPendingOrdersAsync share no real
        // cohesion, deliberately left as one small group rather than
        // forcing a fake abstraction (the plan's own Section 1.3 reasoning).
        services.TryAddSingleton<OrderReadStore>();
        services.TryAddSingleton<ServingHandoffNoteStore>();
        // V1-RMD-204: shared by /table-draft's assignment suggestion and by
        // SignalRPendingOrderAnnouncer's QR-routing (V1-RMD-202) — one
        // "who's most suitable right now" answer.
        services.TryAddSingleton<SuggestedWaiterResolver>();
        services.TryAddSingleton<IRoleRepository, PostgresRoleRepository>();
        services.TryAddSingleton<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddSingleton<IAuthorizationService, AuthorizationService>();
        // V1-RMD-244: bills.void/bills.comp are the exact sensitive-command
        // examples this store was built for (V1-RMD-116's own comment on
        // the audit read endpoints); V1-RMD-237 wired the first real
        // caller (bills.discount) in a different module, this is the second.
        services.TryAddSingleton<IAuditEventStore, PostgresAuditEventStore>();
        // V1-ORD-005: wires the existing (previously unreachable)
        // ItemExceptionHandler.VoidItemAsync to the pre-send void endpoint.
        services.TryAddSingleton<ItemExceptionHandler>();
        // V1-BIL-005: the grant-request flow (V1-IAM-019/020/021/023) worked
        // end to end but had zero HTTP callers — IAuthorizationGrantService
        // .RequestAsync was dead code. The comp endpoint is the first real
        // caller; DelegationEscalationResolver and BehaviouralTighteningGate
        // are registered so they actually run on this path, not stubbed out.
        services.TryAddSingleton<IAuthorizationGrantRepository, PostgresAuthorizationGrantRepository>();
        services.TryAddSingleton<IAuthorizationPolicyRepository, PostgresAuthorizationPolicyRepository>();
        services.TryAddSingleton<IAuthorizationDelegationRepository, PostgresAuthorizationDelegationRepository>();
        services.TryAddSingleton<IEscalationResolver, DelegationEscalationResolver>();
        // V1-WTR-012: plain AddSingleton, not TryAddSingleton — TryAdd*
        // checks only the SERVICE type and DelegationEscalationResolver
        // already registered one IEscalationResolver, so a TryAdd here
        // would silently no-op and this resolver would never run. Ordered
        // after Delegation — an active delegation (broader, individually
        // granted authority) takes precedence over this small, always-on
        // self-service allowance. Runs for every escalated grant but only
        // ever resolves bills.comp requests from the waiter role within
        // PersonalCompBudgetPolicy's caps; anything else falls through to
        // it unchanged (returns null).
        // V1-SET-004: PersonalCompBudgetEscalationResolver lives in the
        // Identity module, which never depends on Settings (Settings is
        // consumed only at this Host layer, same boundary every other
        // Experience registration respects) — so the toggle check wraps it
        // here instead of reaching into that module.
        services.AddSingleton<IEscalationResolver>(serviceProvider =>
            new GarsonFeatureGatedEscalationResolver(
                new PersonalCompBudgetEscalationResolver(
                    serviceProvider.GetRequiredService<IAuthorizationGrantRepository>()),
                serviceProvider.GetRequiredService<ISettingsService>(),
                GarsonFeature.PersonalCompBudget));
        services.TryAddSingleton<IBehaviouralRateSource, PostgresBehaviouralRateSource>();
        services.TryAddSingleton<IBehaviouralTighteningRepository, PostgresBehaviouralTighteningRepository>();
        services.TryAddSingleton<IPrePolicyGate, BehaviouralTighteningGate>();
        services.TryAddSingleton<IAuthorizationGrantService, AuthorizationGrantService>();
        // V1-IAM-027: codes the V0-DOM-006 amendment (Semih, 2026-09-04) — a
        // sent-but-unserved item may now be voided under the bills.void
        // grant. Lives at the Host layer (SentItemVoidStore) rather than
        // inside Orders' ItemExceptionHandler so Orders/Kitchen/Billing stay
        // decoupled from each other (V0-ARC-001); Host already depends on
        // all three.
        services.TryAddSingleton<IKitchenTicketRepository, PostgresKitchenTicketRepository>();
        services.TryAddSingleton<IBillRepository, PostgresBillRepository>();
        services.TryAddSingleton<SentItemVoidStore>();
        // V1-RMD-143: Semih's decision (2026-09-09) that Accept should really
        // consume stock — see OrderStockConsumptionService's own doc comment.
        // ITableRepository-style same-module ownership: these are Inventory's
        // own registrations (already made by InventoryModule in the real
        // Host), TryAdd defers to that; a standalone composition of just this
        // experience still resolves the whole chain.
        services.TryAddSingleton<IProductStockMappingRepository, PostgresProductStockMappingRepository>();
        services.TryAddSingleton<IModifierStockMappingRepository, PostgresModifierStockMappingRepository>();
        services.TryAddSingleton<IStockItemRepository, PostgresStockItemRepository>();
        services.TryAddSingleton<IStockBalanceRepository, PostgresStockBalanceRepository>();
        services.TryAddSingleton<IStockMovementRepository, PostgresStockMovementRepository>();
        // V11-RCP-004: same TryAdd-defers-to-RecipesModule shape as the
        // stock registrations just above — a standalone composition of just
        // this experience still resolves the whole chain.
        services.TryAddSingleton<IProductRecipeMappingRepository, PostgresProductRecipeMappingRepository>();
        services.TryAddSingleton<IRecipeVersionRepository, PostgresRecipeVersionRepository>();
        services.TryAddSingleton<ITheoreticalConsumptionRecordRepository, PostgresTheoreticalConsumptionRecordRepository>();
        services.TryAddSingleton<IUnitConverter, UnitConverter>();
        // V12-STK-001: consumption respects every other order's holds.
        services.TryAddSingleton<IReservationAwareConsumptionGuard, PostgresReservationAwareConsumptionGuard>();
        services.TryAddSingleton<OrderStockConsumptionService>();
        // V1-RMD-143 follow-up (2026-09-09): SentItemVoidStore restores an
        // item's own consumed stock when it is voided before the kitchen
        // ever started on it — reuses Inventory's own, already-built (and
        // already-tested at the module level) MovementReversal service
        // rather than hand-rolling a second "undo a Consumption" primitive.
        services.TryAddSingleton<IStockLocationRepository, PostgresStockLocationRepository>();
        services.TryAddSingleton<IStockBalanceProjector, StockBalanceProjector>();
        services.TryAddSingleton<IStockMovementReversalService, StockMovementReversalService>();
        // V1-RMD-137: found by an independent audit (2026-09-09) — no HTTP
        // action anywhere could ever move an order out of PendingConfirmation
        // (see PendingOrderConfirmationStore's own doc comment for the full
        // story). Reuses the same IOrderRepository/IKitchenTicketRepository/
        // IBillRepository already registered above.
        // V12-QRO-003: accepting a QR order claims its portions through the cross-channel
        // arbiter (V12-STK-001); its compensation path needs V11-RSV-003's cancellation
        // decision. Same TryAdd-defers-to-InventoryModule shape as the stock registrations above.
        services.TryAddSingleton<IPortionReservationRepository, PostgresPortionReservationRepository>();
        services.TryAddSingleton<IPortionReservationLifecycleService, PortionReservationLifecycleService>();
        services.TryAddSingleton<IReservationBalanceRepository, PostgresReservationBalanceRepository>();
        services.TryAddSingleton<IReservationBalanceProjector, ReservationBalanceProjector>();
        services.TryAddSingleton<IInventoryTransactionRunner, PostgresInventoryTransactionRunner>();
        services.TryAddSingleton<IWasteRecordRepository, PostgresWasteRecordRepository>();
        services.TryAddSingleton<IWasteRecordingService, WasteRecordingService>();
        services.TryAddSingleton<IKitchenItemStateProvider, PostgresKitchenItemStateProvider>();
        services.TryAddSingleton<IPortionCancellationDecisionService, PortionCancellationDecisionService>();
        services.TryAddSingleton<ICrossChannelPortionArbiter, PostgresCrossChannelPortionArbiter>();
        services.TryAddSingleton<PendingOrderConfirmationStore>();
        // V12-QRO-002: the background half of "no remote QR service denial"
        // (see QrOrderExpiryHostedService's own doc comment) — an
        // unconfirmed QR order auto-rejects (reusing the exact same
        // PendingOrderConfirmationStore.RejectAsync above) once it exceeds
        // the configured timeout.
        // V1-SET-004: PostgresSettingsRepository takes a DbDataSource, not
        // the NpgsqlDataSource this standalone composition registers —
        // same gap KitchenOperationsEndpoints.AddKitchenOperationsExperience
        // already had to close for its own settings-backed features.
        services.TryAddSingleton<DbDataSource>(serviceProvider =>
            serviceProvider.GetRequiredService<NpgsqlDataSource>());
        services.TryAddSingleton<ISettingValidator, SettingValidator>();
        services.TryAddSingleton<ISettingsRepository, PostgresSettingsRepository>();
        services.TryAddSingleton<ISettingsService, SettingsService>();
        services.AddHostedService<QrOrderExpiryHostedService>();
        // V1-RMD-113: found by an independent audit (2026-09-06) —
        // table-draft's own submit-draft endpoint had a completely separate,
        // thinner submit path (OrderManagementStore.SubmitOrderAsync) that
        // never created a kitchen ticket, never checked idempotency, and
        // never notified the customer display, unlike the terminal-wide
        // quick-sale "/submit" route (DualScreenApplication.Endpoints.cs),
        // which already had all three via SubmitOrderHandler. Same station
        // configuration contract as that route: ALKAROS_KITCHEN_STATION_ID
        // is required once an order is actually submitted (resolved lazily,
        // so a standalone composition that never calls submit-draft is
        // unaffected). No per-item printer routing collaborators are passed
        // here (kept minimal) — the dispatcher falls back to a single
        // ticket at the configured default station, its own documented
        // behaviour with no router.
        services.TryAddSingleton<SubmitOrderHandler>();
        services.TryAddSingleton<IOrderSubmissionDispatcher>(sp =>
        {
            var stationId = Environment.GetEnvironmentVariable(DualScreenApplication.KitchenStationEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(stationId))
            {
                throw new InvalidOperationException(
                    $"{DualScreenApplication.KitchenStationEnvironmentVariable} is required before order submission is enabled.");
            }

            // V1-RMD-144: a Cashier/Waiter order consumes its stock on submit
            // (QR/NFC still consume on Accept — the dispatcher checks
            // Order.Source). Stock runs before the kitchen dispatcher so an
            // order stock cannot cover never produces a ticket row.
            return new CompositeOrderSubmissionDispatcher(
                new OrderSubmissionStockDispatcher(sp.GetRequiredService<OrderStockConsumptionService>()),
                new KitchenOrderSubmissionDispatcher(sp.GetRequiredService<IKitchenTicketRepository>(), stationId));
        });
        services.AddSignalR(options => options.EnableDetailedErrors = false);
        // V1-ORD-005: every endpoint in this group calls RequireCashierSessionAsync
        // (or the permission variant), which throws DualScreenUnauthorizedException
        // on a missing/invalid session — with no filter that unwound as a bare 500,
        // not the 401 the caller needs. Same fix already exists per-module for
        // Billing/Catalog/Kitchen/Tables/Authorization; Orders never got it.
        services.TryAddTransient<OrderManagementExceptionFilter>();
        return services;
    }

    public static RouteGroupBuilder MapOrderManagementApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup(RoutePrefix)
            .WithTags("Orders")
            .RequireRateLimiting("terminal-write")
            .AddEndpointFilter<OrderManagementExceptionFilter>();

        group.MapPost("/table-draft", async (
            Guid terminalId,
            CreateTableDraftRequest request,
            TableDraftService store,
            SuggestedWaiterResolver suggestedWaiter,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actingUserId = await RequireCashierPermissionAsync(
                context, terminalId, dualStore, authorization, ApplicationPermissions.OrdersCreate, cancellationToken);

            // V1-RMD-127: found by an independent audit (2026-09-09) — these
            // four literals and the two GRANT_DENIED messages below were
            // hardcoded in English, a docs/UI_STYLE_GUIDE.md violation (every
            // user-visible string must be Turkish).
            if (request.TableId == Guid.Empty)
                return Results.BadRequest(new { error = new { code = "INVALID_TABLE", message = "Masa kimliği boş olamaz." } });

            if (request.Items == null || request.Items.Count == 0)
                return Results.BadRequest(new { error = new { code = "EMPTY_ITEMS", message = "Sipariş kalemleri boş olamaz." } });

            // V1-RMD-204: assigning someone ELSE as the serving waiter is the
            // same privileged act V1-RMD-111's own hand-off endpoint gates
            // with orders.transfer-server-any; naming yourself needs nothing
            // beyond the orders.create check already done above.
            var servingUserId = request.AssignedWaiterUserId ?? actingUserId;
            if (servingUserId != actingUserId)
            {
                await authorization.AuthorizeAsync(
                    actingUserId, ApplicationPermissions.OrdersTransferServerAny, cancellationToken);

                // V1-RMD-207: found while verifying V1-RMD-204 — nothing
                // constrains orders.orders.serving_user_id to a real row
                // (deliberately, V1-RMD-111's module boundary), so without
                // this a caller who HOLDS transfer-server-any could name any
                // Guid and it would be silently accepted.
                if (!await suggestedWaiter.IsValidWaiterAsync(servingUserId, cancellationToken))
                    return Results.BadRequest(new { error = new { code = "INVALID_WAITER", message = "Belirtilen garson bulunamadı veya yetkili değil." } });
            }

            var draft = await store.CreateOrUpdateTableDraftAsync(request, servingUserId, cancellationToken);
            return Results.Ok(draft);
        });

        // V1-RMD-204: what a Cashier/PosTerminal picker shows as the default
        // when opening a table by hand — the exact same candidate
        // SignalRPendingOrderAnnouncer (V1-RMD-202) already picks for a QR
        // order, just exposed for a human to see (and override) before
        // committing to it.
        group.MapGet("/suggested-waiter", async (
            Guid terminalId,
            SuggestedWaiterResolver suggestedWaiter,
            DualScreenStore dualStore,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierSessionAsync(context, terminalId, dualStore, cancellationToken);
            // V1-RMD-208: Cashier has no real table (KASA-1, V1-RMD-157), so
            // the zone-preference tier never applies here.
            var suggestion = await suggestedWaiter.ResolveMostSuitableWaiterAsync(tableId: null, cancellationToken);
            return suggestion is null ? Results.NoContent() : Results.Ok(suggestion);
        }).RequireRateLimiting("terminal-read");

        // V1-RMD-149: what a waiter falls back to when the live announcement
        // was missed. Same permission as accepting one — whoever may resolve
        // a pending order may see the queue of them.
        group.MapGet("/pending", async (
            Guid terminalId,
            OrderReadStore store,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierPermissionAsync(
                context, terminalId, dualStore, authorization, ApplicationPermissions.OrdersCreate, cancellationToken);
            return Results.Ok(await store.GetPendingOrdersAsync(cancellationToken));
        // V1-RMD-163: found by the 2026-09-10 Garson audit — the group
        // default below is "terminal-write" (120/min), correct for the
        // POST endpoints in this group but wrong for a read; this and the
        // other three GETs in this group used to share that tighter
        // write bucket with every mutating call on the same terminal.
        }).RequireRateLimiting("terminal-read");

        // V1-ORD-006: the party has left the table and is paying at the till.
        // Same permission as taking the order — a routine floor action every
        // staff role holds, not a money decision.
        group.MapPost("/{orderId:guid}/send-to-cashier", async (
            Guid terminalId,
            Guid orderId,
            SendCheckToCashierRequestV1 request,
            CashierHandoffStore store,
            ICashierQueueAnnouncer queueAnnouncer,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierPermissionAsync(
                context, terminalId, dualStore, authorization, ApplicationPermissions.OrdersCreate, cancellationToken);

            if (request is null || request.TableId == Guid.Empty)
                return Results.BadRequest(new { error = new { code = "INVALID_TABLE", message = "Masa kimliği boş olamaz." } });

            var sent = await store.SendCheckToCashierAsync(request.TableId, orderId, cancellationToken);
            // A repeat of the same request changed nothing, so the till has nothing to reload.
            if (!sent.AlreadySent)
                await queueAnnouncer.AnnouncePendingChecksChangedAsync("Sent", orderId, request.TableId, cancellationToken);
            return Results.Ok(sent);
        });

        // V1-RMD-281: the waiter (or the till) took a check back that was sent by mistake. Same permission as
        // sending it; refused with a Turkish reason once money has moved or a newer check sits on the table.
        group.MapPost("/{orderId:guid}/recall-from-cashier", async (
            Guid terminalId,
            Guid orderId,
            RecallCheckRequestV1 request,
            CashierHandoffStore store,
            ICashierQueueAnnouncer queueAnnouncer,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            IAuditEventStore auditEvents,
            IBillRepository bills,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashierPermissionAsync(
                context, terminalId, dualStore, authorization, ApplicationPermissions.OrdersCreate, cancellationToken);

            if (request is null || request.TableId == Guid.Empty)
                return Results.BadRequest(new { error = new { code = "INVALID_TABLE", message = "Masa kimliği boş olamaz." } });

            try
            {
                // The till must not keep a ghost bill (a re-send would otherwise reuse it with stale items); the
                // store cancels it in the same transaction as its money check (V1-RMD-414).
                var result = await store.RecallCheckAsync(request.TableId, orderId, bills, cancellationToken);

                if (result.Outcome == "Recalled")
                {
                    await auditEvents.AppendAsync(
                        new AuditEvent(
                            id: Guid.NewGuid(),
                            eventName: "check.recalled-from-cashier",
                            aggregateType: "Order",
                            aggregateId: orderId,
                            actorType: "User",
                            correlationId: context.TraceIdentifier,
                            actorId: principal,
                            reason: "Hesap kasadan masaya geri alındı.",
                            afterStateJson: System.Text.Json.JsonSerializer.Serialize(new { tableId = request.TableId })),
                        cancellationToken);
                }
                // Also on a repeat: a retry may be completing a request that failed half-way.
                await queueAnnouncer.AnnouncePendingChecksChangedAsync("Recalled", orderId, request.TableId, cancellationToken);
                return Results.Ok(result);
            }
            catch (TableHasNewerCheckException)
            {
                return Results.Json(
                    new { error = new { code = "TABLE_HAS_OPEN_CHECK", message = "Bu masada yeni bir hesap açık; önce onu kasaya gönderin." } },
                    statusCode: StatusCodes.Status409Conflict);
            }
            catch (CheckHasPaymentException)
            {
                return Results.Json(
                    new { error = new { code = "CHECK_HAS_PAYMENT", message = "Bu hesapta tahsilat başlamış; masaya geri alınamaz." } },
                    statusCode: StatusCodes.Status409Conflict);
            }
            catch (CheckNotRecallableException exception)
            {
                return Results.Json(
                    new { error = new { code = "CHECK_NOT_RECALLABLE", message = exception.Message } },
                    statusCode: StatusCodes.Status404NotFound);
            }
        });

        // V1-RMD-216: found by an independent audit (2026-09-16) — same gap
        // V1-RMD-160 fixed on the sibling GET /{orderId} below: this used to
        // call RequireCashierSessionAsync only (any authenticated terminal
        // session, no permission check), unlike its /pending sibling above.
        // A session holding neither OrdersCreate nor any Orders permission
        // (e.g. a kitchen-only role sharing the same terminal-session
        // mechanism) could read every check awaiting payment, amounts
        // included. Same OrdersCreate as /pending and GET /{orderId} -
        // whoever may take/resolve an order may see this queue too.
        group.MapGet("/awaiting-payment", async (
            Guid terminalId,
            CashierHandoffStore store,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierPermissionAsync(
                context, terminalId, dualStore, authorization, ApplicationPermissions.OrdersCreate, cancellationToken);
            return Results.Ok(await store.GetChecksAwaitingPaymentAsync(cancellationToken));
        }).RequireRateLimiting("terminal-read");

        // V1-RMD-216: found by the same independent audit as the entry right
        // above - the identical gap, on the identical store's other read
        // path (OrderReadStore.GetActiveOrderByTableIdAsync, same full
        // order/items/amounts payload as GET /{orderId} below already
        // requires OrdersCreate for since V1-RMD-160).
        group.MapGet("/table/{tableId:guid}", async (
            Guid terminalId,
            Guid tableId,
            OrderReadStore store,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierPermissionAsync(
                context, terminalId, dualStore, authorization, ApplicationPermissions.OrdersCreate, cancellationToken);

            var order = await store.GetActiveOrderByTableIdAsync(tableId, cancellationToken);
            if (order == null)
                return Results.NotFound(new { error = new { code = "ORDER_NOT_FOUND", message = "Bu masa için aktif sipariş bulunamadı." } });

            return Results.Ok(order);
        }).RequireRateLimiting("terminal-read");

        // V1-RMD-160: found by the 2026-09-10 Garson audit — this used to
        // call RequireCashierSessionAsync (any authenticated session, no
        // permission check at all), unlike its sibling endpoints in this
        // group. terminalId is not a data boundary anywhere in this store
        // (it is used only for session/idempotency-key context — a routine
        // staff member on any terminal legitimately needs to read any
        // table's order), so the real gap is the missing permission check,
        // not a missing terminal filter. OrdersCreate matches /pending's own
        // reasoning: whoever may take/resolve an order may read one.
        group.MapGet("/{orderId:guid}", async (
            Guid terminalId,
            Guid orderId,
            OrderReadStore store,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierPermissionAsync(
                context, terminalId, dualStore, authorization, ApplicationPermissions.OrdersCreate, cancellationToken);

            var order = await store.GetOrderByIdAsync(orderId, cancellationToken);
            if (order == null)
                return Results.NotFound(new { error = new { code = "ORDER_NOT_FOUND", message = "Sipariş bulunamadı." } });

            return Results.Ok(order);
        }).RequireRateLimiting("terminal-read");

        // Renamed from "/{orderId}/submit" (found by an independent audit,
        // 2026-09-05): that exact path was ALSO mapped unconditionally in
        // DualScreenApplication.Endpoints.cs (the terminal-wide quick-sale
        // submit, which PosTerminal's real "submit order" button actually
        // calls — SubmitOrderRequest{operationId, expectedRevision} matches
        // that handler's contract, not this one's SubmitTableOrderRequest).
        // Both were mapped on the same WebApplication (DualScreenApplication
        // .Build: MapApi(app) then app.MapOrderManagementApi()), so every
        // request to that path threw AmbiguousMatchException — the core
        // "submit order" flow returned a bare 500 on every attempt. This
        // route has zero real client callers (grep confirmed — only the
        // orphaned, non-compiling OrderManagementExperienceTests.cs calls
        // OrderManagementStore.SubmitOrderAsync directly, bypassing HTTP),
        // so renaming it is safe; the terminal-wide route is unchanged.
        //
        // V1-RMD-113: now delegates to the same SubmitOrderHandler the
        // terminal-wide route uses (see that handler's own doc comment) and
        // notifies the customer display exactly as that route does —
        // previously this endpoint neither created a kitchen ticket nor
        // told anyone the order changed.
        group.MapPost("/{orderId:guid}/submit-draft", async (
            Guid terminalId,
            Guid orderId,
            SubmitTableOrderRequest request,
            OrderSubmissionCoordinator store,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            IHubContext<CustomerDisplayHub> hub,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = await RequireCashierPermissionAsync(
                context, terminalId, dualStore, authorization, ApplicationPermissions.OrdersSend, cancellationToken);

            var submitted = await store.SubmitOrderAsync(
                terminalId, orderId, request.ExpectedRowVersion, request.OperationId, userId, cancellationToken);

            await hub.Clients.Group(DualScreenApplication.TerminalGroup(terminalId)).SendAsync(
                CustomerDisplayHub.SnapshotChanged,
                new { orderId, revision = submitted.RowVersion, kind = "OrderChanged" },
                cancellationToken);

            return Results.Ok(submitted);
        });

        // V1-WTR-025: the explicit "fire" action a multi-course check
        // needs once the table is ready for its next course. Same
        // permission as submit-draft — both are "send items to the
        // kitchen", one for a fresh round, one for a course already on the
        // ticket but held back.
        group.MapPost("/{orderId:guid}/fire-course", async (
            Guid terminalId,
            Guid orderId,
            FireCourseRequestV1 request,
            OrderSubmissionCoordinator store,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            IHubContext<CustomerDisplayHub> hub,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = await RequireCashierPermissionAsync(
                context, terminalId, dualStore, authorization, ApplicationPermissions.OrdersSend, cancellationToken);

            var fired = await store.FireCourseAsync(orderId, request.CourseNumber, userId, cancellationToken);

            await hub.Clients.Group(DualScreenApplication.TerminalGroup(terminalId)).SendAsync(
                CustomerDisplayHub.SnapshotChanged,
                new { orderId, revision = fired.RowVersion, kind = "OrderChanged" },
                cancellationToken);

            return Results.Ok(fired);
        });

        // V1-ORD-005: ItemExceptionHandler.VoidItemAsync already existed
        // (V1-ORD-003) and works — nothing called it. Gated by orders.create
        // (model §2: unsent items need only orders.create, not the grant-class
        // bills.void); VoidItemAsync itself still refuses anything already
        // sent to the kitchen (V0-DOM-006's default wall, unchanged for this
        // path — see V1-IAM-027 for the grant-gated exception).
        group.MapPost("/{orderId:guid}/items/{itemId:guid}/void", async (
            Guid terminalId,
            Guid orderId,
            Guid itemId,
            VoidOrderItemRequestV1 request,
            ItemExceptionHandler itemExceptions,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            IAuditEventStore auditEvents,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = await RequireCashierPermissionAsync(
                context, terminalId, dualStore, authorization, ApplicationPermissions.OrdersCreate, cancellationToken);

            // V1-RMD-127: found by an independent audit (2026-09-09) — these
            // inline catches returned the raw English ex.Message straight to
            // the client (docs/UI_STYLE_GUIDE.md violation) for exceptions
            // OrderManagementExceptionFilter (registered on this whole group)
            // already maps to the correct Turkish text and the exact same
            // status code / error code. Order not found or the item no
            // longer Active (already voided/comped) surfaces as
            // InvalidOperationException — the filter treats that as "the
            // world moved on" too (409 CONCURRENCY_CONFLICT), not a bad
            // request. Let every one of these bubble to the filter instead
            // of duplicating its mapping here.
            var command = new VoidOrderItemCommand(
                orderId,
                itemId,
                request.ExpectedRowVersion,
                userId,
                request.ReasonCode,
                CorrelationId: context.TraceIdentifier,
                request.Notes);
            var result = await itemExceptions.VoidItemAsync(command, cancellationToken);
            // V1-RMD-244: first real audit event on this endpoint —
            // IAuditEventStore existed since V1-OPS-001 with zero callers.
            await auditEvents.AppendAsync(
                new AuditEvent(
                    id: Guid.NewGuid(),
                    eventName: "order-item.voided",
                    aggregateType: "OrderItem",
                    aggregateId: itemId,
                    actorType: "User",
                    correlationId: context.TraceIdentifier,
                    actorId: userId,
                    reason: request.ReasonCode,
                    afterStateJson: JsonSerializer.Serialize(new { status = result.NewItemStatus.ToString() })),
                cancellationToken);
            return Results.Ok(new VoidOrderItemResultV1(
                result.OrderId,
                result.OrderItemId,
                result.NewItemStatus.ToString(),
                result.NewOrderRowVersion,
                result.NewOrderTotal,
                result.AppliedAt));
        });

        // V1-BIL-005: ItemExceptionHandler.ApplyComplimentaryAsync already
        // existed (V1-ORD-003) and works regardless of KitchenState — nothing
        // called it. bills.comp is grant-class (model §3): a role that holds
        // it outright (cashier/supervisor/manager, per ApplicationPermissions
        // .RoleGrants) applies directly; a role that does not (waiter) raises
        // an IAuthorizationGrantService request — the policy engine, an active
        // delegation (DelegationEscalationResolver) or a manager decides.
        // Own-check (a waiter may only comp a check they serve) is the model's
        // rule; Order.ServingUserId (V1-RMD-111, garson-masa design) now
        // carries that assignment, so SubjectServingUserId below is the
        // order's real server, not a permanently-dormant null.
        group.MapPost("/{orderId:guid}/items/{itemId:guid}/comp", async (
            Guid terminalId,
            Guid orderId,
            Guid itemId,
            ApplyComplimentaryRequestV1 request,
            ItemExceptionHandler itemExceptions,
            IOrderRepository orders,
            IRoleRepository roles,
            IAuthorizationGrantService grants,
            IAuthorizationGrantRepository grantsRepository,
            DualScreenStore dualStore,
            IAuditEventStore auditEvents,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = await RequireCashierSessionAsync(context, terminalId, dualStore, cancellationToken);

            if (!ComplimentaryReasonCatalog.IsValid(request.ReasonCode))
                throw new InvalidItemReasonException(
                    $"Reason '{request.ReasonCode}' is not a valid complimentary catalog reason.");

            // V1-WTR-012: only set when PersonalCompBudgetEscalationResolver
            // is what actually resolved this request - the waiter client
            // shows today's remaining allowance in the success toast instead
            // of a generic message.
            decimal? personalBudgetRemaining = null;

            var permissions = await roles.GetPermissionCodesForUserAsync(userId, cancellationToken);
            if (!permissions.Contains(ApplicationPermissions.BillsComp, StringComparer.Ordinal))
            {
                var role = await roles.GetGoverningRoleForUserAsync(userId, cancellationToken);
                if (role is null)
                    throw new AuthorizationDeniedException(userId, ApplicationPermissions.BillsComp, "Requester has no assigned role.");

                var order = await orders.GetByIdAsync(orderId, cancellationToken)
                    ?? throw new OrderItemNotFoundException(orderId, itemId);
                var item = order.Items.FirstOrDefault(i => i.Id == itemId)
                    ?? throw new OrderItemNotFoundException(orderId, itemId);

                var resolution = await grants.RequestAsync(
                    new GrantRequest(
                        request.IdempotencyKey,
                        ApplicationPermissions.BillsComp,
                        userId,
                        role.Code,
                        request.ReasonCode,
                        item.GrossAmount,
                        SubjectType: "OrderItem",
                        SubjectId: itemId,
                        SubjectServingUserId: order.ServingUserId),
                    cancellationToken);

                switch (resolution.Outcome)
                {
                    case GrantOutcome.Refused:
                        return Results.Json(
                            new { error = new { code = "GRANT_DENIED", message = "İkram talebi reddedildi." } },
                            statusCode: StatusCodes.Status403Forbidden);
                    case GrantOutcome.Pending:
                        return Results.Accepted(value: new ApplyComplimentaryResultV1(
                            "Pending", orderId, itemId, null, null, null, null, resolution.Grant.GrantId, null));
                    case GrantOutcome.Authorized:
                        if (resolution.Grant.Path == PolicyPath.PersonalBudget)
                        {
                            var startOfUtcDay = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
                            var spentToday = await grantsRepository.SumPersonalBudgetGrantedSinceAsync(
                                userId, ApplicationPermissions.BillsComp, startOfUtcDay, cancellationToken);
                            personalBudgetRemaining =
                                Math.Max(0m, PersonalCompBudgetPolicy.DailyCap - spentToday);
                        }
                        break;
                    default:
                        throw new InvalidOperationException($"Unhandled grant outcome '{resolution.Outcome}'.");
                }
            }

            // V1-RMD-127: see the matching note on the /void endpoint above —
            // OrderManagementExceptionFilter already maps every one of these
            // to the same status/error code with the correct Turkish text.
            var command = new ApplyComplimentaryCommand(
                orderId,
                itemId,
                request.ExpectedRowVersion,
                userId,
                request.ReasonCode,
                CorrelationId: context.TraceIdentifier,
                request.Notes);
            var result = await itemExceptions.ApplyComplimentaryAsync(command, cancellationToken);
            await auditEvents.AppendAsync(
                new AuditEvent(
                    id: Guid.NewGuid(),
                    eventName: "order-item.complimentary-applied",
                    aggregateType: "OrderItem",
                    aggregateId: itemId,
                    actorType: "User",
                    correlationId: context.TraceIdentifier,
                    actorId: userId,
                    reason: request.ReasonCode,
                    afterStateJson: JsonSerializer.Serialize(new { status = result.NewItemStatus.ToString() })),
                cancellationToken);
            return Results.Ok(new ApplyComplimentaryResultV1(
                "Applied",
                result.OrderId,
                result.OrderItemId,
                result.NewItemStatus.ToString(),
                result.NewOrderRowVersion,
                result.NewOrderTotal,
                result.AppliedAt,
                null,
                personalBudgetRemaining));
        });

        // V1-IAM-027: V0-DOM-006 amendment (Semih, 2026-09-04) — a sent-but-
        // unserved item (KitchenState ∈ {Sent, Preparing, Ready}) may now be
        // voided under the bills.void grant. Only reachable at all once
        // kitchen.live_sync_enabled is on (V1-SET-002) and V1-KIT-005 is
        // mirroring real kitchen state onto the order item — otherwise
        // KitchenState never leaves NotSent and ItemNotYetSentException
        // below always fires, same as today.
        group.MapPost("/{orderId:guid}/items/{itemId:guid}/void-sent", async (
            Guid terminalId,
            Guid orderId,
            Guid itemId,
            VoidSentItemRequestV1 request,
            SentItemVoidStore store,
            IOrderRepository orders,
            IRoleRepository roles,
            IAuthorizationGrantService grants,
            DualScreenStore dualStore,
            IAuditEventStore auditEvents,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = await RequireCashierSessionAsync(context, terminalId, dualStore, cancellationToken);

            if (!VoidReasonCatalog.IsValid(request.ReasonCode))
                throw new InvalidItemReasonException(
                    $"Reason '{request.ReasonCode}' is not a valid void catalog reason.");

            var permissions = await roles.GetPermissionCodesForUserAsync(userId, cancellationToken);
            if (!permissions.Contains(ApplicationPermissions.BillsVoid, StringComparer.Ordinal))
            {
                var role = await roles.GetGoverningRoleForUserAsync(userId, cancellationToken);
                if (role is null)
                    throw new AuthorizationDeniedException(userId, ApplicationPermissions.BillsVoid, "Requester has no assigned role.");

                // Own-check (V1-RMD-111, garson-masa design): the guard reads
                // the order's real server via ServingUserId, same as /comp.
                var order = await orders.GetByIdAsync(orderId, cancellationToken)
                    ?? throw new OrderItemNotFoundException(orderId, itemId);

                var resolution = await grants.RequestAsync(
                    new GrantRequest(
                        request.IdempotencyKey,
                        ApplicationPermissions.BillsVoid,
                        userId,
                        role.Code,
                        request.ReasonCode,
                        0m,
                        SubjectType: "OrderItem",
                        SubjectId: itemId,
                        SubjectServingUserId: order.ServingUserId),
                    cancellationToken);

                switch (resolution.Outcome)
                {
                    case GrantOutcome.Refused:
                        return Results.Json(
                            new { error = new { code = "GRANT_DENIED", message = "İptal talebi reddedildi." } },
                            statusCode: StatusCodes.Status403Forbidden);
                    case GrantOutcome.Pending:
                        return Results.Accepted(value: new VoidSentItemResultV1(
                            "Pending", orderId, itemId, null, null, null, null, null, null, resolution.Grant.GrantId));
                    case GrantOutcome.Authorized:
                        break;
                    default:
                        throw new InvalidOperationException($"Unhandled grant outcome '{resolution.Outcome}'.");
                }
            }

            // V1-RMD-127: see the matching note on the /void endpoint above —
            // OrderManagementExceptionFilter already maps every one of these
            // to the same status/error code with the correct Turkish text.
            var command = new SentItemVoidCommand(
                orderId,
                itemId,
                request.ExpectedRowVersion,
                userId,
                request.ReasonCode,
                CorrelationId: context.TraceIdentifier,
                request.Notes);
            var result = await store.VoidAsync(command, cancellationToken);
            await auditEvents.AppendAsync(
                new AuditEvent(
                    id: Guid.NewGuid(),
                    eventName: "order-item.void-sent",
                    aggregateType: "OrderItem",
                    aggregateId: itemId,
                    actorType: "User",
                    correlationId: context.TraceIdentifier,
                    actorId: userId,
                    reason: request.ReasonCode,
                    afterStateJson: JsonSerializer.Serialize(new
                    {
                        kitchenTicketItemCancelled = result.KitchenTicketItemCancelled,
                        billLineConvertedToWaste = result.BillLineConvertedToWaste,
                    })),
                cancellationToken);
            return Results.Ok(new VoidSentItemResultV1(
                "Applied",
                result.OrderId,
                result.OrderItemId,
                result.NewOrderRowVersion,
                result.NewOrderTotal,
                result.KitchenTicketItemCancelled,
                result.BillLineConvertedToWaste,
                result.StockRestored,
                result.AppliedAt,
                null));
        });

        // V1-RMD-137: found by an independent audit (2026-09-09) — an
        // age-restricted order (V12-NFC-002) is deliberately parked at
        // PendingConfirmation for a staff ID check at the point of service,
        // but nothing could ever move it out of that state. Gated by
        // orders.create, same as /void above — routine order-lifecycle
        // action every staff role holds, not a grant-class money decision.
        group.MapPost("/{orderId:guid}/accept", async (
            Guid terminalId,
            Guid orderId,
            AcceptPendingOrderRequestV1 request,
            PendingOrderConfirmationStore store,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = await RequireCashierPermissionAsync(
                context, terminalId, dualStore, authorization, ApplicationPermissions.OrdersCreate, cancellationToken);

            var result = await store.AcceptAsync(
                orderId, request.ExpectedRowVersion, userId, request.Notes, cancellationToken);
            return Results.Ok(result);
        });

        group.MapPost("/{orderId:guid}/reject", async (
            Guid terminalId,
            Guid orderId,
            RejectPendingOrderRequestV1 request,
            PendingOrderConfirmationStore store,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = await RequireCashierPermissionAsync(
                context, terminalId, dualStore, authorization, ApplicationPermissions.OrdersCreate, cancellationToken);

            var result = await store.RejectAsync(
                orderId, request.ExpectedRowVersion, userId, request.Reason, cancellationToken);
            return Results.Ok(result);
        });

        // V1-RMD-177: found by the 2026-09-10 Garson audit — /transfer-server
        // had no client anywhere, and the reason turned out to be deeper than
        // "nobody built the button": no session below manager level had any
        // way to list staff at all to pick a hand-off target from. Deliberately
        // minimal (id + display name, no username/role data) and available to
        // every cashier session — picking a colleague from a list is not
        // itself a privileged act; TransferServingUserAsync below still makes
        // its own real authorization decision once a target is chosen.
        group.MapGet("/staff", async (
            Guid terminalId,
            IRoleRepository roles,
            DualScreenStore dualStore,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = await RequireCashierSessionAsync(context, terminalId, dualStore, cancellationToken);
            var staff = await roles.ListActiveUsersAsync(userId, cancellationToken);
            return Results.Ok(staff.Select(s => new StaffMemberV1(s.UserId, s.DisplayName)).ToList());
        }).RequireRateLimiting("terminal-read");

        // V1-RMD-212: same authorization level as /staff above (a cashier
        // session, nothing more privileged) - this is a read of how busy
        // each waiter already is, useful right next to the same picker
        // /staff already feeds.
        group.MapGet("/waiter-load", async (
            Guid terminalId,
            SuggestedWaiterResolver suggestedWaiter,
            DualScreenStore dualStore,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierSessionAsync(context, terminalId, dualStore, cancellationToken);
            var loads = await suggestedWaiter.ListActiveLoadsAsync(cancellationToken);
            return Results.Ok(loads);
        }).RequireRateLimiting("terminal-read");

        // V1-RMD-111: the garson-masa hand-off. Two-tier permission model
        // (Toast "Change Server" / Lightspeed "Table Ownership" precedent,
        // researched 2026-09-06): a server handing off their OWN open checks
        // needs only orders.transfer-server (every role holds it); handing
        // off ANOTHER server's checks needs orders.transfer-server-any
        // (cashier and up). This is a bulk, unconditional reassignment of
        // every non-terminal order currently attributed to FromUserId — not
        // itself grant-class; the own-check *guard* on void/comp is what
        // actually depends on the result.
        group.MapPost("/transfer-server", async (
            Guid terminalId,
            TransferServingUserRequestV1 request,
            OrderReadStore store,
            ServingHandoffNoteStore handoffNotes,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actingUserId = await RequireCashierSessionAsync(context, terminalId, dualStore, cancellationToken);

            var permissionCode = actingUserId == request.FromUserId
                ? ApplicationPermissions.OrdersTransferServer
                : ApplicationPermissions.OrdersTransferServerAny;
            await authorization.AuthorizeAsync(actingUserId, permissionCode, cancellationToken);

            // Found in an independent review (2026-09-11): this used to call
            // TransferServingUserAsync first and validate/store the note
            // second. A note over 200 characters threw AFTER the tables had
            // already been reassigned - the caller saw a 400, but the
            // transfer had already happened. LeaveAsync depends on neither
            // side of the transfer (it only needs the two user ids), so
            // validating and storing the note FIRST makes a rejected note
            // leave every table untouched, matching what the error response
            // actually tells the caller. Left by the person actually
            // performing the hand-off (not always request.FromUserId - a
            // cashier using orders.transfer-server-any transfers on someone
            // else's behalf), for the target to see once. A no-op when the
            // note is empty (LeaveAsync's own contract).
            await handoffNotes.LeaveAsync(actingUserId, request.ToUserId, request.HandoffNote, cancellationToken);
            var count = await store.TransferServingUserAsync(request.FromUserId, request.ToUserId, cancellationToken);
            return Results.Ok(new TransferServingUserResultV1(count));
        });

        // V1-WTR-013: "read once" by design - the client calls this right
        // after opening a table, gets the departing waiter's context note
        // exactly the first time (if one was left), and never sees it
        // again. No permission check beyond a valid cashier session: a
        // waiter's own pending note is never anyone else's business to
        // withhold, and ServingHandoffNoteStore only ever returns notes
        // addressed to the caller (to_user_id = userId).
        group.MapPost("/handoff-note/pop", async (
            Guid terminalId,
            ServingHandoffNoteStore handoffNotes,
            DualScreenStore dualStore,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = await RequireCashierSessionAsync(context, terminalId, dualStore, cancellationToken);
            var note = await handoffNotes.PopPendingAsync(userId, cancellationToken);
            return note is null
                ? Results.NoContent()
                : Results.Ok(new ServingHandoffNoteV1(note.Note, note.FromDisplayName, note.CreatedAt));
        }).RequireRateLimiting("terminal-read");

        // V1-WTR-021: garson-karsilastirma idea #9. Same "no permission
        // check beyond a valid session" reasoning as handoff-note/pop above
        // - these are always and only the caller's own numbers.
        group.MapGet("/my-shift-summary", async (
            Guid terminalId,
            ShiftSummaryStore store,
            DualScreenStore dualStore,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = await RequireCashierSessionAsync(context, terminalId, dualStore, cancellationToken);
            return Results.Ok(await store.GetMyShiftSummaryAsync(userId, cancellationToken));
        }).RequireRateLimiting("terminal-read");

        return group;
    }

    private static async Task<Guid> RequireCashierSessionAsync(
        HttpContext context, Guid terminalId, DualScreenStore store, CancellationToken cancellationToken)
    {
        var cashierToken = context.Request.Cookies[CashierCookieName];
        if (string.IsNullOrWhiteSpace(cashierToken))
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                cashierToken = authHeader["Bearer ".Length..].Trim();
            }
        }

        var principal = await store.AuthenticateCashierAsync(cashierToken, terminalId, cancellationToken);
        if (principal is null)
            throw new DualScreenUnauthorizedException("Cashier authentication is required.");
        return principal.UserId;
    }

    private static async Task<Guid> RequireCashierPermissionAsync(
        HttpContext context,
        Guid terminalId,
        DualScreenStore store,
        IAuthorizationService authorization,
        string permissionCode,
        CancellationToken cancellationToken)
    {
        var userId = await RequireCashierSessionAsync(context, terminalId, store, cancellationToken);
        await authorization.AuthorizeAsync(userId, permissionCode, cancellationToken);
        return userId;
    }
}

/// <summary>
/// V1-ORD-005: catches every domain exception this group's endpoints throw —
/// principally <see cref="DualScreenUnauthorizedException"/> from the shared
/// session helpers, so a missing/invalid cashier session maps to 401 rather
/// than an unhandled 500. Mirrors the per-module filter already present on
/// Billing/Catalog/Kitchen/Tables/Authorization (e.g. KitchenOperationsExceptionFilter).
/// V1-RMD-127: the void/comp/void-sent endpoints used to shadow this with
/// their own inline try/catch blocks that returned raw English ex.Message
/// text instead of the Turkish strings below — removed, so this Map is now
/// the single place those exceptions turn into a response.
/// </summary>
public sealed class OrderManagementExceptionFilter : IEndpointFilter
{
    private static readonly Action<ILogger, string, string, Exception?> LogRequestFailure =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(5200, nameof(LogRequestFailure)),
            "Order management request failed on {Path} ({TraceIdentifier}).");

    // V1-RMD-187: found by the 2026-09-12 five-agent independent Garson
    // audit — InvalidOperationException maps to a 409 below, under the
    // >=500 threshold LogRequestFailure gates on, so it was never logged
    // at all. It is thrown for many distinct reasons across this codebase
    // (order not found, "no items to submit", V1-RMD-182's own "cannot
    // fire a course from Cancelled", a genuine concurrency conflict...),
    // all reported to the caller as the same generic concurrency-conflict
    // message. A real, non-concurrency bug here left zero server-side
    // trace to diagnose it from. Warning, not Error: a 409 is an expected
    // client-facing outcome in the common (actually-concurrent) case, not
    // an alert-worthy fault.
    private static readonly Action<ILogger, string, string, Exception?> LogInvalidOperation =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(5201, nameof(LogInvalidOperation)),
            "Order management request on {Path} ({TraceIdentifier}) mapped to a 409 via InvalidOperationException.");

    private readonly ILogger<OrderManagementExceptionFilter> _logger;

    public OrderManagementExceptionFilter(ILogger<OrderManagementExceptionFilter> logger)
    {
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (Exception exception)
        {
            var mapped = Map(exception);
            if (mapped.Status >= StatusCodes.Status500InternalServerError)
            {
                LogRequestFailure(
                    _logger,
                    context.HttpContext.Request.Path,
                    context.HttpContext.TraceIdentifier,
                    exception);
            }
            else if (exception is InvalidOperationException)
            {
                LogInvalidOperation(
                    _logger,
                    context.HttpContext.Request.Path,
                    context.HttpContext.TraceIdentifier,
                    exception);
            }

            return Results.Json(
                new { error = new { code = mapped.Code, message = mapped.Message } },
                statusCode: mapped.Status);
        }
    }

    private static (int Status, string Code, string Message) Map(Exception exception) => exception switch
    {
        DualScreenUnauthorizedException => (401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
        AuthorizationDeniedException => (403, "FORBIDDEN", "Bu işlem için yetkiniz yok."),
        KeyNotFoundException or OrderItemNotFoundException or OrderNotFoundException => (404, "NOT_FOUND", "İstenen kayıt bulunamadı."),
        InvalidItemReasonException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
        // V1-WTR-013: the 200-character cap on an optional hand-off note.
        HandoffNoteTooLongException tooLong =>
            (400, "VALIDATION_FAILED", $"Devir notu en fazla {tooLong.MaxLength} karakter olabilir."),
        LateVoidRejectedException => (409, "ALREADY_SENT", "Ürün zaten mutfağa gönderilmiş."),
        ItemNotYetSentException => (409, "NOT_YET_SENT", "Ürün henüz mutfağa gönderilmedi."),
        ItemAlreadyServedException => (409, "ALREADY_SERVED", "Ürün zaten servis edildi."),
        BillNotModifiableForWasteException => (409, "BILL_NOT_MODIFIABLE", "Hesap bu durumda değiştirilemez."),
        VoidStockRestoreFailedException => (409, "STOCK_RESTORE_FAILED", "Ürünün stoğu geri verilemedi; iptal yapılmadı."),
        OrderNotAwaitingConfirmationException => (409, "ORDER_NOT_PENDING_CONFIRMATION", "Sipariş onay bekleyen durumda değil."),
        // V1-RMD-143: Semih's decision (2026-09-09) — Accept refuses outright
        // rather than silently skipping stock consumption, either because a
        // sold product has no stock mapping configured at all, or because
        // the mapped stock item does not have enough on hand right now.
        ProductStockNotConfiguredException stockNotConfigured =>
            (409, "PRODUCT_STOCK_NOT_CONFIGURED", $"'{stockNotConfigured.ProductName}' için stok tanımlanmamış, lütfen yöneticiye bildirin."),
        InsufficientOrderStockException insufficientStock =>
            (409, "INSUFFICIENT_STOCK", $"'{insufficientStock.ProductName}' için yeterli stok yok."),
        StockItemHasNoDefaultLocationException =>
            (409, "STOCK_ITEM_MISCONFIGURED", "Bu ürünün stok kalemi için bir konum tanımlanmamış, lütfen yöneticiye bildirin."),
        // V12-QRO-003: the order already holds stock for different lines (a concurrent change
        // between two accept attempts) — a refresh, not a stock shortage.
        CrossChannelReservationConflictException => (409, "CONCURRENCY_CONFLICT", "Sipariş başka bir işlem tarafından değiştirildi."),
        OrderAlreadyBilledException => (409, "ORDER_ALREADY_BILLED", "Sipariş zaten faturalandırılmış, bu işlemle reddedilemez."),
        // V1-RMD-221: found by an independent audit (2026-09-16) — must be
        // listed before the generic InvalidOperationException branch below,
        // or this never matches (CourseNotFireableException does not
        // inherit InvalidOperationException). Fixing the same request by
        // simply resending it can never succeed, unlike a real
        // CONCURRENCY_CONFLICT — the waiter needs to refresh the ticket
        // instead.
        CourseNotFireableException => (409, "COURSE_NOT_FIREABLE",
            "Bu kurs artık ateşlenemez — zaten ateşlenmiş olabilir veya sipariş durumu değişti. Listeyi yenileyin."),
        StaleOrderRowVersionException or InvalidOperationException => (409, "CONCURRENCY_CONFLICT", "Sipariş başka bir işlem tarafından değiştirildi."),
        IdempotencyKeyReusedException or SubmitOrderIdempotencyConflictException => (409, "IDEMPOTENCY_KEY_REUSED", "Bu işlem anahtarı farklı bir istek için zaten kullanılmış."),
        OrderSubmissionDispatchException => (503, "KITCHEN_DISPATCH_FAILED", "Sipariş mutfağa iletilemedi."),
        InvalidTransferTargetException => (400, "INVALID_TRANSFER_TARGET", "Devir hedefi geçersiz."),
        // V1-ORD-006: the waiter is asked what happened at the table rather
        // than having the previous party's unpaid check silently orphaned.
        TableCheckAlreadyOpenException => (409, "TABLE_CHECK_ALREADY_OPEN",
            "Bu masada kapanmamış bir hesap var. Önce hesabı kasaya gönderin."),
        // V1-SET-004: the whole feature is turned off for this deployment.
        GarsonFeatureDisabledException => (403, "FEATURE_DISABLED",
            "Bu özellik bu işletme için kapatılmış."),
        ArgumentException or BadHttpRequestException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
        // V1-RMD-162: found by the 2026-09-10 Garson audit — every
        // PostgresException used to fall straight through to the generic
        // 503 below, telling the caller the database was unreachable even
        // when the real problem was a data constraint the request itself
        // violated (e.g. V1-RMD-156's quantity/price CHECK constraints, or
        // a value too large for the column). A client-side offline queue
        // treats 503 as transient and retries forever — a request that can
        // never succeed would retry until the queue gave up or the device
        // died, rather than telling the user immediately. Same branches
        // Catalog's own error mapper already established for this table.
        PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } =>
            (409, "DUPLICATE_RESOURCE", "Aynı kayıt zaten mevcut."),
        PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } =>
            (400, "REFERENCE_NOT_FOUND", "İşaret edilen bir kayıt bulunamadı."),
        PostgresException { SqlState: PostgresErrorCodes.CheckViolation or PostgresErrorCodes.NumericValueOutOfRange } =>
            (400, "VALIDATION_FAILED", "İstek bir veri kısıtını ihlal ediyor."),
        PostgresException or NpgsqlException => (503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
        _ => (500, "INTERNAL_ERROR", "İşlem tamamlanamadı."),
    };
}

/// <summary>
/// V1-SET-004: makes any <see cref="IEscalationResolver"/> behave as if it
/// were never registered when <paramref name="feature"/> is off for this
/// deployment — lives at the Host layer specifically so the wrapped
/// resolver's own module (here, Identity) never has to depend on Settings,
/// the same boundary every Experience registration already respects.
/// </summary>
public sealed class GarsonFeatureGatedEscalationResolver : IEscalationResolver
{
    private readonly IEscalationResolver _inner;
    private readonly ISettingsService _settings;
    private readonly GarsonFeature _feature;

    public GarsonFeatureGatedEscalationResolver(
        IEscalationResolver inner, ISettingsService settings, GarsonFeature feature)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _feature = feature;
    }

    public async Task<PolicyPath?> TryResolveAsync(
        GrantRequest request, DateTimeOffset instant, CancellationToken cancellationToken = default)
    {
        if (!await GarsonFeatureToggles.IsEnabledAsync(_settings, _feature, cancellationToken))
            return null;

        return await _inner.TryResolveAsync(request, instant, cancellationToken);
    }
}
