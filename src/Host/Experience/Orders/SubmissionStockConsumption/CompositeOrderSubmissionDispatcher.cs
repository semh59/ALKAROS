using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Orders.SubmitOrder;
using Npgsql;

namespace ALKAROS.Host.Experience.Orders.SubmissionStockConsumption;

/// <summary>
/// V1-RMD-144: <see cref="SubmitOrderHandler"/> takes a single
/// <see cref="IOrderSubmissionDispatcher"/>, and the kitchen ticket
/// dispatcher already occupies it. This runs several in the caller's own
/// submission transaction, in the given order.
///
/// Order matters: stock consumption is registered before the kitchen ticket
/// dispatcher, so an order that cannot be covered by stock throws before any
/// ticket row is written and the whole submission rolls back together — the
/// kitchen never sees a ticket for food the stock could not cover.
/// </summary>
public sealed class CompositeOrderSubmissionDispatcher : IOrderSubmissionDispatcher
{
    private readonly IReadOnlyList<IOrderSubmissionDispatcher> _dispatchers;

    public CompositeOrderSubmissionDispatcher(params IOrderSubmissionDispatcher[] dispatchers)
    {
        ArgumentNullException.ThrowIfNull(dispatchers);
        if (dispatchers.Length == 0)
            throw new ArgumentException("At least one dispatcher is required.", nameof(dispatchers));
        if (Array.Exists(dispatchers, d => d is null))
            throw new ArgumentException("Dispatchers cannot contain a null entry.", nameof(dispatchers));

        _dispatchers = dispatchers;
    }

    public async Task DispatchAsync(
        Order order,
        IReadOnlyList<OrderItem> firedItems,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        foreach (var dispatcher in _dispatchers)
        {
            await dispatcher.DispatchAsync(order, firedItems, connection, transaction, cancellationToken).ConfigureAwait(false);
        }
    }
}
