using System.Data;
using ALKAROS.Host.Experience.Orders;
using ALKAROS.Host.Experience.Orders.OrderStockConsumption;
using ALKAROS.Host.Experience.Orders.PendingOrderConfirmation;
using ALKAROS.Orders.Integration;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Orders.SubmitOrder;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.NfcOrdering;

/// <summary>
/// V12-NFC-001. A customer tapping a table's NFC tag reaches this store
/// directly — there is no cashier/waiter session, no permission check, and
/// no relay (this only ever runs over the restaurant's own local network,
/// see `docs/architecture/qr-relay-provider-decision.md`). An NFC tap is
/// treated as a trusted, physically-present order: it walks the order
/// straight to `Accepted` instead of waiting in `PendingConfirmation` for a
/// waiter — <b>unless</b> the cart contains an age-restricted item
/// (`V12-NFC-002`, `catalog.products.is_age_restricted`), in which case the
/// "trusted immediate accept" shortcut is withheld the same way a QR order
/// is: the order stops at `PendingConfirmation` and the table becomes
/// `Reserved`, per `docs/domain/table-reservation-policy.md`'s
/// "PendingConfirmation moves the table to Reserved" rule — the order's own
/// PendingConfirmation state is what owns the hold; no separate
/// `table_mgmt.table_reservations` row is created (that record is the
/// cashier-scheduled-reservation concept, a different one).
/// </summary>
public sealed class NfcOrderingStore
{
    // V1-RMD-138: found by an independent audit (2026-09-09) — this
    // anonymous, unauthenticated endpoint had no upper bound on either a
    // line's quantity or how many line items a single submission could
    // carry (only the domain's own quantity > 0 lower bound applied). With
    // no staff member in the loop, a malicious or buggy client could submit
    // an absurd order (a huge quantity, or thousands of lines) straight to
    // the kitchen. MaxQuantityPerItem mirrors the exact bound the
    // cashier-facing DualScreenStore.Orders.cs.AddItemAsync already
    // enforces (request.Quantity > 999); MaxItemsPerSubmission has no
    // existing precedent to mirror, chosen as a generous but finite cap no
    // real single-table order could plausibly exceed.
    private const int MaxQuantityPerItem = 999;
    private const int MaxItemsPerSubmission = 50;
    // Same bound the guest clients enforce with maxlength; the server must not trust that.
    private const int MaxSpecialInstructionsLength = 200;

    private readonly NpgsqlDataSource _dataSource;
    private readonly IOrderRepository _repository;
    private readonly SubmitOrderHandler _submitHandler;
    private readonly OrderStockConsumptionService _stockConsumption;
    /// <summary>
    /// V1-RMD-149: optional so a standalone NFC composition still builds; a
    /// missing announcer only means nobody is told, the order is unaffected.
    /// </summary>
    private readonly IPendingOrderAnnouncer? _announcer;

