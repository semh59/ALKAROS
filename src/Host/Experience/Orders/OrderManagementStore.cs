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
                modifiers: null,
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

            await using var cmd = new NpgsqlCommand(
                """
                UPDATE table_mgmt.tables
                SET current_order_id = @order_id,
                    current_status = 'Occupied',
                    row_version = row_version + 1
                WHERE table_id = @table_id;
                """, connection, transaction);
            cmd.Parameters.Add("order_id", NpgsqlDbType.Uuid).Value = orderId;
            cmd.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = request.TableId;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
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
            // A retried request (e.g. the offline queue resending a call whose
            // response was lost to a dropped connection) carries the same
            // client-generated item ids as the round it already applied —
            // drop those from newItems instead of appending a duplicate line.
            var alreadyPersistedIds = currentOrder.Items.Select(item => item.Id).ToHashSet();
            var mergedItems = currentOrder.Items
                .Concat(newItems.Where(item => !alreadyPersistedIds.Contains(item.Id)))
                .ToList();
            var orderNumber = $"TBL-{request.TableNumber}-{orderId.ToString("N")[..6].ToUpperInvariant()}";

            order = new Order(
                orderId,
                OrderSource.Waiter,
                orderNumber,
                mergedItems,
                tableId: request.TableId,
                // Preserves whatever submission id the order was originally
                // created with — not request.Id, which on a genuinely new
                // round of items (not a retry) legitimately differs and must
                // not overwrite the original (rebuilding an Order without an
                // explicit sourceReferenceId would otherwise silently null it).
                sourceReferenceId: currentOrder.SourceReferenceId,
                notes: request.OrderNote,
                status: OrderState.Draft,
                createdAt: existingOrder.CreatedAt,
                updatedAt: now,
                rowVersion: existingOrder.RowVersion,
                servingUserId: currentOrder.ServingUserId
            );

            // Saved through the connection/transaction already holding the
            // FOR UPDATE lock acquired above, instead of the store's other
            // connection — a separate connection would block on that lock
            // until this method returns, which never happens (self-deadlock).
            await _repository.SaveAsync(order, existingOrder.RowVersion, connection, transaction, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        // V1-RMD-143: same enrichment as every other order-viewing path
        // (GetOrderByIdAsync, LoadOrderDtoAsync) — a waiter building up a
        // table's cart sees the same "kalan stok" as one reviewing it later.
        return await WithAvailableStockAsync(MapToDto(order, request.TableNumber), cancellationToken);
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
        var enrichedItems = new List<OrderItemDto>(dto.Items.Count);
        foreach (var item in dto.Items)
        {
            var mappings = await _stockMappings.GetByProductIdAsync(item.ProductId, cancellationToken);
            decimal? availableStockQuantity = null;
            foreach (var mapping in mappings)
            {
                var stockItem = await _stockItems.GetByIdAsync(mapping.StockItemId, cancellationToken);
                if (stockItem?.DefaultLocationId is not { } locationId)
                    continue;

                var balance = await _stockBalances.GetByItemAndLocationAsync(mapping.StockItemId, locationId, cancellationToken);
                if (balance is null)
                    continue;

                // The limiting stock item decides how many more units of the
                // product can still be made — same reasoning as a real BOM.
                var unitsFromThisMapping = balance.AvailableQuantity / mapping.QuantityMultiplier;
                availableStockQuantity = availableStockQuantity is null
                    ? unitsFromThisMapping
                    : Math.Min(availableStockQuantity.Value, unitsFromThisMapping);
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

    private static async Task<OrderDto?> GetActiveOrderByTableIdInternalAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tableId, CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand(
            """
            SELECT o.order_id, o.status, o.row_version, o.created_at
            FROM orders.orders o
            WHERE o.table_id = @table_id
              AND o.status = 'Draft'
            ORDER BY o.created_at DESC
            LIMIT 1
            FOR UPDATE;
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
            CreatedAt: i.CreatedAt
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
