using ALKAROS.Audit.EventStore;
using ALKAROS.Host.Experience.Catalog;
using ALKAROS.Host.Experience.WaiterNotifications;
using ALKAROS.Host.Experience.WebPush;
using ALKAROS.Identity.Authorization.Grants;
using ALKAROS.Kitchen.OrderItemStateSync;
using ALKAROS.Kitchen.PhysicalPrintRecovery;
using ALKAROS.Kitchen.PrintQueue;
using ALKAROS.Kitchen.Routing;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.Messaging;
using ALKAROS.Operations.BackupHealth;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Settings.KitchenDenseModeThreshold;
using ALKAROS.Settings.KitchenLiveSync;
using ALKAROS.Settings.TypedSettings;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.KitchenOperations;

public sealed class KitchenOperationsStore
{
    private readonly IKitchenTicketRepository _tickets;
    private readonly IPrinterRepository _printers;
    private readonly IPrinterRouteRepository _routes;
    private readonly IPrintQueueRepository _printJobs;
    private readonly IPhysicalPrintRecoveryRepository _deliveries;
    private readonly IBackupHealthService _backupHealth;
    private readonly IAuditEventStore _audit;
    private readonly ISettingsService _settings;
    private readonly OutboxStore _outbox;
    private readonly IOrderRepository _orders;
    private readonly IHubContext<WaiterOrderStatusHub> _waiterHub;
    private readonly WebPushSender? _push;
    private readonly NpgsqlDataSource _dataSource;
    private readonly CatalogManagementStore _catalog;
    private readonly IAuthorizationGrantRepository _grants;

    public KitchenOperationsStore(
        IKitchenTicketRepository tickets,
        IPrinterRepository printers,
        IPrinterRouteRepository routes,
        IPrintQueueRepository printJobs,
        IPhysicalPrintRecoveryRepository deliveries,
        IBackupHealthService backupHealth,
        IAuditEventStore audit,
        ISettingsService settings,
        OutboxStore outbox,
        IOrderRepository orders,
        IHubContext<WaiterOrderStatusHub> waiterHub,
        NpgsqlDataSource dataSource,
        CatalogManagementStore catalog,
        IAuthorizationGrantRepository grants,
        WebPushSender? push = null)
    {
        _tickets = tickets ?? throw new ArgumentNullException(nameof(tickets));
        _printers = printers ?? throw new ArgumentNullException(nameof(printers));
        _routes = routes ?? throw new ArgumentNullException(nameof(routes));
        _printJobs = printJobs ?? throw new ArgumentNullException(nameof(printJobs));
        _deliveries = deliveries ?? throw new ArgumentNullException(nameof(deliveries));
        _backupHealth = backupHealth ?? throw new ArgumentNullException(nameof(backupHealth));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        // V1-KIT-005: publishes the item's new state for Orders to mirror,
        // gated by kitchen.live_sync_enabled (V1-SET-002, default off).
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        // V1-WTR-009: broadcasts "ready" to every connected waiter device,
        // same gate.
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _waiterHub = waiterHub ?? throw new ArgumentNullException(nameof(waiterHub));
        // V1-WTR-011: the same "ready" announcement, for a device whose app is
        // closed. Optional so the standalone KitchenOperations test harness
        // keeps constructing this store without a push stack — the same shape
        // NfcOrderingStore already uses for IPendingOrderAnnouncer.
        _push = push;
        // V1-KIT-008: SuspendProductAvailabilityAsync reads catalog.products
        // directly (same precedent as KitchenOrderSubmissionDispatcher's own
        // age-restriction/category lookups — V1-RMD-137/V1-KIT-006) rather
        // than adding a new cross-module port, and reuses Catalog's own
        // write path (CatalogManagementStore) instead of touching
        // Product.cs/PostgresProductRepository at all.
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _grants = grants ?? throw new ArgumentNullException(nameof(grants));
    }

    public async Task<IReadOnlyList<KitchenTicketV1>> GetActiveTicketsAsync(
        string stationId,
        CancellationToken cancellationToken)
    {
        var normalizedStation = RequireText(stationId, 100, nameof(stationId));
        var tickets = await _tickets.GetActiveByStationAsync(normalizedStation, cancellationToken);
        // V1-KIT-012: one batched query for every distinct order on this
        // station's board, not one query per ticket — several tickets
        // (different stations) can share the same orderId.
        var labels = await ResolveTableLabelsAsync(
            tickets.Select(ticket => ticket.OrderId).ToArray(), cancellationToken);
        return tickets.Select(ticket => ToDto(ticket, ResolveLabel(labels, ticket.OrderId))).ToArray();
    }

