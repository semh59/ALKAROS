using System.Text.Json;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.MovementReversal;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Kitchen.TicketLifecycle;
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
/// Writes three aggregates (Order, KitchenTicket, Bill) across separate
/// short transactions, not one atomic transaction — the same accepted
/// tradeoff V1-KIT-005 documented: a crash between commits can leave the
/// kitchen ticket item or bill line one step behind the order, but every
/// consumer of this state is already at-least-once/out-of-order tolerant,
/// and the Order write (the money-bearing one) always happens first and
/// alone decides whether the item is voided.
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
    private readonly IKitchenTicketRepository _tickets;
    private readonly IBillRepository _bills;
    private readonly IStockMovementRepository _stockMovements;
    private readonly IStockMovementReversalService _stockReversal;
    private readonly NpgsqlDataSource _dataSource;

    public SentItemVoidStore(
        IOrderRepository orders,
        IKitchenTicketRepository tickets,
        IBillRepository bills,
        IStockMovementRepository stockMovements,
        IStockMovementReversalService stockReversal,
        NpgsqlDataSource dataSource)
    {
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _tickets = tickets ?? throw new ArgumentNullException(nameof(tickets));
        _bills = bills ?? throw new ArgumentNullException(nameof(bills));
        _stockMovements = stockMovements ?? throw new ArgumentNullException(nameof(stockMovements));
        _stockReversal = stockReversal ?? throw new ArgumentNullException(nameof(stockReversal));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<SentItemVoidResult> VoidAsync(
        SentItemVoidCommand command, CancellationToken cancellationToken = default)
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

        var now = DateTimeOffset.UtcNow;
        var historyReason = string.IsNullOrWhiteSpace(command.Notes)
            ? $"VoidSent:{command.ReasonCode}"
            : $"VoidSent:{command.ReasonCode} - {command.Notes}";

        var voidedOrder = order.CancelItem(command.OrderItemId, historyReason, command.ActorId, now);

        // V1-RMD-424 (V1-RMD-398 G-12): the Order write and the stock restore of an item the kitchen had not started
        // commit together. The restore used to be a best-effort step after the Order was saved, so a failed restore
        // left the item voided and its stock gone for good (the audit recorded StockRestored=false). Keyed off
        // `item.KitchenState` captured BEFORE CancelItem() above - this store's own precondition allows Held, Sent,
        // Preparing or Ready.
        //
        // V1-RMD-318 (independent 2026-09-26 audit, finding K6): a `Held` item (a later course of a multi-course
        // round - Order.FireRound's own doc comment) is just as much "nothing physically used yet" as `Sent` is (this
        // whole method's own class doc comment above) - it was activated and had its stock CONSUMED at the same
        // fire-round moment a Sent item was (OrderSubmissionStockDispatcher filters on IsActive, not KitchenState).
        var restoresStock = item.KitchenState is KitchenState.Sent or KitchenState.Held;
        long newOrderRowVersion;
        bool stockRestored;
        await using (var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false))
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
        {
            newOrderRowVersion = await _orders.SaveAsync(
                voidedOrder, command.ExpectedRowVersion, connection, transaction, cancellationToken).ConfigureAwait(false);
            stockRestored = restoresStock
                && await RestoreStockForVoidedItemAsync(item, command, connection, transaction, cancellationToken)
                    .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        var kitchenCancelled = await CancelKitchenTicketItemAsync(
            command.OrderId, command.OrderItemId, historyReason, now, cancellationToken).ConfigureAwait(false);
        var wasteConverted = billContext is not null
            ? await ApplyBillWasteConversionAsync(
                billContext.Value.Bill, billContext.Value.BillItem, item, command.ReasonCode, cancellationToken)
                .ConfigureAwait(false)
            : false;
        await AppendAuditAsync(
            order.Id, item, command, kitchenCancelled, wasteConverted, stockRestored, now, cancellationToken).ConfigureAwait(false);

        return new SentItemVoidResult(
            order.Id, item.Id, newOrderRowVersion, voidedOrder.Total, kitchenCancelled, wasteConverted, stockRestored, now);
    }

    /// <summary>
    /// Reverses every Consumption movement OrderStockConsumptionService
    /// recorded for this exact order item at Accept time (found via
    /// `sourceReferenceId = item.Id` — deliberately per-item, not
    /// per-order, so a sibling item's own consumption on the same order is
    /// never touched). Only reached when the item never left `Sent`
    /// (nothing was physically used yet); a movement already reversed by
    /// some other path (should not happen — a stock movement source item id
    /// is unique to one void attempt) is skipped rather than double-applied.
    /// Returns false (not an error) if there was nothing to reverse — e.g.
    /// the mapping existed at Accept time but was since removed and the
    /// ledger genuinely has no Consumption row, which the immutable ledger
    /// would still show even then, so this is a defensive fallback rather
    /// than an expected outcome.
    /// </summary>
    private async Task<bool> RestoreStockForVoidedItemAsync(
        OrderItem item,
        SentItemVoidCommand command,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var movements = await _stockMovements.GetBySourceAsync(StockMovementSourceType.Order, item.Id, cancellationToken)
            .ConfigureAwait(false);

        var restoredAny = false;
        foreach (var movement in movements)
        {
            if (movement.MovementType != StockMovementType.Consumption)
                continue;
            if (!await _stockReversal.CanReverseAsync(movement.Id, cancellationToken).ConfigureAwait(false))
                continue;

            try
            {
                await _stockReversal.ReverseMovementAsync(
                    new StockMovementReversalRequest(
                        movement.Id,
                        Reason: $"VoidSent:{command.ReasonCode} — kitchen had not started (item {item.Id:D})",
                        ActorId: command.ActorId),
                    connection,
                    transaction,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is StockMovementReversalException
                or DuplicateReversalException
                or StockMovementNotFoundException
                or InactiveStockItemException
                or InactiveStockLocationException
                or StockItemNotFoundException
                or StockLocationNotFoundException)
            {
                throw new VoidStockRestoreFailedException(item.Id, exception);
            }
            restoredAny = true;
        }

        return restoredAny;
    }

    /// <summary>
    /// Best-effort mirror cancellation on the matching kitchen ticket item, if
    /// one exists — the Order write above is the authority on whether the
    /// item is voided; a missing ticket item (should not happen once
    /// kitchen.live_sync_enabled is on, but is not this store's job to
    /// enforce) is not a reason to fail the void.
    /// </summary>
    private async Task<bool> CancelKitchenTicketItemAsync(
        Guid orderId, Guid orderItemId, string reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var tickets = await _tickets.GetByOrderIdAsync(orderId, cancellationToken).ConfigureAwait(false);
        foreach (var ticket in tickets)
        {
            var match = ticket.Items.FirstOrDefault(i => i.OrderItemId == orderItemId);
            if (match is null || !match.CanTransitionTo(KitchenTicketItemState.Cancelled))
                continue;

            var updated = ticket.UpdateItemStatus(match.Id, KitchenTicketItemState.Cancelled, reason, now);
            await _tickets.SaveAsync(updated, ticket.RowVersion, cancellationToken).ConfigureAwait(false);
            return true;
        }

        return false;
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
        Bill bill, BillItem billItem, OrderItem item, string reasonCode, CancellationToken cancellationToken)
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
        await _bills.SaveAsync(updatedBill, bill.RowVersion, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task AppendAuditAsync(
        Guid orderId,
        OrderItem item,
        SentItemVoidCommand command,
        bool kitchenTicketItemCancelled,
        bool billLineConvertedToWaste,
        bool stockRestored,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO audit.audit_events (
                id, event_name, aggregate_type, aggregate_id, actor_id, actor_type,
                reason, correlation_id, causation_id, before_state_json, after_state_json,
                metadata_json, occurred_at
            ) VALUES (
                @id, @event_name, @aggregate_type, @aggregate_id, @actor_id, @actor_type,
                @reason, @correlation_id, @causation_id, @before_state_json, @after_state_json,
                @metadata_json, @occurred_at
            );
            """;
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("event_name", "Order.SentItemVoided");
        cmd.Parameters.AddWithValue("aggregate_type", "Order");
        cmd.Parameters.AddWithValue("aggregate_id", orderId);
        cmd.Parameters.AddWithValue("actor_id", command.ActorId);
        cmd.Parameters.AddWithValue("actor_type", "User");
        cmd.Parameters.AddWithValue("reason", command.ReasonCode);
        cmd.Parameters.AddWithValue("correlation_id", command.CorrelationId);
        cmd.Parameters.AddWithValue("causation_id", DBNull.Value);

        var before = new
        {
            ItemId = item.Id,
            Status = item.Status.ToString(),
            KitchenState = item.KitchenState.ToString(),
            Total = item.GrossAmount,
        };
        var after = new
        {
            ItemId = item.Id,
            Status = OrderItemState.Cancelled.ToString(),
            KitchenState = KitchenState.Cancelled.ToString(),
            KitchenTicketItemCancelled = kitchenTicketItemCancelled,
            BillLineConvertedToWaste = billLineConvertedToWaste,
            StockRestored = stockRestored,
        };

        var pBefore = cmd.Parameters.AddWithValue("before_state_json", JsonSerializer.Serialize(before));
        pBefore.NpgsqlDbType = NpgsqlDbType.Jsonb;
        var pAfter = cmd.Parameters.AddWithValue("after_state_json", JsonSerializer.Serialize(after));
        pAfter.NpgsqlDbType = NpgsqlDbType.Jsonb;

        cmd.Parameters.AddWithValue("metadata_json", DBNull.Value);
        cmd.Parameters.AddWithValue("occurred_at", now);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
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
