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
    public async Task<TableMergeResult> ExecuteMergeAsync(
        TableMergeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var now = request.MergedAt ?? DateTimeOffset.UtcNow;
        var mergeGroupId = Guid.NewGuid();
        var allTableIds = new List<Guid> { request.PrimaryTableId };
        allTableIds.AddRange(request.Participants.Select(p => p.TableId));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        // 0. Canonical lock ordering: Lock all involved tables in deterministic Guid ascending order to prevent PostgreSQL 40P01 deadlocks
        var allTableIdsToLock = allTableIds.Distinct().OrderBy(id => id).ToArray();
        const string canonicalLockSql = $"""
            SELECT table_id FROM {TablesTable}
            WHERE table_id = ANY(@table_ids)
            ORDER BY table_id
            FOR UPDATE;
            """;
        await using (var lockCmd = new NpgsqlCommand(canonicalLockSql, connection, transaction))
        {
            lockCmd.Parameters.AddWithValue("table_ids", allTableIdsToLock);
            await using var lockReader = await lockCmd.ExecuteReaderAsync(cancellationToken);
            while (await lockReader.ReadAsync(cancellationToken)) { }
        }

        // 1. Lock and validate Primary Table
        string primaryStatus;
        Guid? primaryCurrentOrderId;
        Guid? primaryCurrentBillId;
        long primaryRowVersion;

        const string selectPrimarySql = $"""
            SELECT table_id, table_number, active, current_status, current_order_id, current_bill_id, row_version
            FROM {TablesTable}
            WHERE table_id = @primary_id;
            """;

        await using (var cmd = new NpgsqlCommand(selectPrimarySql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("primary_id", request.PrimaryTableId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new TableNotFoundException(request.PrimaryTableId, $"Primary table '{request.PrimaryTableId}' not found.");

            var active = reader.GetBoolean(2);
            primaryStatus = reader.GetString(3);
            primaryCurrentOrderId = reader.IsDBNull(4) ? null : reader.GetGuid(4);
            primaryCurrentBillId = reader.IsDBNull(5) ? null : reader.GetGuid(5);
            primaryRowVersion = reader.GetInt64(6);

            if (!active)
                throw new InvalidTableMergeStateException(request.PrimaryTableId, "Inactive", "Primary table is inactive.");

            if (primaryStatus is "Reserved" or "Cleaning" or "OutOfService")
                throw new InvalidTableMergeStateException(request.PrimaryTableId, primaryStatus, $"Primary table is in {primaryStatus} state.");

            if (primaryRowVersion != request.ExpectedPrimaryRowVersion)
                throw new TableMergeConcurrencyException(request.PrimaryTableId, request.ExpectedPrimaryRowVersion, primaryRowVersion);
        }

        // Verify primary table is not already in an active merge as a participant
        const string checkPrimaryMergedSql = $"""
            SELECT table_merge_id FROM {TableMergesTable}
            WHERE merged_table_id = @primary_id AND status = 'Active'
            LIMIT 1;
            """;
        await using (var cmd = new NpgsqlCommand(checkPrimaryMergedSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("primary_id", request.PrimaryTableId);
            var activeMergeId = await cmd.ExecuteScalarAsync(cancellationToken);
            if (activeMergeId is not null and not DBNull)
                throw new InvalidTableMergeStateException(request.PrimaryTableId, "Merged", "Primary table is already a merged participant in another active merge.");
        }

        // 2. Lock and validate each Participant Table
        var participantData = new Dictionary<Guid, (string Status, Guid? CurrentOrderId, Guid? CurrentBillId, long RowVersion)>();

        foreach (var participant in request.Participants)
        {
            const string selectParticipantSql = $"""
                SELECT table_id, table_number, active, current_status, current_order_id, current_bill_id, row_version
                FROM {TablesTable}
                WHERE table_id = @participant_id;
                """;

            await using var cmd = new NpgsqlCommand(selectParticipantSql, connection, transaction);
            cmd.Parameters.AddWithValue("participant_id", participant.TableId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new TableNotFoundException(participant.TableId, $"Participant table '{participant.TableId}' not found.");

            var active = reader.GetBoolean(2);
            var status = reader.GetString(3);
            Guid? currentOrderId = reader.IsDBNull(4) ? null : reader.GetGuid(4);
            Guid? currentBillId = reader.IsDBNull(5) ? null : reader.GetGuid(5);
            var rowVersion = reader.GetInt64(6);

            if (!active)
                throw new InvalidTableMergeStateException(participant.TableId, "Inactive", "Participant table is inactive.");

            if (status is "Reserved" or "Cleaning" or "OutOfService")
                throw new InvalidTableMergeStateException(participant.TableId, status, $"Participant table is in {status} state.");

            if (rowVersion != participant.ExpectedRowVersion)
                throw new TableMergeConcurrencyException(participant.TableId, participant.ExpectedRowVersion, rowVersion);

            participantData[participant.TableId] = (status, currentOrderId, currentBillId, rowVersion);
        }

        // Verify no participant is already part of an active merge
        foreach (var participant in request.Participants)
        {
            const string checkParticipantMergedSql = $"""
                SELECT table_merge_id FROM {TableMergesTable}
                WHERE (primary_table_id = @table_id OR merged_table_id = @table_id) AND status = 'Active'
                LIMIT 1;
                """;
            await using var cmd = new NpgsqlCommand(checkParticipantMergedSql, connection, transaction);
            cmd.Parameters.AddWithValue("table_id", participant.TableId);
            var activeMergeId = await cmd.ExecuteScalarAsync(cancellationToken);
            if (activeMergeId is not null and not DBNull)
                throw new InvalidTableMergeStateException(participant.TableId, "Merged", $"Participant table {participant.TableId} is already part of an active merge.");
        }

        // 3. Payment-aware topology policy (V13-TBL-001): a Bill with any
        // Payment currently Pending/Unknown/ReconciliationRequired locks
        // every table it touches out of merge until it settles. A merely
        // partially allocated/paid Bill is safe — its own bill_id never
        // changes, so its allocations never move to the wrong Bill (see
        // PaymentAwareTableTopologyPolicy's own doc comment).
        var participantBillIdsForPolicy = new List<Guid>();
        const string checkBillsSql = $"""
            SELECT bill_id
            FROM {BillsTable}
            WHERE table_id = ANY(@table_ids) AND status NOT IN ('Paid', 'Cancelled');
            """;

        await using (var cmd = new NpgsqlCommand(checkBillsSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("table_ids", allTableIds.ToArray());
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                participantBillIdsForPolicy.Add(reader.GetGuid(0));
            }
        }

        await PaymentAwareTableTopologyPolicy.EnsureNoUnsettledPaymentForMergeAsync(
            participantBillIdsForPolicy, connection, transaction, cancellationToken);

        // 4. Consolidate and reparent Orders and Bills from participants to Primary Table
        var allConsolidatedOrderIds = new List<Guid>();
        var allConsolidatedBillIds = new List<Guid>();
        var tableMergeIds = new List<Guid>();
        var newParticipantRowVersions = new Dictionary<Guid, long>();

        // Gather primary table's existing orders and bills
        const string selectPrimaryOrdersSql = $"""
            SELECT order_id FROM {OrdersTable}
            WHERE table_id = @primary_id AND status NOT IN ('Completed', 'Cancelled');
            """;
        await using (var cmd = new NpgsqlCommand(selectPrimaryOrdersSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("primary_id", request.PrimaryTableId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                allConsolidatedOrderIds.Add(reader.GetGuid(0));
            }
        }

        const string selectPrimaryBillsSql = $"""
            SELECT bill_id FROM {BillsTable}
            WHERE table_id = @primary_id AND status NOT IN ('Paid', 'Cancelled');
            """;
        await using (var cmd = new NpgsqlCommand(selectPrimaryBillsSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("primary_id", request.PrimaryTableId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                allConsolidatedBillIds.Add(reader.GetGuid(0));
            }
        }

        foreach (var participant in request.Participants)
        {
            var pData = participantData[participant.TableId];

            // Discover participant orders
            var participantOrderIds = new List<Guid>();
            const string selectPartOrdersSql = $"""
                SELECT order_id FROM {OrdersTable}
                WHERE table_id = @part_id AND status NOT IN ('Completed', 'Cancelled');
                """;
            await using (var cmd = new NpgsqlCommand(selectPartOrdersSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("part_id", participant.TableId);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    participantOrderIds.Add(reader.GetGuid(0));
                }
            }

            // Discover participant bills
            var participantBillIds = new List<Guid>();
            const string selectPartBillsSql = $"""
                SELECT bill_id FROM {BillsTable}
                WHERE table_id = @part_id AND status NOT IN ('Paid', 'Cancelled');
                """;
            await using (var cmd = new NpgsqlCommand(selectPartBillsSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("part_id", participant.TableId);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    participantBillIds.Add(reader.GetGuid(0));
                }
            }

            var origOrderId = pData.CurrentOrderId ?? participantOrderIds.FirstOrDefault();
            var origBillId = pData.CurrentBillId ?? participantBillIds.FirstOrDefault();

            // The participant's still-active orders and bills follow the table
            // to the primary. One table event is written to the outbox in this
            // transaction; Order and Bill move their own rows when the outbox
            // delivers it (V0-ARC-001 row 3 — integration event, eventually
            // consistent). The ID lists below are the rows the event will move.
            await OutboxStore.EnqueueAsync(
                new OutboxEnvelope(
                    IntegrationEventTypes.TableMerged,
                    "table_merge",
                    participant.TableId,
                    IntegrationEventSerializer.Serialize(new TableMerged(
                        mergeGroupId, participant.TableId, request.PrimaryTableId, now))),
                connection,
                transaction,
                cancellationToken);
            allConsolidatedOrderIds.AddRange(participantOrderIds);
            allConsolidatedBillIds.AddRange(participantBillIds);

            // Update participant table state (marked Occupied, pointers cleared)
            const string updatePartTableSql = $"""
                UPDATE {TablesTable}
                SET current_status = 'Occupied',
                    current_order_id = NULL,
                    current_bill_id = NULL,
                    row_version = row_version + 1
                WHERE table_id = @part_id AND row_version = @expected_version
                RETURNING row_version;
                """;
            await using (var cmd = new NpgsqlCommand(updatePartTableSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("part_id", participant.TableId);
                cmd.Parameters.AddWithValue("expected_version", participant.ExpectedRowVersion);
                var result = await cmd.ExecuteScalarAsync(cancellationToken);
                if (result is null or DBNull)
                    throw new TableMergeConcurrencyException(participant.TableId, participant.ExpectedRowVersion, pData.RowVersion);

                newParticipantRowVersions[participant.TableId] = (long)result;
            }

            // Insert into table_mgmt.table_merges
            var mergeId = Guid.NewGuid();
            tableMergeIds.Add(mergeId);

            const string insertMergeSql = $"""
                INSERT INTO {TableMergesTable} (
                    table_merge_id, merge_group_id, primary_table_id, merged_table_id,
                    original_order_id, original_bill_id, status, reason, merged_by,
                    merged_at, row_version
                ) VALUES (
                    @id, @group_id, @primary_id, @merged_id,
                    @order_id, @bill_id, 'Active', @reason, @merged_by,
                    @merged_at, 1
                );
                """;

            await using (var cmd = new NpgsqlCommand(insertMergeSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("id", mergeId);
                cmd.Parameters.AddWithValue("group_id", mergeGroupId);
                cmd.Parameters.AddWithValue("primary_id", request.PrimaryTableId);
                cmd.Parameters.AddWithValue("merged_id", participant.TableId);
                cmd.Parameters.AddWithValue("order_id", (object?)origOrderId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("bill_id", (object?)origBillId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("reason", request.Reason);
                cmd.Parameters.AddWithValue("merged_by", request.MergedBy);
                cmd.Parameters.AddWithValue("merged_at", now);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        // 5. Update Primary Table
        var finalPrimaryOrderId = primaryCurrentOrderId ?? allConsolidatedOrderIds.FirstOrDefault();
        var finalPrimaryBillId = primaryCurrentBillId ?? allConsolidatedBillIds.FirstOrDefault();
        long newPrimaryRowVersion;

        const string updatePrimarySql = $"""
            UPDATE {TablesTable}
            SET current_status = 'Occupied',
                current_order_id = @order_id,
                current_bill_id = @bill_id,
                row_version = row_version + 1
            WHERE table_id = @primary_id AND row_version = @expected_version
            RETURNING row_version;
            """;

        await using (var cmd = new NpgsqlCommand(updatePrimarySql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("primary_id", request.PrimaryTableId);
            cmd.Parameters.AddWithValue("order_id", (object?)finalPrimaryOrderId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("bill_id", (object?)finalPrimaryBillId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("expected_version", request.ExpectedPrimaryRowVersion);
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            if (result is null or DBNull)
                throw new TableMergeConcurrencyException(request.PrimaryTableId, request.ExpectedPrimaryRowVersion, primaryRowVersion);

            newPrimaryRowVersion = (long)result;
        }

        // 6. Append Audit Event to audit.audit_events (AUD-01: Fail-Closed)
        const string insertAuditSql = $"""
            INSERT INTO {AuditEventsTable} (
                id, event_name, aggregate_type, aggregate_id, actor_id, actor_type,
                reason, correlation_id, causation_id, before_state_json, after_state_json,
                metadata_json, occurred_at
            ) VALUES (
                @id, 'Table.Merged', 'Table', @primary_id, @actor_id, 'User',
                @reason, @correlation_id, NULL, @before_state_json, @after_state_json,
                @metadata_json, @occurred_at
            );
            """;

        var beforeState = new
        {
            PrimaryTableId = request.PrimaryTableId,
            PrimaryStatus = primaryStatus,
            PrimaryRowVersion = primaryRowVersion,
            Participants = participantData.Select(kvp => new
            {
                TableId = kvp.Key,
                Status = kvp.Value.Status,
                RowVersion = kvp.Value.RowVersion
            })
        };

        var afterState = new
        {
            MergeGroupId = mergeGroupId,
            PrimaryTableId = request.PrimaryTableId,
            NewPrimaryRowVersion = newPrimaryRowVersion,
            NewParticipantRowVersions = newParticipantRowVersions,
            ConsolidatedOrderIds = allConsolidatedOrderIds,
            ConsolidatedBillIds = allConsolidatedBillIds
        };

        var metadata = new
        {
            MergeGroupId = mergeGroupId,
            TableMergeIds = tableMergeIds,
            Reason = request.Reason,
            MergedBy = request.MergedBy
        };

        await using (var auditCmd = new NpgsqlCommand(insertAuditSql, connection, transaction))
        {
            auditCmd.Parameters.AddWithValue("id", Guid.NewGuid());
            auditCmd.Parameters.AddWithValue("primary_id", request.PrimaryTableId);
            auditCmd.Parameters.AddWithValue("actor_id", request.MergedBy);
            auditCmd.Parameters.AddWithValue("reason", request.Reason);
            auditCmd.Parameters.AddWithValue("correlation_id", mergeGroupId.ToString("N"));

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

        return new TableMergeResult(
            mergeGroupId,
            tableMergeIds,
            request.PrimaryTableId,
            newPrimaryRowVersion,
            request.Participants.Select(p => p.TableId).ToList(),
            newParticipantRowVersions,
            allConsolidatedOrderIds,
            allConsolidatedBillIds,
            now);
    }

}
