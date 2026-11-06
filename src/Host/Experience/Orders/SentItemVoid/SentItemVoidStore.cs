using System.Text.Json;
using ALKAROS.Audit.EventStore;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Orders.ItemExceptions;
using ALKAROS.Orders.OrderAggregate;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.Orders.SentItemVoid;

/// <summary>
/// V1-IAM-027: codes the `## Amendment` (Semih, 2026-09-04) to
/// docs/domain/void-complimentary-discount-policy.md (V0-DOM-006) — a
/// sent-but-unserved item may now be voided, gated by the bills.void grant.
/// Lives at the Host layer (not inside Orders' ItemExceptionHandler) so
/// Orders/Kitchen/Billing stay decoupled from each other (V0-ARC-001);
/// this is the composition root that already depends on all three.
///
/// Writes three aggregates (Order, KitchenTicket, Bill) and both audit records in ONE transaction: the item is
/// either voided everywhere (order, stock, kitchen ticket, bill line, audit) or nowhere, so a crash between steps
/// can no longer leave the kitchen cooking a voided item or the bill at full price.
///
/// V1-RMD-143 follow-up (Semih, 2026-09-09): the policy doc's own
/// definition of Waste (docs/domain/void-complimentary-discount-policy.md
/// line 16: a PREPARED item's cost leaving stock) assumed the kitchen had
/// actually started on the item by the time anyone would void it. Once
/// Accept started really consuming stock (V1-RMD-143), that assumption
/// stopped holding for the earliest of the three KitchenStates this store
/// accepts: `Sent` means the ticket reached the kitchen, not that anyone
/// touched an ingredient yet. Semih's revision (2026-09-09): a `Sent`
/// item's stock IS given back (nothing was physically used); `Preparing`/
/// `Ready` still convert to Waste with no stock returned, matching the
/// documented policy for a genuinely prepared item.
/// </summary>
public sealed class SentItemVoidStore
{
    private readonly IOrderRepository _orders;
    private readonly IBillRepository _bills;
    private readonly OrderItemVoidEffects _effects;
    private readonly NpgsqlDataSource _dataSource;
    private readonly IAuditEventStore _auditEvents;

    public SentItemVoidStore(
        IOrderRepository orders,
        IBillRepository bills,
        OrderItemVoidEffects effects,
        NpgsqlDataSource dataSource,
        IAuditEventStore auditEvents)
    {
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _bills = bills ?? throw new ArgumentNullException(nameof(bills));
        _effects = effects ?? throw new ArgumentNullException(nameof(effects));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _auditEvents = auditEvents ?? throw new ArgumentNullException(nameof(auditEvents));
    }

    /// <summary>Throws the same errors <see cref="VoidAsync"/> would for a request that cannot be applied, without changing anything.</summary>
    public async Task EnsureVoidableAsync(SentItemVoidCommand command, CancellationToken cancellationToken = default) =>
        _ = await LoadVoidableAsync(command, cancellationToken).ConfigureAwait(false);

    private async Task<(Order Order, OrderItem Item, (Bill Bill, BillItem BillItem)? BillContext)> LoadVoidableAsync(
        SentItemVoidCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!VoidReasonCatalog.IsValid(command.ReasonCode))
            throw new InvalidItemReasonException($"Reason '{command.ReasonCode}' is not a valid void catalog reason.");

        var order = await _orders.GetByIdAsync(command.OrderId, cancellationToken).ConfigureAwait(false)
            ?? throw new OrderItemNotFoundException(command.OrderId, command.OrderItemId);

        if (order.RowVersion != command.ExpectedRowVersion)
            throw new StaleOrderRowVersionException(order.Id, command.ExpectedRowVersion, order.RowVersion);

        var item = order.Items.FirstOrDefault(i => i.Id == command.OrderItemId)
            ?? throw new OrderItemNotFoundException(order.Id, command.OrderItemId);

        if (item.Status != OrderItemState.Active)
            throw new InvalidOperationException(
                $"Order item '{item.Id}' cannot be voided from status {item.Status}.");
        if (item.KitchenState == KitchenState.NotSent)
            throw new ItemNotYetSentException(item.Id);
        if (item.KitchenState is KitchenState.Served or KitchenState.Cancelled)
            throw new ItemAlreadyServedException(item.Id);

