using System.Data;
using System.Text.Json;
using ALKAROS.IntegrationContracts;
using ALKAROS.Messaging;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Tables.TableMerge;

/// <summary>
/// PostgreSQL implementation of <see cref="ITableMergeRepository"/> (V1-TBL-003, PDF:I.10, PDF:III.5.4).
/// Executes atomic, reversible multi-table merges and unmerges in a single database transaction.
/// </summary>
public sealed partial class PostgresTableMergeRepository : ITableMergeRepository
{
    private const string TableMergesTable = "table_mgmt.table_merges";
    private const string TablesTable = "table_mgmt.tables";
    // Read-only references for merge preconditions and participant discovery.
    // Order/Bill row moves are requested through a table integration event.
    private const string OrdersTable = "orders.orders";
    private const string BillsTable = "billing.bills";
    private const string BillAllocationsTable = "billing.bill_allocations";
    private const string AuditEventsTable = "audit.audit_events";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresTableMergeRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<TableMergeRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Merge ID cannot be empty.", nameof(id));

        const string sql = $"""
            SELECT table_merge_id, merge_group_id, primary_table_id, merged_table_id,
                   original_order_id, original_bill_id, status, reason, merged_by,
                   merged_at, unmerged_at, unmerged_by, unmerge_reason, row_version
            FROM {TableMergesTable}
            WHERE table_merge_id = @id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadRecord(reader);
    }

    public async Task<IReadOnlyList<TableMergeRecord>> GetByGroupIdAsync(
        Guid mergeGroupId,
        CancellationToken cancellationToken = default)
    {
        if (mergeGroupId == Guid.Empty)
            throw new ArgumentException("Merge group ID cannot be empty.", nameof(mergeGroupId));

        const string sql = $"""
            SELECT table_merge_id, merge_group_id, primary_table_id, merged_table_id,
                   original_order_id, original_bill_id, status, reason, merged_by,
                   merged_at, unmerged_at, unmerged_by, unmerge_reason, row_version
            FROM {TableMergesTable}
            WHERE merge_group_id = @group_id
            ORDER BY merged_at ASC;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("group_id", mergeGroupId);

        var list = new List<TableMergeRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(ReadRecord(reader));
        }

        return list;
    }

    public async Task<IReadOnlyList<TableMergeRecord>> GetActiveByPrimaryTableAsync(
        Guid primaryTableId,
        CancellationToken cancellationToken = default)
    {
        if (primaryTableId == Guid.Empty)
            throw new ArgumentException("Primary table ID cannot be empty.", nameof(primaryTableId));

        const string sql = $"""
            SELECT table_merge_id, merge_group_id, primary_table_id, merged_table_id,
                   original_order_id, original_bill_id, status, reason, merged_by,
                   merged_at, unmerged_at, unmerged_by, unmerge_reason, row_version
            FROM {TableMergesTable}
            WHERE primary_table_id = @primary_id AND status = 'Active'
            ORDER BY merged_at ASC;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("primary_id", primaryTableId);

        var list = new List<TableMergeRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(ReadRecord(reader));
        }

        return list;
    }

    public async Task<TableMergeRecord?> GetActiveByMergedTableAsync(
        Guid mergedTableId,
        CancellationToken cancellationToken = default)
    {
        if (mergedTableId == Guid.Empty)
            throw new ArgumentException("Merged table ID cannot be empty.", nameof(mergedTableId));

        const string sql = $"""
            SELECT table_merge_id, merge_group_id, primary_table_id, merged_table_id,
                   original_order_id, original_bill_id, status, reason, merged_by,
                   merged_at, unmerged_at, unmerged_by, unmerge_reason, row_version
            FROM {TableMergesTable}
            WHERE merged_table_id = @merged_id AND status = 'Active'
            LIMIT 1;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("merged_id", mergedTableId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadRecord(reader);
    }

    private static TableMergeRecord ReadRecord(NpgsqlDataReader reader)
    {
        return new TableMergeRecord(
            id: reader.GetGuid(0),
            mergeGroupId: reader.GetGuid(1),
            primaryTableId: reader.GetGuid(2),
            mergedTableId: reader.GetGuid(3),
            originalOrderId: reader.IsDBNull(4) ? null : reader.GetGuid(4),
            originalBillId: reader.IsDBNull(5) ? null : reader.GetGuid(5),
            status: Enum.Parse<TableMergeStatus>(reader.GetString(6)),
            reason: reader.GetString(7),
            mergedBy: reader.GetGuid(8),
            mergedAt: reader.GetFieldValue<DateTimeOffset>(9),
            unmergedAt: reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10),
            unmergedBy: reader.IsDBNull(11) ? null : reader.GetGuid(11),
            unmergeReason: reader.IsDBNull(12) ? null : reader.GetString(12),
            rowVersion: reader.GetInt64(13));
    }
}