    public NfcOrderingStore(
        NpgsqlDataSource dataSource,
        IOrderRepository repository,
        SubmitOrderHandler submitHandler,
        OrderStockConsumptionService stockConsumption,
        IPendingOrderAnnouncer? announcer = null)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _submitHandler = submitHandler ?? throw new ArgumentNullException(nameof(submitHandler));
        _stockConsumption = stockConsumption ?? throw new ArgumentNullException(nameof(stockConsumption));
        _announcer = announcer;
    }

    public async Task<OrderDto> PlaceOrderAsync(Guid tableId, NfcOrderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Items.Count == 0)
            throw new ArgumentException("Order items cannot be empty.", nameof(request));
        if (request.Items.Count > MaxItemsPerSubmission)
            throw new ArgumentException($"Order cannot contain more than {MaxItemsPerSubmission} item lines.", nameof(request));
        foreach (var line in request.Items)
        {
            if (line.Quantity > MaxQuantityPerItem)
                throw new ArgumentException(
                    $"Quantity for product {line.ProductId} cannot exceed {MaxQuantityPerItem}.", nameof(request));
            if (line.SpecialInstructions is { Length: > MaxSpecialInstructionsLength })
                throw new ArgumentException(
                    $"Special instructions cannot exceed {MaxSpecialInstructionsLength} characters.", nameof(request));
        }

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
            var hasAgeRestrictedItem = request.Items.Any(line =>
                catalog.TryGetValue(line.ProductId, out var product) && product.IsAgeRestricted);

            var items = new List<OrderItem>();
            foreach (var line in request.Items)
            {
                if (!catalog.TryGetValue(line.ProductId, out var product))
                    throw new KeyNotFoundException($"Product {line.ProductId} was not found, is not available, or has no active price.");
                var (productName, unitPrice, taxRate, _) = product;

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
                // An age-restricted cart never gets the trusted-channel
                // shortcut, so the table must not look freely occupied
                // either — it holds as `Reserved`, exactly like an
                // unconfirmed QR order (table-reservation-policy.md).
                await using var checkInCommand = new NpgsqlCommand(
                    """
                    UPDATE table_mgmt.tables
                    SET current_order_id = @order_id,
                        current_status = @status,
                        row_version = row_version + 1
                    WHERE table_id = @table_id;
                    """, connection, transaction);
                checkInCommand.Parameters.Add("order_id", NpgsqlDbType.Uuid).Value = orderId;
                checkInCommand.Parameters.Add("status", NpgsqlDbType.Text).Value = hasAgeRestrictedItem ? "Reserved" : "Occupied";
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
    /// immediately instead of stopping at PendingConfirmation for a waiter —
    /// unless the order's own items include an age-restricted product
    /// (`V12-NFC-002`), in which case the walk deliberately stops at
    /// PendingConfirmation, same as any other channel would. Re-checked
    /// against the order's persisted items on every call (including a
    /// replay) rather than threaded through as a parameter, so a replay
    /// after a crash mid-walk makes the exact same decision the original
    /// attempt did. Safe to call again on an order that already reached (or
    /// passed) Accepted, or that is deliberately parked at
    /// PendingConfirmation — every step is a no-op once the order is past
    /// it (or not meant to go further).
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

        var hasAgeRestrictedItem = await AnyItemIsAgeRestrictedAsync(order, cancellationToken);
        // Semih's decision (2026-09-09): Accept consumes stock here too — the
        // exact same gate PendingOrderConfirmationStore.AcceptAsync applies,
        // since a missing product mapping or insufficient stock is just as
        // real for a trusted NFC tap as for a staff-confirmed order. Treated
        // the same way an age-restricted cart already is: the "trusted
        // immediate accept" shortcut is withheld, not the whole request
        // failed — the order simply stays at PendingConfirmation for a
        // waiter to resolve (who reaches the exact same check again through
        // PendingOrderConfirmationStore.AcceptAsync, and sees a real Turkish
        // reason if it is still refused).
        if (!hasAgeRestrictedItem && order.CanTransitionTo(OrderState.Accepted))
        {
            order = await TryConsumeStockAndAcceptAsync(order, cancellationToken);
        }

        var tableNumber = await GetTableNumberAsync(tableId, cancellationToken) ?? "—";

        // V1-RMD-149: an NFC order that could not take the trusted shortcut
        // (age-restricted cart, or stock refused it) is now waiting for a
        // waiter exactly like a QR order — and until this existed nobody was
        // told about either.
        if (_announcer is not null && order.Status == OrderState.PendingConfirmation)
        {
            await _announcer.AnnounceAsync(
                new PendingOrderAnnouncement(
                    order.Id, order.TableId, tableNumber, order.Items.Count, order.Total, DateTimeOffset.UtcNow),
                cancellationToken).ConfigureAwait(false);
        }

        return MapToDto(order, tableNumber);
    }

    /// <summary>
    /// Consumes stock and saves the order's own Accepted transition inside
    /// ONE shared transaction — not two separate commits. This is what
    /// actually closes the double-consumption race a split-transaction
    /// version could not: two concurrent identical requests both reading
    /// the same PendingConfirmation order would otherwise both pass
    /// <see cref="Order.CanTransitionTo"/> and both commit a stock delta
    /// before either one's order-state write could catch the other (the
    /// row-version-guarded UPDATE is the only thing serializing them).
    /// Sharing one transaction means the loser's row-locked UPDATE ...
    /// WHERE row_version = @expected blocks until the winner commits, then
    /// affects zero rows and throws — rolling back that transaction's own
    /// stock consumption right along with it, so only the winner's delta
    /// survives. Falls through to a fresh read on any of: a routine stock
    /// refusal (order stays at PendingConfirmation for a waiter), or losing
    /// that race (the winner's Accepted state is what gets returned).
    /// </summary>
    private async Task<Order> TryConsumeStockAndAcceptAsync(Order order, CancellationToken cancellationToken)
    {
        try
        {
            var accepted = order.TransitionTo(
                OrderState.Accepted, reason: "NFC güvenilir kanal - onay gerekmez.", changedAt: DateTimeOffset.UtcNow);

            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await _stockConsumption.ConsumeForAcceptedOrderAsync(
                order, QrOrderExpiryHostedService.SystemActorId, connection, transaction, cancellationToken);
            await _repository.SaveAsync(accepted, order.RowVersion, connection, transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (ProductStockNotConfiguredException)
        {
            // Stays at PendingConfirmation; a waiter resolves it later.
        }
        catch (InsufficientOrderStockException)
        {
            // Stays at PendingConfirmation; a waiter resolves it later.
        }
        catch (InvalidOperationException)
        {
            // Lost the race: a concurrent identical request already saved
            // this exact transition first. Fall through to the re-read.
        }

        return await _repository.GetByIdAsync(order.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Order {order.Id} disappeared mid-transition.");
    }

    private async Task<bool> AnyItemIsAgeRestrictedAsync(Order order, CancellationToken cancellationToken)
    {
        var productIds = order.Items.Select(i => i.ProductId).Distinct().ToArray();
        if (productIds.Length == 0)
            return false;

        await using var cmd = _dataSource.CreateCommand(
            "SELECT 1 FROM catalog.products WHERE product_id = ANY(@product_ids) AND is_age_restricted LIMIT 1;");
        cmd.Parameters.AddWithValue("product_ids", productIds);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is not null;
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

    private static async Task<Dictionary<Guid, (string Name, decimal Price, decimal TaxRate, bool IsAgeRestricted)>> ResolveCatalogProductsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, IEnumerable<Guid> productIds, CancellationToken cancellationToken)
    {
        var ids = productIds.Distinct().ToArray();
        var result = new Dictionary<Guid, (string, decimal, decimal, bool)>();
        if (ids.Length == 0)
            return result;

        // V1-RMD-128: found by an independent audit (2026-09-09) — this
        // query checked p.active (master-data existence) but not
        // p.is_available (the real-time 86/suspend toggle
        // CatalogManagementStore.SetProductAvailabilityV1 flips), unlike
        // the terminal-wide quick-sale path (DualScreenStore.Orders.cs)
        // which already checks both. A manager marking an item unavailable
        // had no effect on this self-service NFC ordering path — a
        // customer could still add it with no staff member in the loop to
        // catch it before it reached the kitchen.
        await using var cmd = new NpgsqlCommand(
            """
            SELECT p.product_id, p.name, p.current_price, COALESCE(t.vat_rate, 0), p.is_age_restricted
            FROM catalog.products p
            LEFT JOIN catalog.tax_profiles t ON t.tax_profile_id = p.tax_profile_id AND t.active
            WHERE p.product_id = ANY(@product_ids) AND p.active AND p.is_available AND p.current_price IS NOT NULL;
            """, connection, transaction);
        cmd.Parameters.AddWithValue("product_ids", ids);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result[reader.GetGuid(0)] = (reader.GetString(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetBoolean(4));
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
            i.Quantity,
            i.UnitPrice,
            i.GrossAmount,
            i.Notes,
            AvailableStockQuantity: null,
            Status: i.Status.ToString(),
            KitchenState: i.KitchenState.ToString(),
            CreatedAt: i.CreatedAt,
            Modifiers: OrderDtoAssembler.MapModifiers(i)
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

/// <summary>V12-NFC-001: the tapped table does not exist, or is disabled.</summary>
public sealed class NfcTableNotFoundException : Exception
{
    public NfcTableNotFoundException(Guid tableId) : base($"Table {tableId} was not found.") { }
}

/// <summary>
/// V12-NFC-001: the table is Reserved, Cleaning or OutOfService — a manual
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
