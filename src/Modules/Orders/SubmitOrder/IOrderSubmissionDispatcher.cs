namespace ALKAROS.Orders.SubmitOrder;

using ALKAROS.Orders.OrderAggregate;
using Npgsql;

/// <summary>
/// Projects a submitted order into an operational downstream workflow while
/// remaining inside the order submission transaction.
/// </summary>
public interface IOrderSubmissionDispatcher
{
    Task DispatchAsync(
        Order order,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);
}
