using System.Data;
using ALKAROS.Identity.Authorization;
using ALKAROS.Orders.OrderAggregate;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.Orders;

public sealed class OrderManagementStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IOrderRepository _repository;
    private readonly IRoleRepository _roles;

    public OrderManagementStore(NpgsqlDataSource dataSource, IOrderRepository repository, IRoleRepository roles)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _roles = roles ?? throw new ArgumentNullException(nameof(roles));
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

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var existingOrder = await GetActiveOrderByTableIdInternalAsync(connection, transaction, request.TableId, cancellationToken);
        var orderId = existingOrder?.OrderId ?? Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var catalog = await ResolveCatalogProductsAsync(
            connection, transaction, request.Items.Select(i => i.ProductId), cancellationToken);

        var newItems = new List<OrderItem>();
        foreach (var i in request.Items)
        {
            if (!catalog.TryGetValue(i.ProductId, out var product))
                throw new KeyNotFoundException($"Product {i.ProductId} was not found or has no active price.");
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
                notes: request.OrderNote,
                status: OrderState.Draft,
                createdAt: now,
                updatedAt: now,
                rowVersion: 1,
                servingUserId: actingUserId
            );

            await _repository.AddAsync(order, cancellationToken);

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

        return MapToDto(order, request.TableNumber);
    }

    public async Task<OrderDto?> GetOrderByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetByIdAsync(orderId, cancellationToken);
        if (order == null) return null;

        var tableNumber = await GetTableNumberAsync(order.TableId, cancellationToken) ?? "—";
        return MapToDto(order, tableNumber);
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

        var order = await _repository.GetByIdAsync(orderId, cancellationToken);
        if (order == null) return null;

        var tableNumber = await GetTableNumberAsync(tableId, cancellationToken) ?? "—";
        return MapToDto(order, tableNumber);
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

    public async Task<OrderDto> SubmitOrderAsync(Guid orderId, long expectedRowVersion, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Order {orderId} not found.");

        order = order.Submit(changedAt: DateTimeOffset.UtcNow);
        var newVersion = await _repository.SaveAsync(order, expectedRowVersion, cancellationToken);

        var tableNumber = await GetTableNumberAsync(order.TableId, cancellationToken) ?? "—";
        return MapToDto(order, tableNumber);
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

        // One round trip for the whole draft instead of one per line.
        await using var cmd = new NpgsqlCommand(
            """
            SELECT p.product_id, p.name, p.current_price, COALESCE(t.vat_rate, 0)
            FROM catalog.products p
            LEFT JOIN catalog.tax_profiles t ON t.tax_profile_id = p.tax_profile_id AND t.active
            WHERE p.product_id = ANY(@product_ids) AND p.active AND p.current_price IS NOT NULL;
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
            (int)i.Quantity,
            i.UnitPrice,
            i.GrossAmount,
            i.Notes
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
