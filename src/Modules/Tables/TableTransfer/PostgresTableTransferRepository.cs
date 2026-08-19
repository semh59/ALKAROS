using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Tables.TableTransfer;

/// <summary>
/// PostgreSQL implementation of <see cref="ITableTransferRepository"/> (V1-TBL-002, PDF:I.9, PDF:III.5.3).
/// Executes atomic table transfers in a single database transaction with optimistic concurrency,
/// payment-policy verification, and immutable provenance logging.
/// </summary>
public sealed class PostgresTableTransferRepository : ITableTransferRepository
{
    private const string TableTransfersTable = "table_mgmt.table_transfers";
    private const string TablesTable = "table_mgmt.tables";
    private const string OrdersTable = "orders.orders";
    private const string BillsTable = "billing.bills";
    private const string BillAllocationsTable = "billing.bill_allocations";
    private const string AuditEventsTable = "audit.audit_events";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresTableTransferRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<TableTransferRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Transfer ID cannot be empty.", nameof(id));

        const string sql = $"""
            SELECT table_transfer_id, source_table_id, target_table_id, order_id, bill_id,
                   reason, transferred_by, transferred_at
            FROM {TableTransfersTable}
            WHERE table_transfer_id = @id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadRecord(reader);
    }

    public async Task<IReadOnlyList<TableTransferRecord>> GetBySourceTableAsync(
        Guid sourceTableId,
        CancellationToken cancellationToken = default)
    {
        if (sourceTableId == Guid.Empty)
            throw new ArgumentException("Source table ID cannot be empty.", nameof(sourceTableId));

        const string sql = $"""
            SELECT table_transfer_id, source_table_id, target_table_id, order_id, bill_id,
                   reason, transferred_by, transferred_at
            FROM {TableTransfersTable}
            WHERE source_table_id = @source_table_id
            ORDER BY transferred_at DESC;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("source_table_id", sourceTableId);

