using System.Data;
using ALKAROS.Identity.Authorization;
using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Orders.SubmitOrder;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.Orders;

public sealed class OrderManagementStore
{
    /// <summary>
    /// V1-RMD-146: the smallest quantity <c>orders.order_items.quantity</c>
    /// NUMERIC(18,3) can hold without rounding away to zero. Half and
    /// one-and-a-half portions are well above it; this only stops a value
    /// that would silently become nothing.
    /// </summary>
    private const decimal MinimumOrderQuantity = 0.001m;

    // V1-RMD-162: found by the 2026-09-10 Garson audit — quantity, item
    // count and note length had no upper bound at all server-side. None of
    // these can overflow orders.order_items.quantity NUMERIC(18,3) or a
    // notes TEXT column outright (Postgres just stores it), so a bug or a
    // hostile client sending an enormous value used to succeed silently —
    // wasting storage/bandwidth at best, or hitting an arbitrary limit
    // somewhere downstream (a receipt printer's buffer, a phone's memory
    // rendering the ticket) with no clear error at worst. These are
    // generous, not tight: comfortably above anything a real kitchen order
    // needs, so no real request is expected to ever hit them.
    private const decimal MaximumOrderQuantity = 9999m;
    private const int MaximumItemsPerDraft = 200;
    private const int MaximumNoteLength = 1000;

    private readonly NpgsqlDataSource _dataSource;
    private readonly IOrderRepository _repository;
    private readonly IRoleRepository _roles;
    private readonly SubmitOrderHandler _submitHandler;
    private readonly IProductStockMappingRepository _stockMappings;
    private readonly IStockItemRepository _stockItems;
    private readonly IStockBalanceRepository _stockBalances;

