using System.Data;
using ALKAROS.Host.Experience.Orders;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Orders.SubmitOrder;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.NfcOrdering;

/// <summary>
/// V14-NFC-001. A customer tapping a table's NFC tag reaches this store
/// directly — there is no cashier/waiter session, no permission check, and
/// no relay (this only ever runs over the restaurant's own local network,
/// see `docs/architecture/qr-relay-provider-decision.md`). Unlike the QR
/// channel (`V14-QRO-*`, still blocked on the relay), an NFC tap is treated
/// as a trusted, physically-present order: it walks the order straight to
/// `Accepted` instead of waiting in `PendingConfirmation` for a waiter.
/// </summary>
public sealed class NfcOrderingStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IOrderRepository _repository;
    private readonly SubmitOrderHandler _submitHandler;

    public NfcOrderingStore(NpgsqlDataSource dataSource, IOrderRepository repository, SubmitOrderHandler submitHandler)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _submitHandler = submitHandler ?? throw new ArgumentNullException(nameof(submitHandler));
    }

    public async Task<OrderDto> PlaceOrderAsync(Guid tableId, NfcOrderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Items.Count == 0)
            throw new ArgumentException("Order items cannot be empty.", nameof(request));

        await using (var connection = await _dataSource.OpenConnectionAsync(cancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken))
        {
            var existingOrderId = await FindOrderIdBySubmissionAsync(connection, transaction, tableId, request.Id, cancellationToken);
            if (existingOrderId is { } replayOrderId)
            {
                await transaction.CommitAsync(cancellationToken);
                return await LoadDtoAfterEnsuringAcceptedAsync(replayOrderId, tableId, request.Id, cancellationToken);
            }

            var table = await LockTableAsync(connection, transaction, tableId, cancellationToken);
            if (table is null || !table.Value.Active)
                throw new NfcTableNotFoundException(tableId);

            var selfCheckIn = table.Value.Status == "Available";
            if (!selfCheckIn && table.Value.Status != "Occupied")
                throw new NfcTableNotAvailableException(tableId, table.Value.Status);

            var catalog = await ResolveCatalogProductsAsync(connection, transaction, request.Items.Select(i => i.ProductId), cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var orderId = Guid.NewGuid();

            var items = new List<OrderItem>();
            foreach (var line in request.Items)
            {
                if (!catalog.TryGetValue(line.ProductId, out var product))
                    throw new KeyNotFoundException($"Product {line.ProductId} was not found or has no active price.");
                var (productName, unitPrice, taxRate) = product;

                items.Add(new OrderItem(
                    line.Id,
                    orderId,
                    line.ProductId,
                    productName,
                    line.Quantity,
                    unitPrice,
                    taxRate,
                    skuSnapshot: null,
                    discountAmount: 0,
                    modifiers: null,
                    status: OrderItemState.Draft,
                    kitchenState: KitchenState.NotSent,
                    portionReservationStatus: PortionReservationStatus.NotApplicable,
                    notes: line.SpecialInstructions,
                    createdAt: now,
                    updatedAt: now));
            }

            var order = new Order(
                orderId,
                OrderSource.Nfc,
                $"NFC-{table.Value.TableNumber}-{now:HHmmssff}",
                items,
                tableId: tableId,
                sourceReferenceId: request.Id,
                status: OrderState.Draft,
                createdAt: now,
                updatedAt: now,
                rowVersion: 1);

            try
            {
                // Through the outer connection/transaction (not a separate
                // one): a concurrent duplicate insert must abort THIS
                // transaction for the catch below to see it — same pattern
                // as OrderManagementStore.CreateOrUpdateTableDraftAsync
                // (V1-RMD-123).
                await _repository.AddAsync(order, connection, transaction, cancellationToken);
            }
            catch (PostgresException ex)
                when (ex.SqlState == PostgresErrorCodes.UniqueViolation && ex.ConstraintName == "ux_orders_table_submission")
            {
                await transaction.RollbackAsync(cancellationToken);
                var concurrentOrderId = await FindOrderIdBySubmissionAsync(tableId, request.Id, cancellationToken);
                if (concurrentOrderId is { } foundOrderId)
                    return await LoadDtoAfterEnsuringAcceptedAsync(foundOrderId, tableId, request.Id, cancellationToken);
                throw;
            }

            if (selfCheckIn)
            {
                await using var checkInCommand = new NpgsqlCommand(
                    """
                    UPDATE table_mgmt.tables
                    SET current_order_id = @order_id,
                        current_status = 'Occupied',
                        row_version = row_version + 1
                    WHERE table_id = @table_id;
                    """, connection, transaction);
                checkInCommand.Parameters.Add("order_id", NpgsqlDbType.Uuid).Value = orderId;
                checkInCommand.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
                await checkInCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            return await LoadDtoAfterEnsuringAcceptedAsync(orderId, tableId, request.Id, cancellationToken);
        }
    }

    /// <summary>
    /// Walks a Draft order all the way to Accepted — Draft -&gt; Submitted
    /// (via the same <see cref="SubmitOrderHandler"/> every other channel
    /// uses, so a kitchen ticket is dispatched exactly once) -&gt;
    /// PendingConfirmation -&gt; Accepted. No new transition is invented
    /// (<see cref="Order.CanTransitionTo"/> is unchanged); this only
    /// chooses, for the trusted NFC channel, to walk the existing chain
    /// immediately instead of stopping at PendingConfirmation for a waiter.
    /// Safe to call again on an order that already reached (or passed)
    /// Accepted — every step is a no-op once the order is past it, which is
    /// what makes an idempotent submission replay correct even if a
    /// previous attempt crashed partway through this walk.
    /// </summary>
    private async Task<OrderDto> LoadDtoAfterEnsuringAcceptedAsync(
        Guid orderId, Guid tableId, Guid submissionId, CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order {orderId} was not found.");

        if (order.Status == OrderState.Draft)
        {
            await _submitHandler.HandleAsync(
                new SubmitOrderCommand(
                    ClientId: $"nfc:{tableId:D}",
                    OperationId: submissionId.ToString(),
                    OrderId: orderId,
                    ExpectedRowVersion: order.RowVersion,
                    SubmittedAt: DateTimeOffset.UtcNow,
                    Reason: "NFC güvenilir kanal - doğrudan mutfağa iletildi."),
                cancellationToken);
            order = await _repository.GetByIdAsync(orderId, cancellationToken)
                ?? throw new InvalidOperationException($"Order {orderId} was not found after submission.");
        }

        order = await TryTransitionAsync(order, OrderState.PendingConfirmation, "NFC güvenilir kanal.", cancellationToken);
        order = await TryTransitionAsync(order, OrderState.Accepted, "NFC güvenilir kanal - onay gerekmez.", cancellationToken);

        var tableNumber = await GetTableNumberAsync(tableId, cancellationToken) ?? "—";
        return MapToDto(order, tableNumber);
    }

    /// <summary>
    /// Applies one transition when the order can still make it; a no-op
    /// when it already passed <paramref name="target"/>. On a stale row
    /// version (a concurrent identical request already made this exact
    /// move) re-reads the order instead of failing — the two racing
    /// requests must resolve to the same final state, not one 200 and one
    /// error.
    /// </summary>
    private async Task<Order> TryTransitionAsync(Order order, OrderState target, string reason, CancellationToken cancellationToken)
    {
        if (!order.CanTransitionTo(target))
            return order;

        try
        {
            var next = order.TransitionTo(target, reason: reason, changedAt: DateTimeOffset.UtcNow);
            await _repository.SaveAsync(next, order.RowVersion, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // A concurrent identical request already advanced this order —
            // fall through to the re-read below either way.
        }

        // Order.TransitionTo() preserves the in-memory RowVersion unchanged
        // (only the repository commit bumps it) — reusing `next` directly
        // for a second chained transition would save it against its own
        // now-stale expected version and always fail. Re-reading after
        // every attempt (success or lost race) is what makes the next
        // TryTransitionAsync call see the real current row version.
        return await _repository.GetByIdAsync(order.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Order {order.Id} disappeared mid-transition.");
    }

    private static async Task<(string Status, string TableNumber, bool Active)?> LockTableAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tableId, CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand(
            """
            SELECT current_status, table_number, active
            FROM table_mgmt.tables
            WHERE table_id = @table_id
            FOR UPDATE;
            """, connection, transaction);
        cmd.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return (reader.GetString(0), reader.GetString(1), reader.GetBoolean(2));
    }

    private static async Task<Dictionary<Guid, (string Name, decimal Price, decimal TaxRate)>> ResolveCatalogProductsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, IEnumerable<Guid> productIds, CancellationToken cancellationToken)
    {
        var ids = productIds.Distinct().ToArray();
        var result = new Dictionary<Guid, (string, decimal, decimal)>();
        if (ids.Length == 0)
            return result;

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

    private async Task<string?> GetTableNumberAsync(Guid tableId, CancellationToken cancellationToken)
    {
        await using var cmd = _dataSource.CreateCommand(
            "SELECT table_number FROM table_mgmt.tables WHERE table_id = @table_id;");
        cmd.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result as string;
    }

    private static OrderDto MapToDto(Order order, string tableNumber)
    {
        var items = order.Items.Select(i => new OrderItemDto(
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
            items,
            order.CreatedAt);
    }
}

/// <summary>V14-NFC-001: the tapped table does not exist, or is disabled.</summary>
public sealed class NfcTableNotFoundException : Exception
{
    public NfcTableNotFoundException(Guid tableId) : base($"Table {tableId} was not found.") { }
}

/// <summary>
/// V14-NFC-001: the table is Reserved, Cleaning or OutOfService — a manual
/// reservation or a pending QR order may own it with no Order of its own
/// (table-reservation-policy.md), so appending to "the open order" is not a
/// safe assumption. NFC self-service is refused; a customer in this state
/// needs a waiter, same as an unreadable QR would.
/// </summary>
public sealed class NfcTableNotAvailableException : Exception
{
    public NfcTableNotAvailableException(Guid tableId, string status)
        : base($"Table {tableId} is not available for NFC self-service (status: {status}).") { }
}
