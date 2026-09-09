using ALKAROS.IntegrationContracts;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Orders.SubmitOrder;
using ALKAROS.Tables.TableLifecycle;
using Npgsql;

namespace ALKAROS.Orders.Integration;

/// <summary>
/// Order's reaction to a QR Ordering submission (V0-ARC-001 row 19: QR
/// Ordering → Order via integration event; QR Ordering has no direct-call
/// edge to Order). Materializes the actual <c>orders.orders</c> row from the
/// event's own price/name snapshot — no reach-back into
/// <c>catalog.products</c> — walks it Draft -&gt; Submitted (via the same
/// <see cref="SubmitOrderHandler"/> every other channel uses, so a kitchen
/// ticket is dispatched exactly once) -&gt; PendingConfirmation, and stops
/// there: unlike NFC's trusted-channel shortcut (V12-NFC-001), a QR order is
/// never auto-accepted — table status is untouched, that is V12-QRO-002's
/// concern.
/// </summary>
/// <remarks>
/// Delivery is at-least-once. Order creation is idempotent through the same
/// partial unique index NFC/table-draft rely on
/// (<c>ux_orders_table_submission</c> on <c>(table_id, source_reference_id)</c>,
/// V1-RMD-123); the Submitted/PendingConfirmation walk is idempotent because
/// every step is a no-op once the order already passed it.
/// </remarks>
public sealed class QrOrderSubmittedConsumer : IIntegrationEventConsumer
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IOrderRepository _orders;
    private readonly ITableRepository _tables;
    private readonly SubmitOrderHandler? _submitHandler;

    /// <summary>
    /// <paramref name="submitHandler"/> is optional (unlike every other
    /// dependency here) because it is registered by Host's
    /// AddOrderManagementExperience, not by any module — a pure module
    /// composition (e.g. HostModuleReachabilityTests, which never delivers
    /// an event to this consumer) has no such registration, and the
    /// built-in container falls back to this default instead of failing
    /// ValidateOnBuild. A real deployment always registers it, so
    /// <see cref="HandleAsync"/> never actually sees null there.
    /// </summary>
    public QrOrderSubmittedConsumer(
        NpgsqlDataSource dataSource, IOrderRepository orders, ITableRepository tables, SubmitOrderHandler? submitHandler = null)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _submitHandler = submitHandler;
    }

    public bool CanHandle(string eventType) => eventType == QrOrderingIntegrationEventTypes.QrOrderSubmitted;

    public async Task HandleAsync(string eventType, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        var e = IntegrationEventSerializer.Deserialize<QrOrderSubmitted>(payload.Span);

        var orderId = await FindOrderIdBySubmissionAsync(e.TableId, e.SubmissionId, cancellationToken);
        if (orderId is null)
        {
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            var newOrderId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var items = e.Items.Select(i => new OrderItem(
                i.ItemId,
                newOrderId,
                i.ProductId,
                i.ProductName,
                i.Quantity,
                i.UnitPrice,
                i.TaxRate,
                skuSnapshot: null,
                discountAmount: 0,
                modifiers: null,
                status: OrderItemState.Draft,
                kitchenState: KitchenState.NotSent,
                portionReservationStatus: PortionReservationStatus.NotApplicable,
                notes: i.Notes,
                createdAt: now,
                updatedAt: now)).ToList();

            // Derived from SubmissionId, not a timestamp: two concurrent
            // redeliveries of DIFFERENT submissions for the same table in
            // the same hundredth of a second would otherwise collide on
            // orders.order_number's own unique constraint before either
            // reached the (table_id, source_reference_id) check below.
            var order = new Order(
                newOrderId,
                OrderSource.Qr,
                $"QR-{e.TableId.ToString("N")[..6]}-{e.SubmissionId.ToString("N")[..8]}",
                items,
                tableId: e.TableId,
                sourceReferenceId: e.SubmissionId,
                status: OrderState.Draft,
                createdAt: now,
                updatedAt: now,
                rowVersion: 1);

            try
            {
                await _orders.AddAsync(order, connection, transaction, cancellationToken).ConfigureAwait(false);
                // V12-QRO-002: backfills the current_order_id cache pointer
                // in the SAME transaction as the Order insert — the table's
                // current_status was already set Reserved by QrOrdering's
                // own reservation policy at submission time (before this
                // consumer ever ran), so this only converges the pointer,
                // it never changes the status.
                await _tables.LinkCurrentOrderAsync(e.TableId, newOrderId, connection, transaction, cancellationToken)
                    .ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                orderId = newOrderId;
            }
            catch (PostgresException ex)
                when (ex.SqlState == PostgresErrorCodes.UniqueViolation
                    && ex.ConstraintName is "ux_orders_table_submission" or "orders_order_number_key")
            {
                // A redelivery raced an earlier delivery that already
                // created the order — fall through to the existing row.
                // The order number is derived from SubmissionId (see
                // above), so a concurrent duplicate insert can surface as
                // either constraint depending on which one Postgres checks
                // first; both are the same race.
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                var found = await FindOrderIdBySubmissionAsync(e.TableId, e.SubmissionId, cancellationToken).ConfigureAwait(false);
                if (found is null)
                    throw;
                orderId = found;
            }
        }

        await WalkToPendingConfirmationAsync(orderId.Value, e.TableId, e.SubmissionId, cancellationToken).ConfigureAwait(false);
    }

    private async Task WalkToPendingConfirmationAsync(Guid orderId, Guid tableId, Guid submissionId, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdAsync(orderId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Order {orderId} was not found.");

        if (order.Status == OrderState.Draft)
        {
            if (_submitHandler is null)
                throw new InvalidOperationException(
                    $"Order {orderId} cannot be submitted: SubmitOrderHandler was not configured for QrOrderSubmittedConsumer.");

            await _submitHandler.HandleAsync(
                new SubmitOrderCommand(
                    ClientId: $"qr:{tableId:D}",
                    OperationId: submissionId.ToString(),
                    OrderId: orderId,
                    ExpectedRowVersion: order.RowVersion,
                    SubmittedAt: DateTimeOffset.UtcNow,
                    Reason: "QR siparişi - personel onayı bekleniyor."),
                cancellationToken).ConfigureAwait(false);
            order = await _orders.GetByIdAsync(orderId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Order {orderId} was not found after submission.");
        }

        await TryTransitionAsync(order, OrderState.PendingConfirmation, "QR siparişi - personel onayı bekleniyor.", cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Applies one transition when the order can still make it; a no-op when
    /// it already passed <paramref name="target"/> — same pattern as
    /// NfcOrderingStore.TryTransitionAsync (V12-NFC-001): a concurrent
    /// identical redelivery must resolve to the same final state, not one
    /// success and one error.
    /// </summary>
    private async Task<Order> TryTransitionAsync(Order order, OrderState target, string reason, CancellationToken cancellationToken)
    {
        if (!order.CanTransitionTo(target))
            return order;

        try
        {
            var next = order.TransitionTo(target, reason: reason, changedAt: DateTimeOffset.UtcNow);
            await _orders.SaveAsync(next, order.RowVersion, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // A concurrent redelivery already advanced this order — fall
            // through to the re-read below either way.
        }

        return await _orders.GetByIdAsync(order.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Order {order.Id} disappeared mid-transition.");
    }

    private async Task<Guid?> FindOrderIdBySubmissionAsync(Guid tableId, Guid submissionId, CancellationToken cancellationToken)
    {
        await using var cmd = _dataSource.CreateCommand(
            """
            SELECT order_id
            FROM orders.orders
            WHERE table_id = @table_id AND source_reference_id = @submission_id
            LIMIT 1;
            """);
        cmd.Parameters.AddWithValue("table_id", tableId);
        cmd.Parameters.AddWithValue("submission_id", submissionId);
        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is Guid orderId ? orderId : null;
    }
}
