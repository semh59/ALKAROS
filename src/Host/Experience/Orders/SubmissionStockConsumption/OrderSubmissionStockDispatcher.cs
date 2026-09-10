using ALKAROS.Host.Experience.Orders.OrderStockConsumption;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Orders.SubmitOrder;
using Npgsql;

namespace ALKAROS.Host.Experience.Orders.SubmissionStockConsumption;

/// <summary>
/// V1-RMD-144: consumes stock at the moment a staff-entered order is sent to
/// the kitchen. Semih's decision (2026-09-10): an order the staff take
/// themselves consumes on submit, a QR order consumes after staff
/// confirmation, and an NFC order consumes straight away.
///
/// V1-RMD-143 attached consumption to <see cref="OrderState.Accepted"/>, but
/// only QR and NFC orders ever reach that state — <see cref="OrderState"/>
/// only allows Accepted from PendingConfirmation, and only
/// <c>QrOrderSubmittedConsumer</c> and <c>NfcOrderingStore</c> enter it. A
/// Cashier/Waiter order stops at Submitted, so its stock never moved at all.
///
/// All four channels share one <see cref="SubmitOrderHandler"/>, so the
/// source check below is what keeps QR and NFC from consuming twice (once
/// here and again on Accept). It is the whole channel split, in one place.
/// </summary>
public sealed class OrderSubmissionStockDispatcher : IOrderSubmissionDispatcher
{
    private readonly OrderStockConsumptionService _consumption;

    public OrderSubmissionStockDispatcher(OrderStockConsumptionService consumption)
    {
        _consumption = consumption ?? throw new ArgumentNullException(nameof(consumption));
    }

    public async Task DispatchAsync(
        Order order,
        IReadOnlyList<OrderItem> firedItems,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(firedItems);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        if (order.Source is not (OrderSource.Cashier or OrderSource.Waiter))
            return;

        // V1-ORD-006: the round FireRound just activated, handed over rather
        // than derived. It used to read `order.Items.Where(IsActive)`, which
        // was only correct because a table whose order had left Draft started
        // a *new* order instead of adding to the open one. Now that a check
        // takes a second round, that derivation would consume round one's
        // stock again on every later round.
        //
        // Cancelled lines never reach the kitchen and so must not consume
        // either — the same reasoning the Accept path documents; a line that
        // was just fired is Active by construction, so this filter is a
        // belt-and-braces guard rather than a load-bearing one.
        var pending = firedItems.Where(item => item.IsActive).ToList();
        if (pending.Count == 0)
            return;

        await _consumption.ConsumeItemsAsync(
            order, pending, "submitted", order.ServingUserId ?? Guid.Empty, connection, transaction, cancellationToken)
            .ConfigureAwait(false);
    }
}
