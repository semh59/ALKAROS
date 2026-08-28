namespace ALKAROS.Kitchen.TicketLifecycle;

using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Orders.SubmitOrder;
using Npgsql;

/// <summary>
/// Creates the station-scoped kitchen ticket for a submitted order in the
/// caller's transaction. An advisory transaction lock makes replay and
/// concurrent submissions deterministic even before a ticket row exists.
/// </summary>
public sealed class KitchenOrderSubmissionDispatcher : IOrderSubmissionDispatcher
{
    private readonly IKitchenTicketRepository _ticketRepository;
    private readonly string _stationId;

    public KitchenOrderSubmissionDispatcher(
        IKitchenTicketRepository ticketRepository,
        string stationId)
    {
        _ticketRepository = ticketRepository ?? throw new ArgumentNullException(nameof(ticketRepository));
        _stationId = stationId?.Trim() ?? string.Empty;
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

        if (string.IsNullOrWhiteSpace(_stationId))
        {
            throw new OrderSubmissionDispatchException(
                "Kitchen station is not configured; submitted order was not acknowledged.");
        }

        try
        {
            await using (var lockCommand = connection.CreateCommand())
            {
                lockCommand.Transaction = transaction;
                lockCommand.CommandText =
                    "SELECT pg_advisory_xact_lock(hashtextextended(@lock_key, 0));";
                lockCommand.Parameters.AddWithValue("lock_key", $"{order.Id:D}:{_stationId}");
                await lockCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var existingCommand = connection.CreateCommand())
            {
                existingCommand.Transaction = transaction;
                existingCommand.CommandText =
                    "SELECT 1 FROM kitchen.kitchen_tickets WHERE order_id = @order_id AND station_id = @station_id LIMIT 1;";
                existingCommand.Parameters.AddWithValue("order_id", order.Id);
                existingCommand.Parameters.AddWithValue("station_id", _stationId);
                if (await existingCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null)
                    return;
            }

            var ticket = KitchenTicket.CreateFromOrder(order, _stationId);
            await _ticketRepository.AddAsync(ticket, connection, transaction, cancellationToken).ConfigureAwait(false);
        }
        catch (OrderSubmissionDispatchException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidOperationException or NpgsqlException)
        {
            throw new OrderSubmissionDispatchException(
                $"Kitchen ticket dispatch failed for order '{order.Id}'.",
                exception);
        }
    }
}