    public async Task<KitchenTicketV1> GetTicketAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        EnsureId(ticketId, nameof(ticketId));
        var ticket = await _tickets.GetByIdAsync(ticketId, cancellationToken)
            ?? throw new KitchenOperationsNotFoundException("Kitchen ticket was not found.");
        return ToDto(ticket, await ResolveTableLabelAsync(ticket.OrderId, cancellationToken));
    }

    public async Task<KitchenTicketV1> TransitionTicketAsync(
        Guid ticketId,
        TransitionKitchenTicketV1 request,
        CancellationToken cancellationToken)
    {
        EnsureId(ticketId, nameof(ticketId));
        ArgumentNullException.ThrowIfNull(request);
        EnsureVersion(request.ExpectedRowVersion, nameof(request.ExpectedRowVersion));
        var target = ParseEnum<KitchenTicketState>(request.TargetState, nameof(request.TargetState));
        var ticket = await _tickets.GetByIdAsync(ticketId, cancellationToken)
            ?? throw new KitchenOperationsNotFoundException("Kitchen ticket was not found.");
        EnsureVersion(ticket.RowVersion, request.ExpectedRowVersion, "kitchen ticket");

        if (ticket.Status == target)
        {
            return ToDto(ticket, await ResolveTableLabelAsync(ticket.OrderId, cancellationToken));
        }

        // V1-RMD-158: InvalidKitchenTransitionException comes from
        // ticket.TransitionTo itself, above SaveAsync, and means the target
        // state is unreachable from the current one (e.g. Ready -> Accepted)
        // — a domain rule violation, not a race. It used to be caught here
        // and rethrown as KitchenOperationsConcurrencyException, which told
        // the client "someone else changed this, re-fetch and retry" even
        // though a retry can never succeed: the transition is simply
        // illegal. The endpoint filter already has its own correct mapping
        // for InvalidKitchenTransitionException (DOMAIN_CONFLICT, distinct
        // from CONCURRENT_MODIFICATION) — letting it propagate reaches that
        // mapping instead of being masked here.
        KitchenTicket transitioned;
        try
        {
            transitioned = ticket.TransitionTo(target, NormalizeReason(request.Reason));
            await _tickets.SaveAsync(transitioned, request.ExpectedRowVersion, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            await ThrowConcurrencyOrNotFoundAsync(ticketId, request.ExpectedRowVersion, exception, cancellationToken);
            throw;
        }

        var canonical = await _tickets.GetByIdAsync(ticketId, cancellationToken)
            ?? throw new KitchenOperationsNotFoundException("Kitchen ticket was not found after transition.");
        return ToDto(canonical, await ResolveTableLabelAsync(canonical.OrderId, cancellationToken));
    }

    public async Task<KitchenTicketV1> TransitionItemAsync(
        Guid ticketId,
        Guid itemId,
        TransitionKitchenItemV1 request,
        CancellationToken cancellationToken)
    {
        EnsureId(ticketId, nameof(ticketId));
        EnsureId(itemId, nameof(itemId));
        ArgumentNullException.ThrowIfNull(request);
        EnsureVersion(request.ExpectedTicketRowVersion, nameof(request.ExpectedTicketRowVersion));
        EnsureVersion(request.ExpectedItemRowVersion, nameof(request.ExpectedItemRowVersion));
        var target = ParseEnum<KitchenTicketItemState>(request.TargetState, nameof(request.TargetState));
        var ticket = await _tickets.GetByIdAsync(ticketId, cancellationToken)
            ?? throw new KitchenOperationsNotFoundException("Kitchen ticket was not found.");
        EnsureVersion(ticket.RowVersion, request.ExpectedTicketRowVersion, "kitchen ticket");
        var item = ticket.Items.FirstOrDefault(value => value.Id == itemId)
            ?? throw new KitchenOperationsNotFoundException("Kitchen ticket item was not found.");
        EnsureVersion(item.RowVersion, request.ExpectedItemRowVersion, "kitchen ticket item");

        try
        {
            var transitioned = ticket.UpdateItemStatus(itemId, target, NormalizeReason(request.Reason));
            await _tickets.SaveAsync(transitioned, request.ExpectedTicketRowVersion, cancellationToken);
            var transitionedItem = transitioned.Items.First(value => value.Id == itemId);
            var liveSyncEnabled = await KitchenLiveSyncSetting.IsEnabledAsync(_settings, cancellationToken);
            await KitchenOrderItemStateSyncPublisher.PublishAsync(
                _outbox, transitioned.OrderId, transitionedItem, liveSyncEnabled, cancellationToken);
            if (liveSyncEnabled && transitionedItem.Status == KitchenTicketItemState.Ready)
                await NotifyWaitersItemIsReadyAsync(transitioned.OrderId, transitionedItem, cancellationToken);
            var canonical = await _tickets.GetByIdAsync(ticketId, cancellationToken)
                ?? throw new KitchenOperationsNotFoundException("Kitchen ticket was not found after transition.");
            return ToDto(canonical, await ResolveTableLabelAsync(canonical.OrderId, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            await ThrowConcurrencyOrNotFoundAsync(ticketId, request.ExpectedTicketRowVersion, exception, cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// V1-KIT-009: reverses an item's most recent transition within its
    /// short undo window. Reuses exactly the same post-save path
    /// (state-sync publish + ready notification) as
    /// <see cref="TransitionItemAsync"/> — the reverted status is
    /// published like any other status, and if undoing Served back to
    /// Ready lands the item on Ready again, the waiter is re-notified the
    /// same way a fresh Ready transition would (correct: it really is
    /// ready again).
    /// </summary>
    public async Task<KitchenTicketV1> UndoItemAsync(
        Guid ticketId,
        Guid itemId,
        UndoKitchenItemV1 request,
        CancellationToken cancellationToken)
    {
        EnsureId(ticketId, nameof(ticketId));
        EnsureId(itemId, nameof(itemId));
        ArgumentNullException.ThrowIfNull(request);
        EnsureVersion(request.ExpectedTicketRowVersion, nameof(request.ExpectedTicketRowVersion));
        EnsureVersion(request.ExpectedItemRowVersion, nameof(request.ExpectedItemRowVersion));
        var ticket = await _tickets.GetByIdAsync(ticketId, cancellationToken)
            ?? throw new KitchenOperationsNotFoundException("Kitchen ticket was not found.");
        EnsureVersion(ticket.RowVersion, request.ExpectedTicketRowVersion, "kitchen ticket");
        var item = ticket.Items.FirstOrDefault(value => value.Id == itemId)
            ?? throw new KitchenOperationsNotFoundException("Kitchen ticket item was not found.");
        EnsureVersion(item.RowVersion, request.ExpectedItemRowVersion, "kitchen ticket item");

        try
        {
            var undone = ticket.UndoItemStatus(itemId);
            await _tickets.SaveAsync(undone, request.ExpectedTicketRowVersion, cancellationToken);
            var undoneItem = undone.Items.First(value => value.Id == itemId);
            var liveSyncEnabled = await KitchenLiveSyncSetting.IsEnabledAsync(_settings, cancellationToken);
            await KitchenOrderItemStateSyncPublisher.PublishAsync(
                _outbox, undone.OrderId, undoneItem, liveSyncEnabled, cancellationToken);
            if (liveSyncEnabled && undoneItem.Status == KitchenTicketItemState.Ready)
                await NotifyWaitersItemIsReadyAsync(undone.OrderId, undoneItem, cancellationToken);
            var canonical = await _tickets.GetByIdAsync(ticketId, cancellationToken)
                ?? throw new KitchenOperationsNotFoundException("Kitchen ticket was not found after undo.");
            return ToDto(canonical, await ResolveTableLabelAsync(canonical.OrderId, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            await ThrowConcurrencyOrNotFoundAsync(ticketId, request.ExpectedTicketRowVersion, exception, cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// V1-WTR-009: broadcasts to every connected waiter device — there is no
    /// waiter-to-table assignment tracked anywhere in this system to target
    /// one specific device (see <see cref="WaiterOrderStatusHub"/>). A
    /// missing order is tolerated (the ticket/order pairing is normally
    /// guaranteed, but a notification is best-effort, not a correctness
    /// path — it must never fail the transition itself).
    /// </summary>
    private async Task NotifyWaitersItemIsReadyAsync(
        Guid orderId, KitchenTicketItem item, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdAsync(orderId, cancellationToken);
        await _waiterHub.Clients.All.SendAsync(
            WaiterOrderStatusHub.OrderItemReady,
            new OrderItemReadyV1(orderId, order?.TableId, item.OrderItemId, item.ProductNameSnapshot),
            cancellationToken);

        // V1-WTR-011: SignalR only reaches a device whose app is open, which
        // is exactly the case a plated dish is not in — the waiter is on the
        // floor with the phone pocketed. The sender swallows its own failures.
        if (_push is not null)
        {
            await _push.BroadcastAsync(
                new WebPushMessage(
                    "Sipariş hazır",
                    $"{item.ProductNameSnapshot} hazır",
                    "alkaros-order-ready"),
                cancellationToken);
        }
    }

    public async Task<IReadOnlyList<PrinterV1>> GetPrintersAsync(CancellationToken cancellationToken)
        => (await _printers.GetAllAsync(cancellationToken)).Select(ToDto).ToArray();

    public async Task<PrinterV1> UpdatePrinterAsync(
        Guid printerId,
        UpdatePrinterV1 request,
        CancellationToken cancellationToken)
    {
        EnsureId(printerId, nameof(printerId));
        ArgumentNullException.ThrowIfNull(request);
        var current = await _printers.GetByIdAsync(printerId, cancellationToken)
            ?? throw new KitchenOperationsNotFoundException("Printer was not found.");
        var updated = new Printer(
            printerId,
            RequireText(request.Name, 200, nameof(request.Name)),
            RequireText(request.StationId, 100, nameof(request.StationId)),
            current.IpAddress,
            current.Port,
            request.IsActive,
            current.CreatedAt,
            DateTimeOffset.UtcNow);
        await _printers.SaveAsync(updated, cancellationToken);
        return ToDto(updated);
    }

    public async Task<IReadOnlyList<PrinterRouteV1>> GetRoutesAsync(CancellationToken cancellationToken)
        => (await _routes.GetAllAsync(cancellationToken)).Select(ToDto).ToArray();

    /// <summary>
    /// Found while wiring category-level printer routing end to end
    /// (2026-09-12): <see cref="UpdateRouteAsync"/> requires the route to
    /// already exist, but nothing anywhere ever created one — the manage
    /// endpoint could edit a route, never make a new one. The repository's
    /// own <c>SaveRouteAsync</c> is already an upsert (<c>ON CONFLICT (id)
    /// DO UPDATE</c>); <paramref name="routeId"/> is caller-generated, the
    /// same client-generated-id convention this codebase already uses
    /// elsewhere (an order line's own id, for one).
    /// </summary>
    public async Task<PrinterRouteV1> CreateRouteAsync(
        Guid routeId,
        UpdatePrinterRouteV1 request,
        CancellationToken cancellationToken)
    {
        EnsureId(routeId, nameof(routeId));
        ArgumentNullException.ThrowIfNull(request);
        var level = ParseEnum<RouteLevel>(request.RouteLevel, nameof(request.RouteLevel));
        var created = new PrinterRoute(
            routeId,
            level,
            request.PrinterId,
            request.ItemId,
            request.ProductId,
            request.CategoryId,
            request.SpecialDate,
            request.IsActive);
        await _routes.SaveRouteAsync(created, cancellationToken);
        return ToDto(created);
    }

    public async Task<PrinterRouteV1> UpdateRouteAsync(
        Guid routeId,
        UpdatePrinterRouteV1 request,
        CancellationToken cancellationToken)
    {
        EnsureId(routeId, nameof(routeId));
        ArgumentNullException.ThrowIfNull(request);
        var current = await _routes.GetByIdAsync(routeId, cancellationToken)
            ?? throw new KitchenOperationsNotFoundException("Printer route was not found.");
        var level = ParseEnum<RouteLevel>(request.RouteLevel, nameof(request.RouteLevel));
        var updated = new PrinterRoute(
            routeId,
            level,
            request.PrinterId,
            request.ItemId,
            request.ProductId,
            request.CategoryId,
            request.SpecialDate,
            request.IsActive,
            current.CreatedAt,
            DateTimeOffset.UtcNow);
        await _routes.SaveRouteAsync(updated, cancellationToken);
        return ToDto(updated);
    }

    public async Task<IReadOnlyList<PrintJobV1>> GetPrintJobsAsync(
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        EnsureId(ticketId, nameof(ticketId));
        return (await _printJobs.GetByTicketIdAsync(ticketId, cancellationToken)).Select(ToDto).ToArray();
    }

    public async Task<PrintJobV1> GetPrintJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        EnsureId(jobId, nameof(jobId));
        var job = await _printJobs.GetByIdAsync(jobId, cancellationToken)
            ?? throw new KitchenOperationsNotFoundException("Print job was not found.");
        return ToDto(job);
    }

    public async Task<IReadOnlyList<PhysicalPrintDeliveryV1>> GetUnknownDeliveriesAsync(CancellationToken cancellationToken)
        => (await _deliveries.GetPendingUnknownDeliveriesAsync(cancellationToken)).Select(ToDto).ToArray();

    public async Task<PhysicalPrintDeliveryV1> GetDeliveryAsync(Guid deliveryId, CancellationToken cancellationToken)
    {
        EnsureId(deliveryId, nameof(deliveryId));
        var delivery = await _deliveries.GetByIdAsync(deliveryId, cancellationToken)
            ?? throw new KitchenOperationsNotFoundException("Physical print delivery was not found.");
        return ToDto(delivery);
    }

    public async Task<PhysicalPrintDeliveryV1> ApproveReprintAsync(
        Guid deliveryId,
        ReprintDecisionV1 request,
        Guid operatorId,
        CancellationToken cancellationToken)
    {
        EnsureId(deliveryId, nameof(deliveryId));
        EnsureId(operatorId, nameof(operatorId));
        ArgumentNullException.ThrowIfNull(request);
        var reason = RequireText(request.Reason, 500, nameof(request.Reason));
        var delivery = await _deliveries.GetByIdAsync(deliveryId, cancellationToken)
            ?? throw new KitchenOperationsNotFoundException("Physical print delivery was not found.");
        var approved = delivery.ApproveReprint(operatorId.ToString("D"), reason, DateTimeOffset.UtcNow);
        await _deliveries.SaveAsync(approved, cancellationToken);
        return ToDto(approved);
    }

    public async Task<PhysicalPrintDeliveryV1> RejectReprintAsync(
        Guid deliveryId,
        ReprintDecisionV1 request,
        Guid operatorId,
        CancellationToken cancellationToken)
    {
        EnsureId(deliveryId, nameof(deliveryId));
        EnsureId(operatorId, nameof(operatorId));
        ArgumentNullException.ThrowIfNull(request);
        var reason = RequireText(request.Reason, 500, nameof(request.Reason));
        var delivery = await _deliveries.GetByIdAsync(deliveryId, cancellationToken)
            ?? throw new KitchenOperationsNotFoundException("Physical print delivery was not found.");
        var rejected = delivery.RejectReprint(operatorId.ToString("D"), reason, DateTimeOffset.UtcNow);
        await _deliveries.SaveAsync(rejected, cancellationToken);
        return ToDto(rejected);
    }

    public async Task<HealthSnapshotV1?> GetLatestHealthAsync(CancellationToken cancellationToken)
        => (await _backupHealth.GetLatestHealthSnapshotAsync(cancellationToken)) is { } value ? ToDto(value) : null;

    // V1-KIT-010: kitchen.live_sync_enabled silently changes behavior
    // (ready-item waiter notifications, KitchenState mirroring to Orders)
    // but was never readable from the Kitchen HTTP surface — an
    // independent review (2026-09-13) found this makes the setting's
    // effect invisible on the screen it actually changes. This is the
    // same flag TransitionItemAsync already reads before publishing.
    // V1-KIT-013: kitchen.dense_mode_threshold (V1-SET-005) rides the same
    // response — both are silent per-deployment behavior toggles the
    // screen's one already-fetched-on-every-load GET should be honest about.
    public async Task<LiveSyncStatusV1> GetLiveSyncStatusAsync(CancellationToken cancellationToken)
        => new(
            await KitchenLiveSyncSetting.IsEnabledAsync(_settings, cancellationToken),
            await KitchenDenseModeThresholdSetting.GetThresholdAsync(_settings, cancellationToken));

    /// <summary>
    /// V1-KIT-008: 86 a product from the Kitchen screen itself. Reuses
    /// Catalog's own write path (<see cref="CatalogManagementStore.SetProductAvailabilityAsync"/>)
    /// — the domain (Product.cs, PostgresProductRepository) is untouched, and
    /// the existing row_version optimistic-concurrency check there is what
    /// rejects the loser of two concurrent 86 calls on the same product with
    /// <see cref="KitchenOperationsConcurrencyException"/> (mapped to 409 by
    /// the endpoint filter), not a new mechanism invented here.
    ///
    /// "Plan conflict" detection (this task's scope, item 3): the simple,
    /// false-positive-tolerant first rule is whether the product was still
    /// on active sale (IsAvailable = true) the instant before this call — an
    /// already-suspended product being 86'd again (idempotent retry) is not
    /// a conflict. When it is, an already-resolved, informational
    /// identity.authorization_grants row is written directly via
    /// <see cref="IAuthorizationGrantRepository.InsertAsync"/> — deliberately
    /// bypassing <c>IAuthorizationGrantService.RequestAsync</c>, whose
    /// idempotency/own-check/policy/escalation pipeline exists to gate an
    /// action the requester does NOT hold outright and ends in a *blocking*
    /// Pending state; a chef who already holds kitchen.availability.suspend
    /// outright needs the opposite — the suspend must take effect
    /// immediately, and this row is a durable, queryable notification a
    /// manager can find (reporting.authorization_grant_daily and a manual
    /// query both already read granted rows this way), not a live push
    /// (out of scope here — see the task's Goal for why not
    /// WaiterOrderStatusHub). InsertAsync's own contract explicitly allows
    /// this: "must be Pending... or a terminal status with Path set".
    /// </summary>
    public async Task<ProductAvailabilitySuspendedV1> SuspendProductAvailabilityAsync(
        Guid productId,
        KitchenOperationsPrincipal principal,
        CancellationToken cancellationToken)
    {
        EnsureId(productId, nameof(productId));
        ArgumentNullException.ThrowIfNull(principal);

        var wasAvailable = await IsProductAvailableAsync(productId, cancellationToken)
            ?? throw new KitchenOperationsNotFoundException("Product was not found.");

        ProductV1 suspended;
        try
        {
            suspended = await _catalog.SetProductAvailabilityAsync(
                productId, new SetProductAvailabilityV1(false), cancellationToken)
                ?? throw new KitchenOperationsNotFoundException("Product was not found.");
        }
        catch (InvalidOperationException)
        {
            throw new KitchenOperationsConcurrencyException(
                $"Product {productId} was updated concurrently; retry the 86.");
        }

        var planConflict = wasAvailable;
        if (planConflict)
            await RaisePlanConflictGrantAsync(productId, principal, cancellationToken);

        return new ProductAvailabilitySuspendedV1(suspended.Id, suspended.IsAvailable, planConflict);
    }

    private async Task<bool?> IsProductAvailableAsync(Guid productId, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            "SELECT is_available FROM catalog.products WHERE product_id = @id;");
        command.Parameters.AddWithValue("id", productId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is bool value ? value : null;
    }

    private async Task RaisePlanConflictGrantAsync(
        Guid productId, KitchenOperationsPrincipal principal, CancellationToken cancellationToken)
    {
        var roleCode = await GetPrimaryRoleCodeAsync(principal.UserId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        // This bypasses IAuthorizationGrantService.RequestAsync entirely (see
        // the method doc-comment), so idempotency_key is not consulted for
        // command replay here — it only needs to satisfy the table's own
        // uniqueness constraint. Each real 86 is its own event, so a fresh
        // random key per call is correct, not a fabricated stand-in.
        var grant = new AuthorizationGrant(
            GrantId: Guid.NewGuid(),
            IdempotencyKey: $"kitchen.availability.suspend.plan-conflict:{Guid.NewGuid():N}",
            PermissionCode: KitchenOperationsEndpoints.AvailabilitySuspendPermission,
            RequesterUserId: principal.UserId,
            RequesterRoleCode: roleCode,
            SubjectType: "product",
            SubjectId: productId,
            SubjectServingUserId: null,
            Amount: 0m,
            ReasonCode: "kitchen.availability.suspend.plan-conflict",
            RequestedAt: now,
            Status: GrantStatus.Granted,
            Path: PolicyPath.Auto,
            ApproverUserId: null,
            ResolvedAt: now);
        await _grants.InsertAsync(grant, cancellationToken);
    }

    private async Task<string> GetPrimaryRoleCodeAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT r.code
            FROM identity.user_roles ur
            JOIN identity.roles r ON r.role_id = ur.role_id
            WHERE ur.user_id = @user_id
            ORDER BY r.code
            LIMIT 1;
            """);
        command.Parameters.AddWithValue("user_id", userId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result as string ?? "unknown";
    }

    public async Task<IReadOnlyList<BackupV1>> GetRecentBackupsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 100.");
        return (await _backupHealth.GetRecentBackupsAsync(limit, cancellationToken)).Select(ToDto).ToArray();
    }

    public async Task<BackupV1> ExecuteBackupAsync(
        StartBackupV1 request,
        ProductionBackupOptions options,
        IProductionBackupPayloadProvider? payloadProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        var backupType = ParseEnum<BackupType>(request.BackupType, nameof(request.BackupType));
        if (request.RetentionDays is < 1 or > 3650)
            throw new ArgumentOutOfRangeException(nameof(request), "RetentionDays must be between 1 and 3650.");
        if (payloadProvider is null)
            throw new BackupProviderUnavailableException();
        var destination = string.IsNullOrWhiteSpace(options.BackupDirectory)
            ? throw new BackupProviderUnavailableException()
            : RequireText(options.BackupDirectory, 500, nameof(options.BackupDirectory));

        var payload = await payloadProvider.CreatePayloadAsync(backupType, cancellationToken);
        if (payload.Length == 0)
            throw new BackupProviderUnavailableException();
        try
        {
            var record = await _backupHealth.ExecuteBackupAsync(
                new StartBackupCommand(backupType, destination, request.RetentionDays),
                payload,
                cancellationToken);
            return ToDto(record);
        }
        catch (BackupExecutionException)
        {
            throw;
        }
    }

    public async Task<IReadOnlyList<AuditEventV1>> GetAuditByAggregateAsync(
        string aggregateType,
        Guid aggregateId,
        CancellationToken cancellationToken)
    {
        var normalizedType = RequireText(aggregateType, 100, nameof(aggregateType));
        EnsureId(aggregateId, nameof(aggregateId));
        return (await _audit.GetByAggregateAsync(normalizedType, aggregateId, cancellationToken)).Select(ToDto).ToArray();
    }

    public async Task<IReadOnlyList<AuditEventV1>> GetAuditByCorrelationAsync(
        string correlationId,
        CancellationToken cancellationToken)
    {
        var normalized = RequireText(correlationId, 200, nameof(correlationId));
        return (await _audit.GetByCorrelationIdAsync(normalized, cancellationToken)).Select(ToDto).ToArray();
    }

    private async Task ThrowConcurrencyOrNotFoundAsync(
        Guid ticketId,
        long expected,
        InvalidOperationException original,
        CancellationToken cancellationToken)
    {
        var actual = await _tickets.GetByIdAsync(ticketId, cancellationToken);
        if (actual is null)
            throw new KitchenOperationsNotFoundException("Kitchen ticket was not found.");
        throw new KitchenOperationsConcurrencyException(
            $"Kitchen ticket row version {actual.RowVersion} does not match expected version {expected}.");
    }

    /// <summary>
    /// V1-KIT-012: one batched read for every ticket's table label, resolved
    /// fresh from orders.orders + table_mgmt.tables — never stored on
    /// kitchen.kitchen_tickets. A LEFT JOIN so a table-less order (no
    /// orders.orders.table_id) still returns the order id with a null table
    /// number, rather than dropping the row. Independent review (2026-09-14)
    /// found the up-front claim here overstated: orders.orders.table_id has
    /// an ON DELETE RESTRICT foreign key to table_mgmt.tables, so a table a
    /// live order still points at can never actually be deleted — the "a
    /// table row was deleted out from under an order" case this LEFT JOIN
    /// was also written to guard against cannot occur in this schema. The
    /// join stays (no reason to make this read fail closed on a NULL
    /// table_id), the claim is just narrowed to what is real.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, (Guid? TableId, string? TableNumber)>> ResolveTableLabelsAsync(
        IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, (Guid? TableId, string? TableNumber)>();
        var distinctOrderIds = orderIds.Distinct().ToArray();
        if (distinctOrderIds.Length == 0)
            return result;

        await using var command = _dataSource.CreateCommand(
            """
            SELECT o.order_id, o.table_id, t.table_number
            FROM orders.orders o
            LEFT JOIN table_mgmt.tables t ON t.table_id = o.table_id
            WHERE o.order_id = ANY(@order_ids);
            """);
        command.Parameters.Add("order_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = distinctOrderIds;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result[reader.GetGuid(0)] = (
                reader.IsDBNull(1) ? null : reader.GetGuid(1),
                reader.IsDBNull(2) ? null : reader.GetString(2));
        }
        return result;
    }

    private async Task<(Guid? TableId, string? TableNumber)> ResolveTableLabelAsync(
        Guid orderId, CancellationToken cancellationToken)
    {
        var labels = await ResolveTableLabelsAsync([orderId], cancellationToken);
        return ResolveLabel(labels, orderId);
    }

    private static (Guid? TableId, string? TableNumber) ResolveLabel(
        IReadOnlyDictionary<Guid, (Guid? TableId, string? TableNumber)> labels, Guid orderId)
        => labels.TryGetValue(orderId, out var label) ? label : (null, null);

    private static KitchenTicketV1 ToDto(KitchenTicket value, (Guid? TableId, string? TableNumber) tableLabel = default)
        => new(
            value.Id,
            value.OrderId,
            value.TicketNumber,
            value.StationId,
            value.Status.ToString(),
            value.RowVersion,
            value.CreatedAt,
            value.UpdatedAt,
            value.AcceptedAt,
            value.ReadyAt,
            value.CancelledAt,
            value.TargetPrepMinutes,
            value.Items.Select(ToDto).ToArray(),
            tableLabel.TableId,
            tableLabel.TableNumber);

    private static KitchenTicketItemV1 ToDto(KitchenTicketItem value)
        => new(
            value.Id,
            value.OrderItemId,
            value.ProductId,
            value.ProductNameSnapshot,
            value.Quantity,
            value.ModifiersSummary,
            value.Notes,
            value.Status.ToString(),
            value.RowVersion,
            value.CreatedAt,
            value.UpdatedAt,
            value.ReadyAt,
            value.ServedAt,
            value.CancelledAt,
            value.IsAgeRestricted);

    private static PrinterV1 ToDto(Printer value)
        => new(value.Id, value.Name, value.StationId, value.IsActive, value.CreatedAt, value.UpdatedAt);

    private static PrinterRouteV1 ToDto(PrinterRoute value)
        => new(value.Id, value.RouteLevel.ToString(), value.PrinterId, value.ItemId, value.ProductId,
            value.CategoryId, value.SpecialDate, value.IsActive, value.CreatedAt, value.UpdatedAt);

    private static PrintJobV1 ToDto(PrintJob value)
        => new(value.Id, value.TicketId, value.PrinterId, value.Status.ToString(), value.AttemptCount,
            value.MaxAttempts, value.NextAttemptAt, value.LeaseExpiresAt, value.PrintedAt, value.FailedAt,
            value.CreatedAt, value.UpdatedAt);

    private static PhysicalPrintDeliveryV1 ToDto(PhysicalPrintDelivery value)
        => new(value.Id, value.PrintJobId, value.TicketId, value.PrinterId, value.Status.ToString(),
            value.AttemptNumber, value.IsReprint, value.OperatorReason, value.CrashWindowReason,
            value.CreatedAt, value.DeliveredAt, value.ResolvedAt, value.RowVersion);

    private static BackupV1 ToDto(BackupRecord value)
        => new(value.BackupId, value.BackupType.ToString(), value.FileSizeBytes, value.Status.ToString(),
            value.ErrorMessage, value.StartedAt, value.CompletedAt, value.RetentionDays);

    private static HealthSnapshotV1 ToDto(SystemHealthSnapshotRecord value)
        => new(value.SnapshotId, value.DatabaseStatus.ToString(), value.DiskStatus.ToString(),
            value.LastBackupStatus.ToString(), value.FreeDiskBytes, value.DatabaseSizeBytes, value.CapturedAt);

    private static AuditEventV1 ToDto(AuditEvent value)
        => new(value.Id, value.EventName, value.AggregateType, value.AggregateId, value.ActorType,
            value.Reason, value.CorrelationId, value.OccurredAt);

    private static T ParseEnum<T>(string? value, string parameterName) where T : struct, Enum
    {
        if (!Enum.TryParse<T>(value, true, out var parsed) || !Enum.IsDefined(parsed))
            throw new ArgumentException($"{parameterName} is not a supported value.", parameterName);
        return parsed;
    }

    private static string RequireText(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
            throw new ArgumentException($"{parameterName} is too long.", parameterName);
        return normalized;
    }

    private static string? NormalizeReason(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : RequireText(value, 500, nameof(value));

    private static void EnsureId(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
            throw new ArgumentException($"{parameterName} cannot be empty.", parameterName);
    }

    private static void EnsureVersion(long value, string parameterName)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(parameterName, "Row version must be positive.");
    }

    private static void EnsureVersion(long actual, long expected, string resource)
    {
        if (actual != expected)
            throw new KitchenOperationsConcurrencyException($"The {resource} was changed by another operation.");
    }
}

public sealed class BackupProviderUnavailableException : Exception
{
    public BackupProviderUnavailableException()
        : base("A verified production backup payload provider is not configured.") { }
}
