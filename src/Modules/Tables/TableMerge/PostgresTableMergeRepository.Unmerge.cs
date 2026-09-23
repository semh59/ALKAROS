using System.Data;
using System.Text.Json;
using ALKAROS.IntegrationContracts;
using ALKAROS.Messaging;
using ALKAROS.Tables.PaymentTopology;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Tables.TableMerge;

public sealed partial class PostgresTableMergeRepository
{
    public async Task<TableUnmergeResult> ExecuteUnmergeAsync(
        TableUnmergeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var now = request.UnmergedAt ?? DateTimeOffset.UtcNow;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        // 1. Fetch and Lock Active Merge Records for this Merge Group
        const string selectMergesSql = $"""
            SELECT table_merge_id, merge_group_id, primary_table_id, merged_table_id,
                   original_order_id, original_bill_id, status, reason, merged_by,
                   merged_at, unmerged_at, unmerged_by, unmerge_reason, row_version
            FROM {TableMergesTable}
            WHERE merge_group_id = @group_id AND status = 'Active'
            FOR UPDATE;
            """;

        var mergeRecords = new List<TableMergeRecord>();
        await using (var cmd = new NpgsqlCommand(selectMergesSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("group_id", request.MergeGroupId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                mergeRecords.Add(ReadRecord(reader));
            }
        }

        if (mergeRecords.Count == 0)
        {
            throw new MergeRecordNotFoundException(request.MergeGroupId, $"No active merge records found for merge group '{request.MergeGroupId}'.");
        }

        var primaryTableId = mergeRecords[0].PrimaryTableId;

        // 2. Lock and validate Primary Table
        long primaryRowVersion;
        const string selectPrimarySql = $"""
            SELECT table_id, row_version
            FROM {TablesTable}
            WHERE table_id = @primary_id
            FOR UPDATE;
            """;

        await using (var cmd = new NpgsqlCommand(selectPrimarySql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("primary_id", primaryTableId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new TableNotFoundException(primaryTableId, $"Primary table '{primaryTableId}' not found.");

            primaryRowVersion = reader.GetInt64(1);
            if (primaryRowVersion != request.ExpectedPrimaryRowVersion)
                throw new TableMergeConcurrencyException(primaryTableId, request.ExpectedPrimaryRowVersion, primaryRowVersion);
        }

        // 3. Lock and validate all Participant Tables
        var expectedVersionMap = request.ExpectedParticipantVersions.ToDictionary(p => p.TableId, p => p.ExpectedRowVersion);

        foreach (var mergeRecord in mergeRecords)
        {
            if (!expectedVersionMap.TryGetValue(mergeRecord.MergedTableId, out var expectedVersion))
                throw new ArgumentException($"Missing expected row version for participant table {mergeRecord.MergedTableId}.", nameof(request));

            const string selectPartSql = $"""
                SELECT table_id, row_version
                FROM {TablesTable}
                WHERE table_id = @part_id
                FOR UPDATE;
                """;

            await using var cmd = new NpgsqlCommand(selectPartSql, connection, transaction);
            cmd.Parameters.AddWithValue("part_id", mergeRecord.MergedTableId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new TableNotFoundException(mergeRecord.MergedTableId, $"Participant table '{mergeRecord.MergedTableId}' not found.");

            var actualVersion = reader.GetInt64(1);
            if (actualVersion != expectedVersion)
                throw new TableMergeConcurrencyException(mergeRecord.MergedTableId, expectedVersion, actualVersion);
        }

        // 4. Payment-aware topology policy (V13-TBL-001): see
        // PaymentAwareTableTopologyPolicy's own doc comment — only an
        // unsettled (Pending/Unknown/ReconciliationRequired) Payment blocks
        // an unmerge; a merely partially allocated/paid bill is safe.
        var primaryBillIdsForPolicy = new List<Guid>();
        const string checkBillsSql = $"""
            SELECT bill_id
            FROM {BillsTable}
            WHERE table_id = @primary_id AND status NOT IN ('Cancelled');
            """;

        await using (var cmd = new NpgsqlCommand(checkBillsSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("primary_id", primaryTableId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                primaryBillIdsForPolicy.Add(reader.GetGuid(0));
            }
        }

        await PaymentAwareTableTopologyPolicy.EnsureNoUnsettledPaymentForMergeAsync(
            primaryBillIdsForPolicy, connection, transaction, cancellationToken);

        // 5. Restore Orders and Bills to their original participant tables
        var restoredOrderIds = new List<Guid>();
        var restoredBillIds = new List<Guid>();
        var newParticipantRowVersions = new Dictionary<Guid, long>();

        foreach (var mergeRecord in mergeRecords)
        {
            var expectedVersion = expectedVersionMap[mergeRecord.MergedTableId];

            // The identified order/bill move back from the primary to the table
            // they came from. One table event carries both; Order and Bill
            // apply their own move when the outbox delivers it (V0-ARC-001
            // row 3). The move is a no-op if the row is no longer on the
            // primary, so the restored-id lists are optimistic.
            if (mergeRecord.OriginalOrderId.HasValue || mergeRecord.OriginalBillId.HasValue)
            {
                await OutboxStore.EnqueueAsync(
                    new OutboxEnvelope(
                        IntegrationEventTypes.TableUnmerged,
                        "table_merge",
                        mergeRecord.MergedTableId,
                        IntegrationEventSerializer.Serialize(new TableUnmerged(
                            mergeRecord.MergeGroupId,
                            mergeRecord.MergedTableId,
                            primaryTableId,
                            mergeRecord.OriginalOrderId,
                            mergeRecord.OriginalBillId,
                            now))),
                    connection,
                    transaction,
                    cancellationToken);

                if (mergeRecord.OriginalOrderId.HasValue)
                    restoredOrderIds.Add(mergeRecord.OriginalOrderId.Value);
                if (mergeRecord.OriginalBillId.HasValue)
                    restoredBillIds.Add(mergeRecord.OriginalBillId.Value);
            }

            // Restore participant table state & pointers
            var hasRestoredWork = mergeRecord.OriginalOrderId.HasValue || mergeRecord.OriginalBillId.HasValue;
            var targetStatus = hasRestoredWork ? "Occupied" : "Available";

            const string updatePartTableSql = $"""
                UPDATE {TablesTable}
                SET current_status = @status,
                    current_order_id = @order_id,
                    current_bill_id = @bill_id,
                    row_version = row_version + 1
                WHERE table_id = @part_id AND row_version = @expected_version
                RETURNING row_version;
                """;

            await using (var cmd = new NpgsqlCommand(updatePartTableSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("status", targetStatus);
                cmd.Parameters.AddWithValue("order_id", (object?)mergeRecord.OriginalOrderId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("bill_id", (object?)mergeRecord.OriginalBillId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("part_id", mergeRecord.MergedTableId);
                cmd.Parameters.AddWithValue("expected_version", expectedVersion);
                var result = await cmd.ExecuteScalarAsync(cancellationToken);
                if (result is null or DBNull)
                    throw new TableMergeConcurrencyException(mergeRecord.MergedTableId, expectedVersion, 0);

                newParticipantRowVersions[mergeRecord.MergedTableId] = (long)result;
            }

            // Update merge record to Unmerged
            const string updateMergeSql = $"""
                UPDATE {TableMergesTable}
                SET status = 'Unmerged',
                    unmerged_at = @now,
                    unmerged_by = @unmerged_by,
                    unmerge_reason = @reason,
                    row_version = row_version + 1
                WHERE table_merge_id = @id;
                """;

            await using (var cmd = new NpgsqlCommand(updateMergeSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("now", now);
                cmd.Parameters.AddWithValue("unmerged_by", request.UnmergedBy);
                cmd.Parameters.AddWithValue("reason", request.Reason);
                cmd.Parameters.AddWithValue("id", mergeRecord.Id);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        // 6. Restore Primary Table state & pointers
        Guid? remainingOrderId = null;
        Guid? remainingBillId = null;

        const string selectRemainingOrderSql = $"""
            SELECT order_id FROM {OrdersTable}
            WHERE table_id = @primary_id AND status NOT IN ('Completed', 'Cancelled')
            LIMIT 1;
            """;
        await using (var cmd = new NpgsqlCommand(selectRemainingOrderSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("primary_id", primaryTableId);
            var res = await cmd.ExecuteScalarAsync(cancellationToken);
            if (res is not null and not DBNull)
                remainingOrderId = (Guid)res;
        }

        const string selectRemainingBillSql = $"""
            SELECT bill_id FROM {BillsTable}
            WHERE table_id = @primary_id AND status NOT IN ('Paid', 'Cancelled')
            LIMIT 1;
            """;
        await using (var cmd = new NpgsqlCommand(selectRemainingBillSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("primary_id", primaryTableId);
            var res = await cmd.ExecuteScalarAsync(cancellationToken);
            if (res is not null and not DBNull)
                remainingBillId = (Guid)res;
        }

        var primaryFinalStatus = remainingOrderId.HasValue || remainingBillId.HasValue ? "Occupied" : "Available";
        long newPrimaryRowVersion;

        const string updatePrimarySql = $"""
            UPDATE {TablesTable}
            SET current_status = @status,
                current_order_id = @order_id,
                current_bill_id = @bill_id,
                row_version = row_version + 1
            WHERE table_id = @primary_id AND row_version = @expected_version
            RETURNING row_version;
            """;

        await using (var cmd = new NpgsqlCommand(updatePrimarySql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("status", primaryFinalStatus);
            cmd.Parameters.AddWithValue("order_id", (object?)remainingOrderId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("bill_id", (object?)remainingBillId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("primary_id", primaryTableId);
            cmd.Parameters.AddWithValue("expected_version", request.ExpectedPrimaryRowVersion);
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            if (result is null or DBNull)
                throw new TableMergeConcurrencyException(primaryTableId, request.ExpectedPrimaryRowVersion, primaryRowVersion);

            newPrimaryRowVersion = (long)result;
        }

        // 7. Append Audit Event to audit.audit_events (AUD-01: Fail-Closed)
        const string insertAuditSql = $"""
            INSERT INTO {AuditEventsTable} (
                id, event_name, aggregate_type, aggregate_id, actor_id, actor_type,
                reason, correlation_id, causation_id, before_state_json, after_state_json,
                metadata_json, occurred_at
            ) VALUES (
                @id, 'Table.Unmerged', 'Table', @primary_id, @actor_id, 'User',
                @reason, @correlation_id, NULL, @before_state_json, @after_state_json,
                @metadata_json, @occurred_at
            );
            """;

        var metadata = new
        {
            MergeGroupId = request.MergeGroupId,
            Reason = request.Reason,
            UnmergedBy = request.UnmergedBy,
            RestoredOrderIds = restoredOrderIds,
            RestoredBillIds = restoredBillIds
        };

        await using (var auditCmd = new NpgsqlCommand(insertAuditSql, connection, transaction))
        {
            auditCmd.Parameters.AddWithValue("id", Guid.NewGuid());
            auditCmd.Parameters.AddWithValue("primary_id", primaryTableId);
            auditCmd.Parameters.AddWithValue("actor_id", request.UnmergedBy);
            auditCmd.Parameters.AddWithValue("reason", request.Reason);
            auditCmd.Parameters.AddWithValue("correlation_id", request.MergeGroupId.ToString("N"));

            auditCmd.Parameters.AddWithValue("before_state_json", DBNull.Value);
            auditCmd.Parameters.AddWithValue("after_state_json", DBNull.Value);

            var pMeta = auditCmd.Parameters.AddWithValue("metadata_json", JsonSerializer.Serialize(metadata));
            pMeta.NpgsqlDbType = NpgsqlDbType.Jsonb;

            auditCmd.Parameters.AddWithValue("occurred_at", now);

            await auditCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new TableUnmergeResult(
            request.MergeGroupId,
            primaryTableId,
            newPrimaryRowVersion,
            newParticipantRowVersions,
            restoredOrderIds,
            restoredBillIds,
            now);
    }

}