        var list = new List<TableTransferRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(ReadRecord(reader));
        }

        return list;
    }

    public async Task<IReadOnlyList<TableTransferRecord>> GetByTargetTableAsync(
        Guid targetTableId,
        CancellationToken cancellationToken = default)
    {
        if (targetTableId == Guid.Empty)
            throw new ArgumentException("Target table ID cannot be empty.", nameof(targetTableId));

        const string sql = $"""
            SELECT table_transfer_id, source_table_id, target_table_id, order_id, bill_id,
                   reason, transferred_by, transferred_at
            FROM {TableTransfersTable}
            WHERE target_table_id = @target_table_id
            ORDER BY transferred_at DESC;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("target_table_id", targetTableId);

        var list = new List<TableTransferRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(ReadRecord(reader));
        }

        return list;
    }

    public async Task<TableTransferResult> ExecuteTransferAsync(
        TableTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var now = request.TransferredAt ?? DateTimeOffset.UtcNow;
        var transferId = Guid.NewGuid();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        // 1. Lock and validate Source Table
        string sourceStatus;
        Guid? sourceCurrentOrderId;
        Guid? sourceCurrentBillId;
        long sourceRowVersion;

        const string selectSourceSql = $"""
            SELECT table_id, table_number, active, current_status, current_order_id, current_bill_id, row_version
            FROM {TablesTable}
            WHERE table_id = @source_id
            FOR UPDATE;
            """;

        await using (var cmd = new NpgsqlCommand(selectSourceSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("source_id", request.SourceTableId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new TableNotFoundException(request.SourceTableId, $"Source table '{request.SourceTableId}' was not found.");
            }

            sourceStatus = reader.GetString(3);
            sourceCurrentOrderId = reader.IsDBNull(4) ? null : reader.GetGuid(4);
            sourceCurrentBillId = reader.IsDBNull(5) ? null : reader.GetGuid(5);
            sourceRowVersion = reader.GetInt64(6);
        }

        if (!string.Equals(sourceStatus, "Occupied", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidSourceTableStateException(request.SourceTableId, sourceStatus);
        }

        if (sourceRowVersion != request.ExpectedSourceRowVersion)
        {
            throw new TableTransferConcurrencyException(request.SourceTableId, request.ExpectedSourceRowVersion, sourceRowVersion);
        }

        // 2. Lock and validate Target Table
        bool targetActive;
        string targetStatus;
        long targetRowVersion;

        const string selectTargetSql = $"""
            SELECT table_id, table_number, active, current_status, current_order_id, current_bill_id, row_version
            FROM {TablesTable}
            WHERE table_id = @target_id
            FOR UPDATE;
            """;

        await using (var cmd = new NpgsqlCommand(selectTargetSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("target_id", request.TargetTableId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new TableNotFoundException(request.TargetTableId, $"Target table '{request.TargetTableId}' was not found.");
            }

            targetActive = reader.GetBoolean(2);
            targetStatus = reader.GetString(3);
            targetRowVersion = reader.GetInt64(6);
        }

        if (!targetActive)
        {
            throw new InvalidTargetTableStateException(request.TargetTableId, "Inactive");
        }

        if (!string.Equals(targetStatus, "Available", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidTargetTableStateException(request.TargetTableId, targetStatus);
        }

        if (targetRowVersion != request.ExpectedTargetRowVersion)
        {
            throw new TableTransferConcurrencyException(request.TargetTableId, request.ExpectedTargetRowVersion, targetRowVersion);
        }

        // 3. Payment-policy validation: verify no payment data on bills for source table
        var activeBillIds = new List<Guid>();
        const string selectBillsSql = $"""
            SELECT bill_id, status, payable_amount, allocated_amount, paid_amount
            FROM {BillsTable}
            WHERE table_id = @source_id AND status NOT IN ('Paid', 'Cancelled');
            """;

        await using (var cmd = new NpgsqlCommand(selectBillsSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("source_id", request.SourceTableId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var billId = reader.GetGuid(0);
                var status = reader.GetString(1);
                var allocated = reader.GetDecimal(3);
                var paid = reader.GetDecimal(4);

                if (!string.Equals(status, "Open", StringComparison.OrdinalIgnoreCase))
                {
                    throw new PaymentPolicyRequiredException(
                        billId,
                        $"Bill '{billId}' is in '{status}' state. Table transfer for non-open bills requires V1.2 payment-aware topology policy.");
                }

                if (allocated > 0 || paid > 0)
                {
                    throw new PaymentPolicyRequiredException(
                        billId,
                        $"Bill '{billId}' has payment progress (allocated: {allocated}, paid: {paid}). Table transfer with payment data requires V1.2 payment-aware topology policy.");
                }

                activeBillIds.Add(billId);
            }
        }

        // Also check if any bill allocations exist for source table bills
        const string selectAllocationsSql = $"""
            SELECT ba.bill_id
            FROM {BillAllocationsTable} ba
            JOIN {BillsTable} b ON ba.bill_id = b.bill_id
            WHERE b.table_id = @source_id AND b.status NOT IN ('Paid', 'Cancelled')
            LIMIT 1;
            """;

        await using (var cmd = new NpgsqlCommand(selectAllocationsSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("source_id", request.SourceTableId);
            var allocBillId = await cmd.ExecuteScalarAsync(cancellationToken);
            if (allocBillId is not null and not DBNull)
            {
                var billId = (Guid)allocBillId;
                throw new PaymentPolicyRequiredException(
                    billId,
                    $"Bill '{billId}' has split/payment allocations. Table transfer requires V1.2 payment policy.");
            }
        }

        // 4. Find open orders on Source Table
        var activeOrderIds = new List<Guid>();
        const string selectOrdersSql = $"""
            SELECT order_id
            FROM {OrdersTable}
            WHERE table_id = @source_id AND status NOT IN ('Completed', 'Cancelled')
            ORDER BY created_at ASC;
            """;

        await using (var cmd = new NpgsqlCommand(selectOrdersSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("source_id", request.SourceTableId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                activeOrderIds.Add(reader.GetGuid(0));
            }
        }

        // 5. Reparent Orders from Source Table to Target Table
        if (activeOrderIds.Count > 0)
        {
            const string updateOrdersSql = $"""
                UPDATE {OrdersTable}
                SET table_id = @target_id,
                    updated_at = @now,
                    row_version = row_version + 1
                WHERE table_id = @source_id AND status NOT IN ('Completed', 'Cancelled');
                """;

            await using var cmd = new NpgsqlCommand(updateOrdersSql, connection, transaction);
            cmd.Parameters.AddWithValue("target_id", request.TargetTableId);
            cmd.Parameters.AddWithValue("now", now);
            cmd.Parameters.AddWithValue("source_id", request.SourceTableId);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // 6. Reparent Bills from Source Table to Target Table
        if (activeBillIds.Count > 0)
        {
            const string updateBillsSql = $"""
                UPDATE {BillsTable}
                SET table_id = @target_id,
                    updated_at = @now,
                    row_version = row_version + 1
                WHERE table_id = @source_id AND status NOT IN ('Paid', 'Cancelled');
                """;

            await using var cmd = new NpgsqlCommand(updateBillsSql, connection, transaction);
            cmd.Parameters.AddWithValue("target_id", request.TargetTableId);
            cmd.Parameters.AddWithValue("now", now);
            cmd.Parameters.AddWithValue("source_id", request.SourceTableId);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // 7. Determine Target Table Primary Order & Bill Pointers
        var primaryOrderId = sourceCurrentOrderId ?? (activeOrderIds.Count > 0 ? activeOrderIds[0] : (Guid?)null);
        var primaryBillId = sourceCurrentBillId ?? (activeBillIds.Count > 0 ? activeBillIds[0] : (Guid?)null);

        // 8. Update Source Table (Occupied -> Available, soft-pointers cleared)
        long newSourceRowVersion;
        const string updateSourceSql = $"""
            UPDATE {TablesTable}
            SET current_status = 'Available',
                current_order_id = NULL,
                current_bill_id = NULL,
                row_version = row_version + 1
            WHERE table_id = @source_id AND row_version = @expected_source_row_version
            RETURNING row_version;
            """;

        await using (var cmd = new NpgsqlCommand(updateSourceSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("source_id", request.SourceTableId);
            cmd.Parameters.AddWithValue("expected_source_row_version", request.ExpectedSourceRowVersion);
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            if (result is null or DBNull)
            {
                throw new TableTransferConcurrencyException(request.SourceTableId, request.ExpectedSourceRowVersion, sourceRowVersion);
            }
            newSourceRowVersion = (long)result;
        }

        // 9. Update Target Table (Available -> Occupied, soft-pointers set)
        long newTargetRowVersion;
        const string updateTargetSql = $"""
            UPDATE {TablesTable}
            SET current_status = 'Occupied',
                current_order_id = @current_order_id,
                current_bill_id = @current_bill_id,
                row_version = row_version + 1
            WHERE table_id = @target_id AND row_version = @expected_target_row_version
            RETURNING row_version;
            """;

        await using (var cmd = new NpgsqlCommand(updateTargetSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("target_id", request.TargetTableId);
            cmd.Parameters.AddWithValue("current_order_id", (object?)primaryOrderId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("current_bill_id", (object?)primaryBillId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("expected_target_row_version", request.ExpectedTargetRowVersion);
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            if (result is null or DBNull)
            {
                throw new TableTransferConcurrencyException(request.TargetTableId, request.ExpectedTargetRowVersion, targetRowVersion);
            }
            newTargetRowVersion = (long)result;
        }

        // 10. Insert Transfer Provenance Record into table_mgmt.table_transfers
        const string insertTransferSql = $"""
            INSERT INTO {TableTransfersTable} (
                table_transfer_id, source_table_id, target_table_id, order_id, bill_id,
                reason, transferred_by, transferred_at
            ) VALUES (
                @transfer_id, @source_id, @target_id, @order_id, @bill_id,
                @reason, @transferred_by, @transferred_at
            );
            """;

        await using (var cmd = new NpgsqlCommand(insertTransferSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("transfer_id", transferId);
            cmd.Parameters.AddWithValue("source_id", request.SourceTableId);
            cmd.Parameters.AddWithValue("target_id", request.TargetTableId);
            cmd.Parameters.AddWithValue("order_id", (object?)primaryOrderId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("bill_id", (object?)primaryBillId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("reason", request.Reason);
            cmd.Parameters.AddWithValue("transferred_by", request.TransferredBy);
            cmd.Parameters.AddWithValue("transferred_at", now);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // 10. Persist Audit Event in the SAME transaction (AUD-01: Fail-Closed)
        const string insertAuditSql = $"""
            INSERT INTO {AuditEventsTable} (
                id, event_name, aggregate_type, aggregate_id, actor_id, actor_type,
                reason, correlation_id, causation_id, before_state_json, after_state_json,
                metadata_json, occurred_at
            ) VALUES (
                @id, @event_name, @aggregate_type, @aggregate_id, @actor_id, @actor_type,
                @reason, @correlation_id, @causation_id, @before_state_json, @after_state_json,
                @metadata_json, @occurred_at
            );
            """;

        var beforeState = new
        {
            SourceTableId = request.SourceTableId,
            SourceStatus = sourceStatus,
            SourceRowVersion = sourceRowVersion,
            TargetTableId = request.TargetTableId,
            TargetStatus = targetStatus,
            TargetRowVersion = targetRowVersion,
            OrderIds = activeOrderIds,
            BillIds = activeBillIds
        };

        var afterState = new
        {
            SourceTableId = request.SourceTableId,
            SourceStatus = "Available",
            SourceRowVersion = newSourceRowVersion,
            TargetTableId = request.TargetTableId,
            TargetStatus = "Occupied",
            TargetRowVersion = newTargetRowVersion,
            PrimaryOrderId = primaryOrderId,
            PrimaryBillId = primaryBillId,
            TransferredOrderIds = activeOrderIds,
            TransferredBillIds = activeBillIds
        };

        var metadata = new
        {
            TransferId = transferId,
            Reason = request.Reason,
            TransferredBy = request.TransferredBy
        };

        await using (var auditCmd = new NpgsqlCommand(insertAuditSql, connection, transaction))
        {
            auditCmd.Parameters.AddWithValue("id", Guid.NewGuid());
            auditCmd.Parameters.AddWithValue("event_name", "Table.Transferred");
            auditCmd.Parameters.AddWithValue("aggregate_type", "Table");
            auditCmd.Parameters.AddWithValue("aggregate_id", request.SourceTableId);
            auditCmd.Parameters.AddWithValue("actor_id", request.TransferredBy);
            auditCmd.Parameters.AddWithValue("actor_type", "User");
            auditCmd.Parameters.AddWithValue("reason", request.Reason);
            auditCmd.Parameters.AddWithValue("correlation_id", transferId.ToString("N"));
            auditCmd.Parameters.AddWithValue("causation_id", DBNull.Value);

            var pBefore = auditCmd.Parameters.AddWithValue("before_state_json", JsonSerializer.Serialize(beforeState));
            pBefore.NpgsqlDbType = NpgsqlDbType.Jsonb;

            var pAfter = auditCmd.Parameters.AddWithValue("after_state_json", JsonSerializer.Serialize(afterState));
            pAfter.NpgsqlDbType = NpgsqlDbType.Jsonb;

            var pMeta = auditCmd.Parameters.AddWithValue("metadata_json", JsonSerializer.Serialize(metadata));
            pMeta.NpgsqlDbType = NpgsqlDbType.Jsonb;

            auditCmd.Parameters.AddWithValue("occurred_at", now);

            await auditCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new TableTransferResult(
            transferId,
            request.SourceTableId,
            newSourceRowVersion,
            request.TargetTableId,
            newTargetRowVersion,
            activeOrderIds,
            activeBillIds,
            now);
    }

    private static TableTransferRecord ReadRecord(NpgsqlDataReader reader)
    {
        return new TableTransferRecord(
            id: reader.GetGuid(0),
            sourceTableId: reader.GetGuid(1),
            targetTableId: reader.GetGuid(2),
            orderId: reader.IsDBNull(3) ? null : reader.GetGuid(3),
            billId: reader.IsDBNull(4) ? null : reader.GetGuid(4),
            reason: reader.GetString(5),
            transferredBy: reader.GetGuid(6),
            transferredAt: reader.GetFieldValue<DateTimeOffset>(7));
    }
}
