using ALKAROS.Billing.BillFoundation;
using ALKAROS.IntegrationContracts;
using Npgsql;

namespace ALKAROS.Billing.Integration;

/// <summary>
/// Bill's reaction to Table Management merge / transfer / unmerge events
/// (V0-ARC-001 row 3: Table → Bill via integration event). It moves Bill's own
/// still-active rows to the new table and touches nothing outside
/// <c>billing.bills</c>.
/// </summary>
/// <remarks>
/// Delivery is at-least-once. Every operation here is idempotent by
/// construction: the reparent updates rows <c>WHERE table_id = @from</c>, so a
/// redelivery after the move finds nothing and changes nothing.
/// </remarks>
public sealed class TableEventBillConsumer : IIntegrationEventConsumer
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IBillRepository _bills;

    public TableEventBillConsumer(NpgsqlDataSource dataSource, IBillRepository bills)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _bills = bills ?? throw new ArgumentNullException(nameof(bills));
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
                await _bills.ReparentActiveBillsToTableAsync(
                    e.ParticipantTableId, e.PrimaryTableId, e.OccurredAt, connection, transaction, cancellationToken)
                    .ConfigureAwait(false);
                break;
            }

            case IntegrationEventTypes.TableTransferred:
            {
                var e = IntegrationEventSerializer.Deserialize<TableTransferred>(payload.Span);
                await _bills.ReparentActiveBillsToTableAsync(
                    e.SourceTableId, e.TargetTableId, e.OccurredAt, connection, transaction, cancellationToken)
                    .ConfigureAwait(false);
                break;
            }

            case IntegrationEventTypes.TableUnmerged:
            {
                var e = IntegrationEventSerializer.Deserialize<TableUnmerged>(payload.Span);
                if (e.OriginalBillId is { } billId)
                {
                    await _bills.ReparentBillToTableAsync(
                        billId, e.PrimaryTableId, e.MergedTableId, e.OccurredAt, connection, transaction, cancellationToken)
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
