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
/// Delivery is at-least-once. Every operation here is idempotent by
/// construction: the reparent updates rows <c>WHERE table_id = @from</c>, so a
/// redelivery after the move finds nothing and changes nothing.
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
                await _orders.ReparentActiveOrdersToTableAsync(
                    e.ParticipantTableId, e.PrimaryTableId, e.OccurredAt, connection, transaction, cancellationToken)
                    .ConfigureAwait(false);
                break;
            }

            case IntegrationEventTypes.TableTransferred:
            {
                var e = IntegrationEventSerializer.Deserialize<TableTransferred>(payload.Span);
                await _orders.ReparentActiveOrdersToTableAsync(
                    e.SourceTableId, e.TargetTableId, e.OccurredAt, connection, transaction, cancellationToken)
                    .ConfigureAwait(false);
                break;
            }

            case IntegrationEventTypes.TableUnmerged:
            {
                var e = IntegrationEventSerializer.Deserialize<TableUnmerged>(payload.Span);
                if (e.OriginalOrderId is { } orderId)
                {
                    await _orders.ReparentOrderToTableAsync(
                        orderId, e.PrimaryTableId, e.MergedTableId, e.OccurredAt, connection, transaction, cancellationToken)
                        .ConfigureAwait(false);
                }

                break;
            }

            default:
                return;
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
