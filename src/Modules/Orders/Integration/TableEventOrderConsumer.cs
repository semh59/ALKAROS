using ALKAROS.IntegrationContracts;
using ALKAROS.Orders.OrderAggregate;
using Npgsql;

namespace ALKAROS.Orders.Integration;

/// <summary>
/// Order's reaction to Table Management merge / transfer / unmerge events
/// (V0-ARC-001 row 3: Table → Order via integration event). It moves Order's
/// own still-active rows to the new table and touches nothing outside
/// <c>orders.orders</c>.
/// </summary>
/// <remarks>
/// Delivery is at-least-once and not ordered. A move names the exact orders and
/// the table they must still be on, so a redelivery changes nothing; a merge
/// move is applied only while its merge is still active, so a late merge event
/// cannot undo an unmerge that already happened. Events from before the id
/// lists existed fall back to moving everything that sits on the source table.
/// </remarks>
public sealed class TableEventOrderConsumer : IIntegrationEventConsumer
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IOrderRepository _orders;

    public TableEventOrderConsumer(NpgsqlDataSource dataSource, IOrderRepository orders)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
    }

    public bool CanHandle(string eventType) => eventType is
        IntegrationEventTypes.TableMerged or
        IntegrationEventTypes.TableTransferred or
        IntegrationEventTypes.TableUnmerged;

    public async Task HandleAsync(string eventType, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        switch (eventType)
        {
            case IntegrationEventTypes.TableMerged:
            {
                var e = IntegrationEventSerializer.Deserialize<TableMerged>(payload.Span);
                if (e.OrderIds is null)
                {
                    await _orders.ReparentActiveOrdersToTableAsync(
                        e.ParticipantTableId, e.PrimaryTableId, e.OccurredAt, connection, transaction, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

                if (!await IsMergeActiveAsync(e.MergeGroupId, e.ParticipantTableId, connection, transaction, cancellationToken).ConfigureAwait(false))
                    break;

                foreach (var id in e.OrderIds)
                {
                    await _orders.ReparentOrderToTableAsync(
                        id, e.ParticipantTableId, e.PrimaryTableId, e.OccurredAt, connection, transaction, cancellationToken)
                        .ConfigureAwait(false);
                }

                break;
            }

            case IntegrationEventTypes.TableTransferred:
            {
                var e = IntegrationEventSerializer.Deserialize<TableTransferred>(payload.Span);
                if (e.OrderIds is null)
                {
                    await _orders.ReparentActiveOrdersToTableAsync(
                        e.SourceTableId, e.TargetTableId, e.OccurredAt, connection, transaction, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

                foreach (var id in e.OrderIds)
                {
                    await _orders.ReparentOrderToTableAsync(
                        id, e.SourceTableId, e.TargetTableId, e.OccurredAt, connection, transaction, cancellationToken)
                        .ConfigureAwait(false);
                }

                break;
            }

            case IntegrationEventTypes.TableUnmerged:
            {
                var e = IntegrationEventSerializer.Deserialize<TableUnmerged>(payload.Span);
                var orderIds = e.OrderIds ?? (e.OriginalOrderId is { } orderId ? [orderId] : []);
                foreach (var id in orderIds)
                {
                    await _orders.ReparentOrderToTableAsync(
                        id, e.PrimaryTableId, e.MergedTableId, e.OccurredAt, connection, transaction, cancellationToken)
                        .ConfigureAwait(false);
                }

                break;
            }

            default:
                return;
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> IsMergeActiveAsync(
        Guid mergeGroupId, Guid participantTableId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1 FROM table_mgmt.table_merges
                WHERE merge_group_id = @group_id AND merged_table_id = @table_id AND status = 'Active');
            """, connection, transaction);
        command.Parameters.AddWithValue("group_id", mergeGroupId);
        command.Parameters.AddWithValue("table_id", participantTableId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
