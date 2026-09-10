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
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        if (order.Source is not (OrderSource.Cashier or OrderSource.Waiter))
            return;

        // Cancelled lines never reach the kitchen, so they must not consume
        // either — the same reasoning the Accept path documents. Every
        // remaining line is consuming for the first time here: an order can
        // only be submitted out of Draft, a Draft order's lines are all still
        // Draft, and a table whose order already left Draft starts a new order
        // rather than re-submitting the old one
        // (OrderManagementStore.GetActiveOrderByTableIdInternalAsync only ever
        // matches status = 'Draft').
        var pending = order.Items.Where(item => item.IsActive).ToList();
        if (pending.Count == 0)
            return;

        await _consumption.ConsumeItemsAsync(
            order, pending, "submitted", order.ServingUserId ?? Guid.Empty, connection, transaction, cancellationToken)
            .ConfigureAwait(false);
    }
}