    public OrderManagementStore(
        NpgsqlDataSource dataSource,
        IOrderRepository repository,
        IRoleRepository roles,
        SubmitOrderHandler submitHandler,
        IProductStockMappingRepository stockMappings,
        IStockItemRepository stockItems,
        IStockBalanceRepository stockBalances)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _roles = roles ?? throw new ArgumentNullException(nameof(roles));
        _submitHandler = submitHandler ?? throw new ArgumentNullException(nameof(submitHandler));
        _stockMappings = stockMappings ?? throw new ArgumentNullException(nameof(stockMappings));
        _stockItems = stockItems ?? throw new ArgumentNullException(nameof(stockItems));
        _stockBalances = stockBalances ?? throw new ArgumentNullException(nameof(stockBalances));
    }

    /// <summary>
    /// <paramref name="actingUserId"/> becomes <see cref="Order.ServingUserId"/>
    /// on a brand-new order (V1-RMD-111, garson-masa design) — the server who
    /// opens a table's tab is attributed as serving it until an explicit
    /// <see cref="TransferServingUserAsync"/> hand-off says otherwise. An
    /// existing draft keeps whoever already opened it; a second round of
    /// items from a different terminal/session does not silently reassign
    /// the check.
    /// </summary>
    public async Task<OrderDto> CreateOrUpdateTableDraftAsync(CreateTableDraftRequest request, Guid actingUserId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequestBounds(request);

        var submissionId = request.Id is { } id && id != Guid.Empty ? id : (Guid?)null;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        if (submissionId is { } sid)
        {
            // V1-RMD-123: found by an independent audit (2026-09-07) — a
            // retry of this exact submission (the offline queue resending a
            // call whose response was lost to a dropped connection) must
            // replay the order it already created, EVEN IF that order has
            // since moved past Draft (already Submitted, already dispatched
            // to the kitchen). The Draft-only lookup below cannot see it —
            // that gap used to make a retry start a second, duplicate order.
            var existingBySubmission = await FindOrderIdBySubmissionAsync(connection, transaction, request.TableId, sid, cancellationToken);
            if (existingBySubmission is { } existingOrderId)
            {
                var replay = await LoadOrderDtoAsync(existingOrderId, request.TableId, cancellationToken)
                    ?? throw new InvalidOperationException($"Order {existingOrderId} was not found replaying submission {sid}.");
                await transaction.CommitAsync(cancellationToken);
                return replay;
            }
        }

        var existingOrder = await GetActiveOrderByTableIdInternalAsync(connection, transaction, request.TableId, cancellationToken);
        var orderId = existingOrder?.OrderId ?? Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var catalog = await ResolveCatalogProductsAsync(
            connection, transaction, request.Items.Select(i => i.ProductId), cancellationToken);
        var modifierCatalog = await ResolveModifiersAsync(
            connection, transaction, request.Items, cancellationToken);
        // V1-RMD-161: found by the 2026-09-10 Garson audit — a modifier
        // group's min/max selection rule (e.g. "exactly one size", "at
        // most two extras") was never checked here; the server only
        // verified each selected id belonged to the product, then priced
        // it. A client could omit a required group entirely or pick past
        // its max and the order still went through with whatever was sent.
        var applicableGroups = await ResolveApplicableModifierGroupsAsync(
            connection, transaction, request.Items.Select(i => i.ProductId), cancellationToken);

        var newItems = new List<OrderItem>();
        foreach (var i in request.Items)
        {
            if (!catalog.TryGetValue(i.ProductId, out var product))
                throw new KeyNotFoundException($"Product {i.ProductId} was not found, is not available, or has no active price.");
            // V1-RMD-146: the contract is decimal now, so a quantity smaller
            // than the column's own precision would round to zero on the way
            // in and the aggregate's quantity > 0 rule would never see it.
            if (i.Quantity < MinimumOrderQuantity)
                throw new ArgumentOutOfRangeException(
                    nameof(request),
                    $"Quantity for product {i.ProductId} must be at least {MinimumOrderQuantity}.");
            var (productName, unitPrice, taxRate) = product;
            // V1-RMD-147: the contract carried a Modifiers field all along and
            // this constructor was hard-coded to null, so every extra a waiter
            // picked was silently dropped.
            var modifiers = BuildModifiers(i, modifierCatalog);
            ValidateModifierGroupSelections(i, modifierCatalog, applicableGroups);

            newItems.Add(new OrderItem(
                i.Id,
                orderId,
                i.ProductId,
                productName,
                i.Quantity,
                unitPrice,
                taxRate,
                skuSnapshot: null,
                discountAmount: 0,
                modifiers: modifiers,
                status: OrderItemState.Draft,
                kitchenState: KitchenState.NotSent,
                portionReservationStatus: PortionReservationStatus.NotApplicable,
                notes: i.SpecialInstructions,
                createdAt: now,
                updatedAt: now
            ));
        }

        Order order;
        if (existingOrder == null)
        {
            var orderNumber = $"TBL-{request.TableNumber}-{now:HHmmssff}";
            order = new Order(
                orderId,
                OrderSource.Waiter,
                orderNumber,
                newItems,
                tableId: request.TableId,
                sourceReferenceId: submissionId,
                notes: request.OrderNote,
                status: OrderState.Draft,
                createdAt: now,
                updatedAt: now,
                rowVersion: 1,
                servingUserId: actingUserId
            );

            try
            {
                // Through the outer connection/transaction (not the
                // store's own separate one): a concurrent duplicate insert
                // must abort THIS transaction for the catch below to see it.
                await _repository.AddAsync(order, connection, transaction, cancellationToken);
            }
            catch (PostgresException ex)
                when (ex.SqlState == PostgresErrorCodes.UniqueViolation && ex.ConstraintName == "ux_orders_table_submission")
            {
                // A concurrent identical retry (two near-simultaneous requests
                // carrying the same client-generated submission id) won this
                // race — the failed transaction cannot be reused, so a fresh
                // connection reads the winner instead of erroring the caller.
                await transaction.RollbackAsync(cancellationToken);
                if (submissionId is { } concurrentSubmissionId)
                {
                    var concurrentOrderId = await FindOrderIdBySubmissionAsync(request.TableId, concurrentSubmissionId, cancellationToken);
                    if (concurrentOrderId is { } foundOrderId)
                    {
                        return await LoadOrderDtoAsync(foundOrderId, request.TableId, cancellationToken)
                            ?? throw new InvalidOperationException($"Order {foundOrderId} was not found replaying submission {concurrentSubmissionId}.");
                    }
                }
                throw;
            }

            // V1-ORD-006: the pointer is only claimed when the table has no
            // open check on it.
            //
            // On the ordinary path this branch is only reached when there was
            // no attached check to begin with, so the condition holds by
            // construction. What it actually guards is the race: two waiters
            // opening the same empty table at the same moment both find no
            // check and both try to claim the pointer. Unconditionally, the
            // second UPDATE won the row and the first waiter's order was left
            // attached to nothing — invisible on every screen. Now the second
            // one matches no row and is told so.
            //
            // Note this is deliberately NOT how a new party is distinguished
            // from the same party's next round: nothing on the server can tell
            // those apart. The waiter says which it is by sending the previous
            // check to the cashier (SendCheckToCashierAsync), and until they
            // do, further items join the open check rather than orphaning it.
            await using var cmd = new NpgsqlCommand(
                """
                UPDATE table_mgmt.tables t
                SET current_order_id = @order_id,
                    current_status = 'Occupied',
                    row_version = row_version + 1
                WHERE t.table_id = @table_id
                  AND (t.current_order_id IS NULL
                       OR NOT EXISTS (
                            SELECT 1
                            FROM orders.orders o
                            WHERE o.order_id = t.current_order_id
                              AND o.status IN ('Draft', 'Submitted')));
                """, connection, transaction);
            cmd.Parameters.Add("order_id", NpgsqlDbType.Uuid).Value = orderId;
            cmd.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = request.TableId;
            if (await cmd.ExecuteNonQueryAsync(cancellationToken) == 0)
                throw new TableCheckAlreadyOpenException(request.TableId);
        }
        else
        {
            // A second round of items sent to a table that already has a
            // Draft order (e.g. a dessert order after starters) must be
            // appended to that order, not replace it — the previous code
            // rebuilt the item list solely from this request and silently
            // dropped everything already there (found by an independent
            // audit, 2026-09-06).
            var currentOrder = await _repository.GetByIdAsync(existingOrder.OrderId, cancellationToken)
                ?? throw new InvalidOperationException($"Order {existingOrder.OrderId} was not found during draft merge.");
            var currentById = currentOrder.Items.ToDictionary(item => item.Id);

            // V1-RMD-164: found by the 2026-09-10 Garson audit — a client-
            // generated item id already on this check used to be treated as
            // a pure retry outright and dropped, keeping whatever content
            // was already stored. Correct for an identical resend, wrong for
            // the audit's exact scenario: table-draft succeeds (the item now
            // exists, Draft, NotSent) but submit-draft fails; the waiter
            // corrects the quantity on the SAME line and resends the whole
            // draft. The old code silently kept the stale quantity forever —
            // the kitchen got the wrong amount and the screen showed the
            // round as sent. "Nothing to save" now means content-unchanged,
            // not merely id-known.
            var anyChange = newItems.Any(item =>
                !currentById.TryGetValue(item.Id, out var existing)
                || (existing.KitchenState == KitchenState.NotSent && !ItemContentUnchanged(existing, item)));

            if (!anyChange)
            {
                // A pure retry: every line already matches what is stored.
                // Saving would only bump the row version and hand the caller
                // a number the database no longer agrees with.
                await transaction.CommitAsync(cancellationToken);
                return await WithAvailableStockAsync(MapToDto(currentOrder, request.TableNumber), cancellationToken);
            }

            // V1-ORD-006: the aggregate merges the round itself, which keeps
            // everything the old hand-rebuilt `new Order(...)` had to restate
            // and got wrong: it hardcoded `status: Draft` (which would now
            // silently reopen a Submitted check), overwrote `notes` with this
            // request's OrderNote (erasing an allergy note left on round one
            // when round two carried none) and rewrote `order_number` into a
            // second format. ReconcileRound (V1-RMD-164) both appends
            // genuinely new lines and, for an existing NotSent line, replaces
            // its content with the corrected version — an already-fired line
            // stays untouched here exactly as it always did.
            order = currentOrder.ReconcileRound(newItems);

            // Saved through the connection/transaction already holding the
            // FOR UPDATE lock acquired above, instead of the store's other
            // connection — a separate connection would block on that lock
            // until this method returns, which never happens (self-deadlock).
            //
            // SaveAsync returns the post-increment row version. Discarding it
            // left the response carrying the pre-increment number, so the
            // client's next call — the submit that immediately follows —
            // failed its optimistic-concurrency check with a 409 and the
            // round could never be sent.
            var mergedRowVersion = await _repository.SaveAsync(
                order, currentOrder.RowVersion, connection, transaction, cancellationToken);
            order = order.WithRowVersion(mergedRowVersion);
        }

        await transaction.CommitAsync(cancellationToken);

        // V1-RMD-143: same enrichment as every other order-viewing path
        // (GetOrderByIdAsync, LoadOrderDtoAsync) — a waiter building up a
        // table's cart sees the same "kalan stok" as one reviewing it later.
        return await WithAvailableStockAsync(MapToDto(order, request.TableNumber), cancellationToken);
    }

    /// <summary>
    /// V1-ORD-006: detaches an open check from its table — Semih's scenario
    /// (2026-09-10): the party has eaten, got up, and is queueing at the till
    /// while new guests are already waiting for the table.
    ///
    /// The check keeps its own identity and stays open for the cashier; the
    /// table stops pointing at it and goes to Cleaning, so the next party can
    /// be seated immediately. Both halves happen in one transaction: a check
    /// that left the table but was not released, or a table released while
    /// still pointing at the check, are each worse than not doing it at all.
    /// </summary>
    public async Task<SendCheckToCashierResultV1> SendCheckToCashierAsync(
        Guid tableId, Guid orderId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        await using (var detach = new NpgsqlCommand(
            """
            UPDATE table_mgmt.tables
            SET current_order_id = NULL,
                current_status = 'Cleaning',
                row_version = row_version + 1
            WHERE table_id = @table_id
              AND current_order_id = @order_id;
            """, connection, transaction))
        {
            detach.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
            detach.Parameters.Add("order_id", NpgsqlDbType.Uuid).Value = orderId;
            if (await detach.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                // Either this check was already sent (a double tap, or the
                // other waiter got there first) or it never belonged to this
                // table. Both are "the world moved on", not a failure to
                // report as an error the waiter must act on.
                await transaction.RollbackAsync(cancellationToken);
                return new SendCheckToCashierResultV1(orderId, tableId, AlreadySent: true);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return new SendCheckToCashierResultV1(orderId, tableId, AlreadySent: false);
    }

    /// <summary>
    /// V1-ORD-006: the cashier's queue — checks that left their table and are
    /// waiting to be settled. Keyed by check, not by table: the table has
    /// already been re-seated by the time the guest reaches the till.
    /// </summary>
    public async Task<IReadOnlyList<PendingCheckSummaryV1>> GetChecksAwaitingPaymentAsync(
        CancellationToken cancellationToken = default)
    {
        await using var cmd = _dataSource.CreateCommand(
            """
            SELECT o.order_id,
                   o.order_number,
                   COALESCE(t.table_number, '—') AS table_number,
                   COUNT(i.order_item_id) FILTER (WHERE i.status = 'Active') AS item_count,
                   o.total,
                   o.created_at
            FROM orders.orders o
            LEFT JOIN orders.order_items i ON i.order_id = o.order_id
            LEFT JOIN table_mgmt.tables t ON t.table_id = o.table_id
            WHERE o.status = 'Submitted'
              AND NOT EXISTS (
                    SELECT 1 FROM table_mgmt.tables ct
                    WHERE ct.current_order_id = o.order_id)
            GROUP BY o.order_id, o.order_number, t.table_number, o.total, o.created_at
            ORDER BY o.created_at;
            """);

        var results = new List<PendingCheckSummaryV1>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new PendingCheckSummaryV1(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                (int)reader.GetInt64(3),
                reader.GetDecimal(4),
                reader.GetFieldValue<DateTimeOffset>(5)));
        }
        return results;
    }

    public async Task<OrderDto?> GetOrderByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetByIdAsync(orderId, cancellationToken);
        if (order == null) return null;

        var tableNumber = await GetTableNumberAsync(order.TableId, cancellationToken) ?? "—";
        var dto = MapToDto(order, tableNumber);
        return await WithAvailableStockAsync(dto, cancellationToken);
    }

    /// <summary>
    /// Semih's own "kalan stok bilgisi ver garsona" (2026-09-09): a staff
    /// member viewing an order — typically a PendingConfirmation one, right
    /// before deciding Accept/Reject — sees how many more units of each item
    /// the mapped stock item(s) could still cover, the exact same
    /// availableQuantity/quantityMultiplier arithmetic
    /// StockMasterEndpoints exposes per product. Null when the product has
    /// no stock mapping at all yet (not tracked) rather than a misleading
    /// zero; this is purely a display aid, it does not gate anything —
    /// OrderStockConsumptionService is the real, authoritative check that
    /// runs at Accept time.
    /// </summary>
    private async Task<OrderDto> WithAvailableStockAsync(OrderDto dto, CancellationToken cancellationToken)
    {
        // V1-RMD-156: this used to issue up to three round trips PER LINE
        // (mappings, then the stock item, then the balance, for every mapping
        // a line had) on a path every order-viewing call goes through —
        // GetOrderByIdAsync, table-draft's own response, and this same
        // method reused for GetOrderByIdAsync too. Three batched queries
        // now cover every line in the order regardless of how many it has.
        if (dto.Items.Count == 0) return dto;

        var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToArray();
        var mappings = await _stockMappings.GetByProductIdsAsync(productIds, cancellationToken);
        if (mappings.Count == 0) return dto;

        var mappingsByProduct = mappings
            .GroupBy(m => m.ProductId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var stockItemIds = mappings.Select(m => m.StockItemId).Distinct().ToArray();
        var stockItems = await _stockItems.GetByIdsAsync(stockItemIds, cancellationToken);
        var stockItemsById = stockItems.ToDictionary(s => s.Id);

        var locatedStockItemIds = stockItems
            .Where(s => s.DefaultLocationId is not null)
            .Select(s => s.Id)
            .ToArray();
        var balances = locatedStockItemIds.Length == 0
            ? []
            : await _stockBalances.GetByStockItemsAsync(locatedStockItemIds, cancellationToken);
        // stock_balances is unique per (stock_item_id, stock_location_id), not
        // per stock_item_id alone — a stock item CAN carry balance rows at
        // several locations. The single-pair lookup this replaces only ever
        // asked about one location, the item's own default, so the batch
        // result is keyed the same way: (item, its default location).
        var balanceByStockItem = balances
            .Where(b => stockItemsById.TryGetValue(b.StockItemId, out var stockItem)
                        && stockItem.DefaultLocationId == b.StockLocationId)
            .ToDictionary(b => b.StockItemId);

        var enrichedItems = new List<OrderItemDto>(dto.Items.Count);
        foreach (var item in dto.Items)
        {
            decimal? availableStockQuantity = null;
            if (mappingsByProduct.TryGetValue(item.ProductId, out var productMappings))
            {
                foreach (var mapping in productMappings)
                {
                    if (!stockItemsById.TryGetValue(mapping.StockItemId, out var stockItem)
                        || stockItem.DefaultLocationId is null)
                        continue;

                    if (!balanceByStockItem.TryGetValue(mapping.StockItemId, out var balance))
                        continue;

                    // The limiting stock item decides how many more units of
                    // the product can still be made — same reasoning as a
                    // real BOM.
                    var unitsFromThisMapping = balance.AvailableQuantity / mapping.QuantityMultiplier;
                    availableStockQuantity = availableStockQuantity is null
                        ? unitsFromThisMapping
                        : Math.Min(availableStockQuantity.Value, unitsFromThisMapping);
                }
            }

            enrichedItems.Add(item with { AvailableStockQuantity = availableStockQuantity });
        }

        return dto with { Items = enrichedItems };
    }

    public async Task<OrderDto?> GetActiveOrderByTableIdAsync(Guid tableId, CancellationToken cancellationToken = default)
    {
        await using var cmd = _dataSource.CreateCommand(
            """
            SELECT order_id
            FROM orders.orders
            WHERE table_id = @table_id
              AND status IN ('Draft', 'Submitted', 'PendingConfirmation', 'Accepted', 'Preparing', 'Ready')
            ORDER BY created_at DESC
            LIMIT 1;
            """);
        cmd.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        if (result is not Guid orderId) return null;

        return await LoadOrderDtoAsync(orderId, tableId, cancellationToken);
    }

    /// <summary>
    /// The garson-masa hand-off (V1-RMD-111): reassigns every non-terminal
    /// order currently attributed to <paramref name="fromUserId"/> to
    /// <paramref name="toUserId"/>. Callers must already have authorized
    /// orders.transfer-server (self, fromUserId == the acting user) or
    /// orders.transfer-server-any (broad) before calling this — that
    /// decision is not this method's job. Guards only against a no-op
    /// transfer and a target who cannot actually serve.
    /// </summary>
    public async Task<int> TransferServingUserAsync(Guid fromUserId, Guid toUserId, CancellationToken cancellationToken = default)
    {
        if (fromUserId == toUserId)
            throw new InvalidTransferTargetException("A server cannot transfer their own checks to themselves.");

        var (exists, active) = await _roles.GetUserStateAsync(toUserId, cancellationToken);
        if (!exists || !active)
            throw new InvalidTransferTargetException($"Target user {toUserId} does not exist or is not active.");

        return await _repository.ReassignServingUserAsync(fromUserId, toUserId, cancellationToken);
    }

    /// <summary>
    /// V1-RMD-113: found by an independent audit (2026-09-06) to be a
    /// completely separate, ad-hoc submit path that never created a
    /// kitchen ticket, never checked idempotency (despite the endpoint
    /// already accepting and discarding an OperationId), and never
    /// notified the customer display — unlike the terminal-wide quick-sale
    /// "/submit" route, which had all three via <see cref="SubmitOrderHandler"/>
    /// from day one. This now delegates to that same handler instead of
    /// reimplementing a thinner version of it, so a table order gets the
    /// identical guarantees a quick-sale order already had.
    /// </summary>
    public async Task<OrderDto> SubmitOrderAsync(
        Guid terminalId, Guid orderId, long expectedRowVersion, string operationId, Guid actorId,
        CancellationToken cancellationToken = default)
    {
        await _submitHandler.HandleAsync(
            new SubmitOrderCommand(
                $"cashier:{terminalId:D}", operationId, orderId, expectedRowVersion,
                actorId, DateTimeOffset.UtcNow, "Masa siparişi mutfağa gönderildi."),
            cancellationToken);

        return await GetOrderByIdAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order {orderId} was submitted but could not be reloaded.");
    }

    /// <summary>
    /// V1-RMD-149: orders sitting in PendingConfirmation, oldest first. The
    /// live SignalR announcement can be missed — the app was closed, the
    /// network dropped, the device just connected — and without this a
    /// waiter had no way back to a guest order that is already waiting.
    /// Reads the aggregate's own rows rather than a projection so the totals
    /// match what the confirmation screen will show.
    /// </summary>
    public async Task<IReadOnlyList<PendingOrderSummaryV1>> GetPendingOrdersAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<PendingOrderSummaryV1>();
        await using var cmd = _dataSource.CreateCommand(
            """
            SELECT o.order_id, o.table_id, COALESCE(t.table_number, ''),
                   count(i.order_item_id), o.total, o.created_at
            FROM orders.orders o
            LEFT JOIN table_mgmt.tables t ON t.table_id = o.table_id
            LEFT JOIN orders.order_items i
              ON i.order_id = o.order_id AND i.status <> 'Cancelled'
            WHERE o.status = 'PendingConfirmation'
            GROUP BY o.order_id, o.table_id, t.table_number, o.total, o.created_at
            ORDER BY o.created_at;
            """);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new PendingOrderSummaryV1(
                reader.GetGuid(0),
                reader.IsDBNull(1) ? null : reader.GetGuid(1),
                reader.GetString(2),
                (int)reader.GetInt64(3),
                reader.GetDecimal(4),
                reader.GetFieldValue<DateTimeOffset>(5)));
        }

        return results;
    }

    /// <summary>
    /// V1-RMD-147: projects an item's recorded modifiers. Shared by the two
    /// order-reading surfaces so a line looks the same whichever one served it.
    /// </summary>
    internal static IReadOnlyList<OrderItemModifierDto>? MapModifiers(OrderItem item)
        => item.Modifiers.Count == 0
            ? null
            : item.Modifiers
                .Select(m => new OrderItemModifierDto(m.ModifierId, m.ModifierNameSnapshot, m.PriceDelta, m.Quantity))
                .ToList();

    /// <summary>
    /// V1-RMD-147: resolves every requested modifier from the catalog, keyed
    /// by (productId, modifierId). A modifier only resolves for a product it
    /// actually belongs to — either directly (<c>catalog.modifiers.product_id</c>)
    /// or through a group assigned to that product
    /// (<c>catalog.product_modifier_groups</c>) — and inactive modifiers and
    /// groups are excluded. The name and price delta come from here, never
    /// from the request, exactly as the product's own name and price already do.
    /// One round trip for the whole draft.
    /// </summary>
    private static async Task<Dictionary<(Guid ProductId, Guid ModifierId), (string Name, decimal PriceDelta, Guid GroupId)>> ResolveModifiersAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<OrderItemDraftDto> items,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<(Guid, Guid), (string, decimal, Guid)>();
        var productIds = new List<Guid>();
        var modifierIds = new List<Guid>();
        foreach (var item in items)
        {
            if (item.Modifiers is not { Count: > 0 }) continue;
            foreach (var selection in item.Modifiers)
            {
                productIds.Add(item.ProductId);
                modifierIds.Add(selection.ModifierId);
            }
        }

        if (modifierIds.Count == 0)
            return result;

        await using var cmd = new NpgsqlCommand(
            """
            SELECT p.product_id, m.modifier_id, m.name, m.price_delta, m.modifier_group_id
            FROM unnest(@product_ids, @modifier_ids) AS p(product_id, modifier_id)
            JOIN catalog.modifiers m
              ON m.modifier_id = p.modifier_id AND m.active
            JOIN catalog.modifier_groups g
              ON g.modifier_group_id = m.modifier_group_id AND g.active
            LEFT JOIN catalog.product_modifier_groups pmg
              ON pmg.modifier_group_id = m.modifier_group_id
             AND pmg.product_id = p.product_id
            WHERE m.product_id = p.product_id OR pmg.product_modifier_group_id IS NOT NULL;
            """, connection, transaction);
        cmd.Parameters.AddWithValue("product_ids", productIds.ToArray());
        cmd.Parameters.AddWithValue("modifier_ids", modifierIds.ToArray());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result[(reader.GetGuid(0), reader.GetGuid(1))] = (reader.GetString(2), reader.GetDecimal(3), reader.GetGuid(4));
        }

        return result;
    }

    /// <summary>
    /// V1-RMD-161: every modifier group applicable to any of the given
    /// products (directly via <c>catalog.modifiers.product_id</c>, or
    /// through <c>catalog.product_modifier_groups</c>), with its own
    /// min/max selection rule, so <see cref="ValidateModifierGroupSelections"/>
    /// can check what a line actually selected against what its product's
    /// groups require — including a required group the client selected
    /// nothing from at all, which the per-selection resolve in
    /// <see cref="ResolveModifiersAsync"/> can never see.
    /// </summary>
    private static async Task<Dictionary<Guid, List<(Guid GroupId, int MinSelections, int MaxSelections)>>> ResolveApplicableModifierGroupsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IEnumerable<Guid> productIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, List<(Guid, int, int)>>();
        var ids = productIds.Distinct().ToArray();
        if (ids.Length == 0)
            return result;

        await using var cmd = new NpgsqlCommand(
            """
            SELECT product_id, modifier_group_id, min_selections, max_selections
            FROM (
                SELECT m.product_id, g.modifier_group_id, g.min_selections, g.max_selections
                FROM catalog.modifiers m
                JOIN catalog.modifier_groups g ON g.modifier_group_id = m.modifier_group_id AND g.active
                WHERE m.product_id = ANY(@product_ids) AND m.active
                UNION
                SELECT pmg.product_id, g.modifier_group_id, g.min_selections, g.max_selections
                FROM catalog.product_modifier_groups pmg
                JOIN catalog.modifier_groups g ON g.modifier_group_id = pmg.modifier_group_id AND g.active
                WHERE pmg.product_id = ANY(@product_ids)
            ) applicable;
            """, connection, transaction);
        cmd.Parameters.AddWithValue("product_ids", ids);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var productId = reader.GetGuid(0);
            if (!result.TryGetValue(productId, out var groups))
                result[productId] = groups = [];
            groups.Add((reader.GetGuid(1), reader.GetInt32(2), reader.GetInt32(3)));
        }

        return result;
    }

    /// <summary>
    /// V1-RMD-161: refuses a line whose modifier selections do not satisfy
    /// every one of its product's group rules — too few from a group whose
    /// min_selections > 0 (including a required group picked from not at
    /// all), or too many from a group's max_selections. A modifier with no
    /// group mapping at all (should not exist given the FK, but the join
    /// above only knows groups that resolved) is not this check's problem;
    /// BuildModifiers already refused an unresolved modifier id outright.
    /// </summary>
    private static void ValidateModifierGroupSelections(
        OrderItemDraftDto item,
        Dictionary<(Guid ProductId, Guid ModifierId), (string Name, decimal PriceDelta, Guid GroupId)> modifierCatalog,
        Dictionary<Guid, List<(Guid GroupId, int MinSelections, int MaxSelections)>> applicableGroups)
    {
        if (!applicableGroups.TryGetValue(item.ProductId, out var groups) || groups.Count == 0)
            return;

        var selectedGroupCounts = new Dictionary<Guid, int>();
        if (item.Modifiers is { Count: > 0 })
        {
            foreach (var selection in item.Modifiers)
            {
                if (!modifierCatalog.TryGetValue((item.ProductId, selection.ModifierId), out var resolved))
                    continue; // BuildModifiers throws on this; nothing to add here.
                selectedGroupCounts[resolved.GroupId] = selectedGroupCounts.GetValueOrDefault(resolved.GroupId) + 1;
            }
        }

        foreach (var group in groups)
        {
            var count = selectedGroupCounts.GetValueOrDefault(group.GroupId);
            if (count < group.MinSelections || count > group.MaxSelections)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(item),
                    $"Product {item.ProductId} modifier group {group.GroupId} requires between " +
                    $"{group.MinSelections} and {group.MaxSelections} selections; got {count}.");
            }
        }
    }

    /// <summary>
    /// V1-RMD-162: found by the 2026-09-10 Garson audit — quantity, item
    /// count and note length were unbounded server-side. Checked once,
    /// before anything touches the database, so a request that will never
    /// succeed is refused immediately with a clear 400 rather than after
    /// partial work, or (for a value large enough to trip a Postgres limit
    /// somewhere downstream) as a raw database error.
    /// </summary>
    private static void ValidateRequestBounds(CreateTableDraftRequest request)
    {
        if (request.Items.Count > MaximumItemsPerDraft)
            throw new ArgumentOutOfRangeException(
                nameof(request),
                $"A draft may not carry more than {MaximumItemsPerDraft} items; got {request.Items.Count}.");

        if (request.OrderNote is { Length: > MaximumNoteLength })
            throw new ArgumentOutOfRangeException(
                nameof(request),
                $"Order note may not exceed {MaximumNoteLength} characters; got {request.OrderNote.Length}.");

        foreach (var item in request.Items)
        {
            if (item.Quantity > MaximumOrderQuantity)
                throw new ArgumentOutOfRangeException(
                    nameof(request),
                    $"Quantity for product {item.ProductId} may not exceed {MaximumOrderQuantity}; got {item.Quantity}.");

            if (item.SpecialInstructions is { Length: > MaximumNoteLength })
                throw new ArgumentOutOfRangeException(
                    nameof(request),
                    $"Special instructions for product {item.ProductId} may not exceed {MaximumNoteLength} characters.");

            if (item.Modifiers is not { Count: > 0 }) continue;
            foreach (var modifier in item.Modifiers)
            {
                if (modifier.Quantity is { } quantity && quantity > MaximumOrderQuantity)
                    throw new ArgumentOutOfRangeException(
                        nameof(request),
                        $"Quantity for modifier {modifier.ModifierId} may not exceed {MaximumOrderQuantity}; got {quantity}.");
            }
        }
    }

    /// <summary>
    /// V1-RMD-164: whether an incoming item is content-identical to what is
    /// already stored — quantity, notes and the modifier set (by id and
    /// quantity; name/price are always catalog-resolved and so can never
    /// differ for the same id). Used to tell an idempotent retry (nothing to
    /// do) apart from a correction (must replace the stored line).
    /// </summary>
    private static bool ItemContentUnchanged(OrderItem existing, OrderItem incoming)
    {
        if (existing.Quantity != incoming.Quantity)
            return false;
        if (existing.Notes != incoming.Notes)
            return false;

        var existingModifiers = existing.Modifiers
            .Select(m => (m.ModifierId, m.Quantity))
            .OrderBy(m => m.ModifierId)
            .ToList();
        var incomingModifiers = incoming.Modifiers
            .Select(m => (m.ModifierId, m.Quantity))
            .OrderBy(m => m.ModifierId)
            .ToList();

        return existingModifiers.SequenceEqual(incomingModifiers);
    }

    /// <summary>
    /// V1-RMD-147: turns one draft line's modifier ids into domain modifiers,
    /// refusing the whole submission if any id does not belong to the product.
    /// Silently dropping it is the defect this task closes, so an unresolved
    /// id must be loud.
    /// </summary>
    private static List<OrderItemModifier>? BuildModifiers(
        OrderItemDraftDto item,
        Dictionary<(Guid ProductId, Guid ModifierId), (string Name, decimal PriceDelta, Guid GroupId)> catalog)
    {
        if (item.Modifiers is not { Count: > 0 })
            return null;

        // V1-RMD-150: a fractional portion is still at least one plate, and
        // the extra that goes on it is not fractional — so the default is the
        // ceiling of the line's quantity, not the quantity itself.
        var defaultQuantity = Math.Max(1m, Math.Ceiling(item.Quantity));

        var modifiers = new List<OrderItemModifier>(item.Modifiers.Count);
        foreach (var selection in item.Modifiers)
        {
            if (!catalog.TryGetValue((item.ProductId, selection.ModifierId), out var resolved))
            {
                throw new KeyNotFoundException(
                    $"Modifier {selection.ModifierId} was not found, is not active, or does not belong to product {item.ProductId}.");
            }

            var quantity = selection.Quantity ?? defaultQuantity;
            if (quantity < MinimumOrderQuantity)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(item),
                    $"Quantity for modifier {selection.ModifierId} must be at least {MinimumOrderQuantity}.");
            }

            modifiers.Add(new OrderItemModifier(
                id: Guid.NewGuid(),
                orderItemId: item.Id,
                modifierId: selection.ModifierId,
                modifierNameSnapshot: resolved.Name,
                priceDelta: resolved.PriceDelta,
                quantity: quantity));
        }

        return modifiers;
    }

    private static async Task<Dictionary<Guid, (string Name, decimal Price, decimal TaxRate)>> ResolveCatalogProductsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IEnumerable<Guid> productIds,
        CancellationToken cancellationToken)
    {
        var ids = productIds.Distinct().ToArray();
        var result = new Dictionary<Guid, (string, decimal, decimal)>();
        if (ids.Length == 0)
            return result;

        // V1-RMD-128: found by an independent audit (2026-09-09) — this
        // query checked p.active (master-data existence) but not
        // p.is_available (the real-time 86/suspend toggle
        // CatalogManagementStore.SetProductAvailabilityV1 flips), unlike
        // the terminal-wide quick-sale path (DualScreenStore.Orders.cs)
        // which already checks both. A manager marking an item unavailable
        // had no effect on table-draft orders — a waiter could still add
        // it, and it would still reach the kitchen.
        //
        // One round trip for the whole draft instead of one per line.
        await using var cmd = new NpgsqlCommand(
            """
            SELECT p.product_id, p.name, p.current_price, COALESCE(t.vat_rate, 0)
            FROM catalog.products p
            LEFT JOIN catalog.tax_profiles t ON t.tax_profile_id = p.tax_profile_id AND t.active
            WHERE p.product_id = ANY(@product_ids) AND p.active AND p.is_available AND p.current_price IS NOT NULL;
            """, connection, transaction);
        cmd.Parameters.AddWithValue("product_ids", ids);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result[reader.GetGuid(0)] = (reader.GetString(1), reader.GetDecimal(2), reader.GetDecimal(3));
        }

        return result;
    }

    /// <summary>
    /// V1-ORD-006: the check currently attached to this table, which is
    /// exactly what <c>table_mgmt.tables.current_order_id</c> points at.
    /// Sending a check to the cashier clears that pointer, so "attached" and
    /// "not yet sent to the cashier" are the same condition and need no extra
    /// column.
    /// </summary>
    /// <remarks>
    /// This used to match <c>status = 'Draft'</c>. The waiter client never
    /// leaves an order in Draft — it always draft-then-submits in one go — so
    /// every round after the first missed this lookup and opened a *second*
    /// order on the table, while the read path returned only the newest one.
    /// A party ordering ₺400 of starters and then ₺900 of mains showed ₺900
    /// on the bill and on the table tile, and the ₺400 was never billed.
    /// </remarks>
    private static async Task<OrderDto?> GetActiveOrderByTableIdInternalAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tableId, CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand(
            """
            SELECT o.order_id, o.status, o.row_version, o.created_at
            FROM orders.orders o
            JOIN table_mgmt.tables t ON t.current_order_id = o.order_id
            WHERE t.table_id = @table_id
              AND o.status IN ('Draft', 'Submitted')
            FOR UPDATE OF o;
            """, connection, transaction);
        cmd.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new OrderDto(
            reader.GetGuid(0),
            tableId,
            "",
            reader.GetString(1),
            reader.GetInt64(2),
            0,
            [],
            reader.GetFieldValue<DateTimeOffset>(3));
    }

    /// <summary>
    /// V1-RMD-123: looks up an existing order for the table by its
    /// client-generated submission id (see <see cref="CreateTableDraftRequest.Id"/>),
    /// regardless of the order's current status — a plain read, not FOR
    /// UPDATE, since the real serialization guard against a concurrent
    /// duplicate is the database's own partial unique index, not this check.
    /// </summary>
    private static async Task<Guid?> FindOrderIdBySubmissionAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tableId, Guid submissionId, CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand(
            """
            SELECT order_id
            FROM orders.orders
            WHERE table_id = @table_id AND source_reference_id = @submission_id
            LIMIT 1;
            """, connection, transaction);
        cmd.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
        cmd.Parameters.Add("submission_id", NpgsqlDbType.Uuid).Value = submissionId;
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is Guid orderId ? orderId : null;
    }

    /// <summary>Same lookup as above, on the store's own connection — used after
    /// a failed transaction has already been rolled back and cannot be reused.</summary>
    private async Task<Guid?> FindOrderIdBySubmissionAsync(Guid tableId, Guid submissionId, CancellationToken cancellationToken)
    {
        await using var cmd = _dataSource.CreateCommand(
            """
            SELECT order_id
            FROM orders.orders
            WHERE table_id = @table_id AND source_reference_id = @submission_id
            LIMIT 1;
            """);
        cmd.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
        cmd.Parameters.Add("submission_id", NpgsqlDbType.Uuid).Value = submissionId;
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is Guid orderId ? orderId : null;
    }

    private async Task<OrderDto?> LoadOrderDtoAsync(Guid orderId, Guid tableId, CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdAsync(orderId, cancellationToken);
        if (order == null) return null;

        var tableNumber = await GetTableNumberAsync(tableId, cancellationToken) ?? "—";
        var dto = MapToDto(order, tableNumber);
        // V1-RMD-143: shared with GetOrderByIdAsync so every order-viewing
        // route (GET /table/{tableId}, the table-draft create/replay
        // responses, and GET /{orderId}) shows the same "kalan stok" —
        // a waiter looking up an order by its table, the far more common
        // path on the floor, must not be the one view left without it.
        return await WithAvailableStockAsync(dto, cancellationToken);
    }

    private async Task<string?> GetTableNumberAsync(Guid? tableId, CancellationToken cancellationToken)
    {
        if (tableId == null) return null;
        await using var cmd = _dataSource.CreateCommand(
            "SELECT table_number FROM table_mgmt.tables WHERE table_id = @table_id;");
        cmd.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId.Value;
        var res = await cmd.ExecuteScalarAsync(cancellationToken);
        return res as string;
    }

    private static OrderDto MapToDto(Order order, string tableNumber)
    {
        var dtos = order.Items.Select(i => new OrderItemDto(
            i.Id,
            i.ProductId,
            i.ProductNameSnapshot,
            i.Quantity,
            i.UnitPrice,
            i.GrossAmount,
            i.Notes,
            AvailableStockQuantity: null,
            Status: i.Status.ToString(),
            KitchenState: i.KitchenState.ToString(),
            CreatedAt: i.CreatedAt,
            Modifiers: MapModifiers(i),
            // V1-RMD-168: mirrors ItemExceptionHandler.VoidItemAsync's own
            // eligibility check exactly.
            CanVoid: i.Status is OrderItemState.Active or OrderItemState.Draft
                && i.KitchenState == KitchenState.NotSent,
            // V1-RMD-168: mirrors SentItemVoidStore.VoidAsync's own
            // eligibility check exactly.
            CanVoidSent: i.Status == OrderItemState.Active
                && i.KitchenState != KitchenState.NotSent
                && i.KitchenState is not (KitchenState.Served or KitchenState.Cancelled)
        )).ToList();

        return new OrderDto(
            order.Id,
            order.TableId ?? Guid.Empty,
            tableNumber,
            order.Status.ToString(),
            order.RowVersion,
            order.Total,
            dtos,
            order.CreatedAt
        );
    }
}

/// <summary>V1-RMD-111: the requested server hand-off target is invalid.</summary>
public sealed class InvalidTransferTargetException : Exception
{
    public InvalidTransferTargetException(string message) : base(message) { }
}

/// <summary>
/// V1-ORD-006: the table still carries an open check that has not been sent
/// to the cashier, so a new party cannot be opened on it yet. Raised instead
/// of silently repointing the table and orphaning the unpaid check.
/// </summary>
public sealed class TableCheckAlreadyOpenException : Exception
{
    public TableCheckAlreadyOpenException(Guid tableId)
        : base($"Table {tableId} still has an open check that was not sent to the cashier.")
    {
        TableId = tableId;
    }

    public Guid TableId { get; }
}
