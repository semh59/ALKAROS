using ALKAROS.Host.Experience.Orders.SentItemVoid;
using ALKAROS.Inventory.CrossChannelReservation;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.MovementReversal;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.Orders.OrderAggregate;
using Npgsql;

namespace ALKAROS.Host.Experience.Orders;

/// <summary>
/// What taking back one sent order line does outside the order itself, inside the caller's transaction: stock and
/// daily menu portions of a line the kitchen had not started go back, a line it had started is waste, and the
/// matching kitchen ticket line is cancelled. Shared by the single-line void and the whole-order cancellation.
/// </summary>
public sealed class OrderItemVoidEffects(
    IStockMovementRepository stockMovements,
    IStockMovementReversalService stockReversal,
    IReservationAwareConsumptionGuard portions,
    IKitchenTicketRepository tickets)
{
    /// <param name="item">The line as it was before it was cancelled.</param>
    /// <param name="stockReason">Why the consumption movements are reversed.</param>
    /// <param name="ticketReason">Why the kitchen ticket line is cancelled.</param>
    public async Task<ItemVoidEffectResult> ApplyAsync(
        OrderItem item,
        Guid actorId,
        string stockReason,
        string ticketReason,
        DateTimeOffset now,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var kitchenStarted = item.KitchenState is KitchenState.Preparing or KitchenState.Ready;
        var stockRestored = !kitchenStarted
            && await RestoreStockAsync(item, actorId, stockReason, connection, transaction, cancellationToken).ConfigureAwait(false);
        if (stockRestored)
            await portions.ReturnPortionsAsync(item.Id, item.Quantity, item.Quantity, connection, transaction, cancellationToken)
                .ConfigureAwait(false);
        else if (kitchenStarted)
            await portions.WastePortionsAsync(item.Id, connection, transaction, cancellationToken).ConfigureAwait(false);

        var kitchenCancelled = await CancelKitchenTicketItemAsync(
            item.OrderId, item.Id, ticketReason, now, connection, transaction, cancellationToken).ConfigureAwait(false);
        return new ItemVoidEffectResult(stockRestored, kitchenCancelled);
    }

    /// <summary>
    /// Reverses every Consumption movement recorded for exactly this order line (per line, so a sibling line's own
    /// consumption is never touched). A movement already reversed elsewhere is skipped; a line that never consumed
    /// anything reports false.
    /// </summary>
    private async Task<bool> RestoreStockAsync(
        OrderItem item,
        Guid actorId,
        string reason,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var movements = await stockMovements.GetBySourceAsync(StockMovementSourceType.Order, item.Id, cancellationToken)
            .ConfigureAwait(false);

        var restoredAny = false;
        foreach (var movement in movements)
        {
            if (movement.MovementType != StockMovementType.Consumption)
                continue;
            if (!await stockReversal.CanReverseAsync(movement.Id, cancellationToken).ConfigureAwait(false))
                continue;

            try
            {
                await stockReversal.ReverseMovementAsync(
                    new StockMovementReversalRequest(movement.Id, Reason: reason, ActorId: actorId),
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

    /// <summary>A missing ticket line (live kitchen sync may be off) is not a reason to fail.</summary>
    private async Task<bool> CancelKitchenTicketItemAsync(
        Guid orderId,
        Guid orderItemId,
        string reason,
        DateTimeOffset now,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        foreach (var ticket in await tickets.GetByOrderIdAsync(orderId, connection, transaction, cancellationToken).ConfigureAwait(false))
        {
            var match = ticket.Items.FirstOrDefault(i => i.OrderItemId == orderItemId);
            if (match is null || !match.CanTransitionTo(KitchenTicketItemState.Cancelled))
                continue;

            var updated = ticket.UpdateItemStatus(match.Id, KitchenTicketItemState.Cancelled, reason, now);
            await tickets.SaveAsync(updated, ticket.RowVersion, connection, transaction, cancellationToken).ConfigureAwait(false);
            return true;
        }

        return false;
    }
}

public readonly record struct ItemVoidEffectResult(bool StockRestored, bool KitchenTicketItemCancelled);