        // Check whether a billed line can even be converted to waste BEFORE
        // touching Order or Kitchen (found by an independent audit,
        // 2026-09-05): this used to run AFTER the Order was already saved
        // and the kitchen ticket item already cancelled — both irreversible
        // — so a Bill that happened to already be Paid/Allocated (a common,
        // not a rare state, for a bill by the time someone asks to void one
        // of its items) deterministically threw here, and the 409 the
        // caller got back ("bill cannot be changed in this state") read as
        // "nothing happened", while in fact the item was already void and
        // the kitchen would
        // never prepare it — the customer was left paying full price for an
        // item that was never coming. Failing fast here, before any write,
        // means a genuinely non-waivable Bill state stops the whole
        // operation with nothing yet mutated. A Bill changing state in the
        // narrow window between this check and the actual write below is a
        // real but far narrower race than the one this replaces — the
        // Bill's own row_version check in ApplyBillWasteConversionAsync
        // still catches that and surfaces it as the usual 409
        // CONCURRENCY_CONFLICT, not a silent inconsistency.
        var billContext = await FindBillLineForWasteAsync(
            command.OrderId, command.OrderItemId, cancellationToken).ConfigureAwait(false);
        return (order, item, billContext);
    }

    public async Task<SentItemVoidResult> VoidAsync(
        SentItemVoidCommand command, CancellationToken cancellationToken = default)
    {
        var (order, item, billContext) = await LoadVoidableAsync(command, cancellationToken).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        var historyReason = string.IsNullOrWhiteSpace(command.Notes)
            ? $"VoidSent:{command.ReasonCode}"
            : $"VoidSent:{command.ReasonCode} - {command.Notes}";

        var voidedOrder = order.CancelItem(command.OrderItemId, historyReason, command.ActorId, now);

        // The Order write and the stock restore of an item the kitchen had not started commit together
        // (OrderItemVoidEffects): a failed restore leaves the item un-voided instead of voided with its stock gone.
        long newOrderRowVersion;
        bool stockRestored;
        bool kitchenCancelled;
        bool wasteConverted;
        await using (var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false))
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
        {
            newOrderRowVersion = await _orders.SaveAsync(
                voidedOrder, command.ExpectedRowVersion, connection, transaction, cancellationToken).ConfigureAwait(false);
            var effects = await _effects.ApplyAsync(
                item,
                command.ActorId,
                $"VoidSent:{command.ReasonCode} — kitchen had not started (item {item.Id:D})",
                historyReason,
                now,
                connection,
                transaction,
                cancellationToken).ConfigureAwait(false);
            stockRestored = effects.StockRestored;
            kitchenCancelled = effects.KitchenTicketItemCancelled;
            wasteConverted = billContext is not null
                && await ApplyBillWasteConversionAsync(
                    billContext.Value.Bill, billContext.Value.BillItem, item, command.ReasonCode, connection, transaction, cancellationToken)
                    .ConfigureAwait(false);
            await AppendAuditAsync(connection, transaction, order.Id, item, command, stockRestored, cancellationToken).ConfigureAwait(false);
            await AppendItemEventAsync(connection, transaction, item, command, kitchenCancelled, wasteConverted, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return new SentItemVoidResult(
            order.Id, item.Id, newOrderRowVersion, voidedOrder.Total, kitchenCancelled, wasteConverted, stockRestored, now);
    }

    /// <summary>
    /// Locates the Bill/BillItem pair for this order item, if any is
    /// billed, and validates up front that it can actually be converted to
    /// waste — throws <see cref="BillNotModifiableForWasteException"/>
    /// immediately (before Order or Kitchen are touched) if a matching
    /// line exists on a Bill that is not Open/Reopened. Returns null when
    /// the item was never billed at all (nothing to convert).
    /// </summary>
    private async Task<(Bill Bill, BillItem BillItem)?> FindBillLineForWasteAsync(
        Guid orderId, Guid orderItemId, CancellationToken cancellationToken)
    {
        var bills = await _bills.GetByOrderIdAsync(orderId, cancellationToken).ConfigureAwait(false);
        foreach (var bill in bills)
        {
            var billItem = bill.Items.FirstOrDefault(i => i.OrderItemId == orderItemId);
            if (billItem is null)
                continue;

            if (bill.Status is not (BillState.Open or BillState.Reopened))
                throw new BillNotModifiableForWasteException(bill.Id, bill.Status.ToString());

            return (bill, billItem);
        }

        return null;
    }

    /// <summary>
    /// Removes the billed Sale line and replaces it with a zero-value
    /// BillLineType.Waste line — the customer owes nothing for it, but the
    /// waste is on record (III.7.2's Waste value, never produced before
    /// this task). Called only after <see cref="FindBillLineForWasteAsync"/>
    /// already confirmed the Bill was modifiable; the Bill's own
    /// row_version check here still catches a concurrent change in the
    /// meantime.
    /// </summary>
    private async Task<bool> ApplyBillWasteConversionAsync(
        Bill bill,
        BillItem billItem,
        OrderItem item,
        string reasonCode,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var wasteItem = new BillItem(
            Guid.NewGuid(),
            bill.Id,
            item.Id,
            item.ProductId,
            item.ProductNameSnapshot,
            item.Quantity,
            item.UnitPrice,
            item.TaxRate,
            discountAmount: 0m,
            netAmount: 0m,
            taxAmount: 0m,
            grossAmount: 0m,
            lineType: BillLineType.Waste,
            notes: $"Void:{reasonCode}");

        var updatedBill = bill.RemoveItem(billItem.Id).AddItem(wasteItem);
        await _bills.SaveAsync(updatedBill, bill.RowVersion, connection, transaction, cancellationToken).ConfigureAwait(false);
        return true;
    }

    // Written in the Order's own transaction: the item is not voided unless this record is kept. What the kitchen and
    // bill steps that follow achieved is recorded by the endpoint's order-item.void-sent event.
    private Task AppendAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid orderId,
        OrderItem item,
        SentItemVoidCommand command,
        bool stockRestored,
        CancellationToken cancellationToken)
        => _auditEvents.AppendAsync(
            new AuditEvent(
                id: Guid.NewGuid(),
                eventName: "Order.SentItemVoided",
                aggregateType: "Order",
                aggregateId: orderId,
                actorType: "User",
                correlationId: command.CorrelationId,
                actorId: command.ActorId,
                reason: command.ReasonCode,
                beforeStateJson: JsonSerializer.Serialize(new
                {
                    ItemId = item.Id,
                    Status = item.Status.ToString(),
                    KitchenState = item.KitchenState.ToString(),
                    Total = item.GrossAmount,
                }),
                afterStateJson: JsonSerializer.Serialize(new
                {
                    ItemId = item.Id,
                    Status = OrderItemState.Cancelled.ToString(),
                    StockRestored = stockRestored,
                })),
            connection, transaction, cancellationToken);

    // The per-item record of what the kitchen and bill steps achieved; it commits with them.
    private Task AppendItemEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        OrderItem item,
        SentItemVoidCommand command,
        bool kitchenTicketItemCancelled,
        bool billLineConvertedToWaste,
        CancellationToken cancellationToken)
        => _auditEvents.AppendAsync(
            new AuditEvent(
                id: Guid.NewGuid(),
                eventName: "order-item.void-sent",
                aggregateType: "OrderItem",
                aggregateId: item.Id,
                actorType: "User",
                correlationId: command.CorrelationId,
                actorId: command.ActorId,
                reason: command.ReasonCode,
                afterStateJson: JsonSerializer.Serialize(new
                {
                    kitchenTicketItemCancelled,
                    billLineConvertedToWaste,
                })),
            connection, transaction, cancellationToken);
}

/// <summary>
/// V1-RMD-424: the stock of an item the kitchen had not started could not be given back, so the void was not made.
/// </summary>
public sealed class VoidStockRestoreFailedException : Exception
{
    public VoidStockRestoreFailedException(Guid orderItemId, Exception innerException)
        : base($"Stock of order item '{orderItemId}' could not be restored; the item was not voided.", innerException)
    {
        OrderItemId = orderItemId;
    }

    public Guid OrderItemId { get; }
}
