namespace ALKAROS.Orders.SubmitOrder;

using ALKAROS.Orders.OrderAggregate;
using Npgsql;

/// <summary>
/// Projects a submitted order into an operational downstream workflow while
/// remaining inside the order submission transaction.
/// </summary>
public interface IOrderSubmissionDispatcher
{
    /// <summary>
    /// <paramref name="firedItems"/> is the round that was just activated by
    /// this submission — not every active line on the order.
    /// </summary>
    /// <remarks>
    /// V1-ORD-006: this used to be derived inside each implementation as
    /// <c>order.Items.Where(item =&gt; item.IsActive)</c>, which was only ever
    /// correct because a table whose order had left Draft started a *new*
    /// order instead of adding to the open one — <c>OrderSubmissionStock
    /// Dispatcher</c> said so in its own comment. That behaviour is what made
    /// a table's second round invisible to the bill (a party ordering
    /// starters then mains was billed for the mains only), so it had to go;
    /// and the moment a check accepts a second round, deriving the list again
    /// would fire round one to the kitchen twice and consume its stock twice.
    /// Passing the delta explicitly removes the derivation, and with it the
    /// invariant nobody could see was load-bearing.
    /// </remarks>
    Task DispatchAsync(
        Order order,
        IReadOnlyList<OrderItem> firedItems,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);
}
