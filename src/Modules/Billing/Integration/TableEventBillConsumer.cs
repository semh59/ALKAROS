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
/// Delivery is at-least-once and not ordered. A move names the exact bills and
/// the table they must still be on, so a redelivery changes nothing; a merge
/// move is applied only while its merge is still active, so a late merge event
/// cannot undo an unmerge that already happened. Events from before the id
/// lists existed fall back to moving everything that sits on the source table.
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
                if (e.BillIds is null)
                {
                    await _bills.ReparentActiveBillsToTableAsync(
                        e.ParticipantTableId, e.PrimaryTableId, e.OccurredAt, connection, transaction, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

                if (!await IsMergeActiveAsync(e.MergeGroupId, e.ParticipantTableId, connection, transaction, cancellationToken).ConfigureAwait(false))
                    break;

                foreach (var id in e.BillIds)
                {
                    await _bills.ReparentBillToTableAsync(
                        id, e.ParticipantTableId, e.PrimaryTableId, e.OccurredAt, connection, transaction, cancellationToken)
                        .ConfigureAwait(false);
                }

                break;
            }

            case IntegrationEventTypes.TableTransferred:
            {
                var e = IntegrationEventSerializer.Deserialize<TableTransferred>(payload.Span);
                if (e.BillIds is null)
                {
                    await _bills.ReparentActiveBillsToTableAsync(
                        e.SourceTableId, e.TargetTableId, e.OccurredAt, connection, transaction, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

                foreach (var id in e.BillIds)
                {
                    await _bills.ReparentBillToTableAsync(
                        id, e.SourceTableId, e.TargetTableId, e.OccurredAt, connection, transaction, cancellationToken)
                        .ConfigureAwait(false);
                }

                break;
            }

            case IntegrationEventTypes.TableUnmerged:
            {
                var e = IntegrationEventSerializer.Deserialize<TableUnmerged>(payload.Span);
                var billIds = e.BillIds ?? (e.OriginalBillId is { } billId ? [billId] : []);
                foreach (var id in billIds)
                {
                    await _bills.ReparentBillToTableAsync(
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
