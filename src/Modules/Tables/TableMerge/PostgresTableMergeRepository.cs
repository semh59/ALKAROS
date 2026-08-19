using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Tables.TableMerge;

/// <summary>
/// PostgreSQL implementation of <see cref="ITableMergeRepository"/> (V1-TBL-003, PDF:I.10, PDF:III.5.4).
/// Executes atomic, reversible multi-table merges and unmerges in a single database transaction.
/// </summary>
public sealed class PostgresTableMergeRepository : ITableMergeRepository
{
    private const string TableMergesTable = "table_mgmt.table_merges";
    private const string TablesTable = "table_mgmt.tables";
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

        // 3. Payment-policy validation: verify no payment data on any participating bills
        const string checkBillsSql = $"""
            SELECT bill_id, status, payable_amount, allocated_amount, paid_amount
            FROM {BillsTable}
            WHERE table_id = ANY(@table_ids) AND status NOT IN ('Paid', 'Cancelled');
            """;

        await using (var cmd = new NpgsqlCommand(checkBillsSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("table_ids", allTableIds.ToArray());
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
                        $"Bill '{billId}' is in '{status}' state. Table merge with non-open bills requires V1.2 payment policy.");
                }

                if (allocated > 0 || paid > 0)
                {
                    throw new PaymentPolicyRequiredException(
                        billId,
                        $"Bill '{billId}' has payment data (allocated: {allocated}, paid: {paid}). Table merge with payment requires V1.2 payment policy.");
                }
            }
        }

        // 3. Precondition: Check bill_allocations table (AUD-01: Fail-Closed)
        const string checkAllocationsSql = $"""
            SELECT ba.bill_id
            FROM {BillAllocationsTable} ba
            JOIN {BillsTable} b ON ba.bill_id = b.bill_id
            WHERE b.table_id = ANY(@table_ids) AND b.status NOT IN ('Paid', 'Cancelled')
            LIMIT 1;
            """;

        await using (var cmd = new NpgsqlCommand(checkAllocationsSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("table_ids", allTableIds.ToArray());
            var allocBillId = await cmd.ExecuteScalarAsync(cancellationToken);
            if (allocBillId is not null and not DBNull)
            {
                var billId = (Guid)allocBillId;
                throw new PaymentPolicyRequiredException(
                    billId,
                    $"Bill '{billId}' has split allocations in {BillAllocationsTable}. Table merge with allocations requires V1.2 payment policy.");
            }
        }

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

            // Reparent participant orders to primary table
            if (participantOrderIds.Count > 0)
            {
                const string updateOrdersSql = $"""
                    UPDATE {OrdersTable}
                    SET table_id = @primary_id,
                        updated_at = @now,
                        row_version = row_version + 1
                    WHERE table_id = @part_id AND status NOT IN ('Completed', 'Cancelled');
                    """;
                await using var cmd = new NpgsqlCommand(updateOrdersSql, connection, transaction);
                cmd.Parameters.AddWithValue("primary_id", request.PrimaryTableId);
                cmd.Parameters.AddWithValue("now", now);
                cmd.Parameters.AddWithValue("part_id", participant.TableId);
                await cmd.ExecuteNonQueryAsync(cancellationToken);

                allConsolidatedOrderIds.AddRange(participantOrderIds);
            }

            // Reparent participant bills to primary table
            if (participantBillIds.Count > 0)
            {
                const string updateBillsSql = $"""
                    UPDATE {BillsTable}
                    SET table_id = @primary_id,
                        updated_at = @now,
                        row_version = row_version + 1
                    WHERE table_id = @part_id AND status NOT IN ('Paid', 'Cancelled');
                    """;
                await using var cmd = new NpgsqlCommand(updateBillsSql, connection, transaction);
                cmd.Parameters.AddWithValue("primary_id", request.PrimaryTableId);
                cmd.Parameters.AddWithValue("now", now);
                cmd.Parameters.AddWithValue("part_id", participant.TableId);
                await cmd.ExecuteNonQueryAsync(cancellationToken);

                allConsolidatedBillIds.AddRange(participantBillIds);
            }

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

        // 4. Payment Policy Check: verify no payment activity occurred on primary table bills
        const string checkBillsSql = $"""
            SELECT bill_id, status, payable_amount, allocated_amount, paid_amount
            FROM {BillsTable}
            WHERE table_id = @primary_id AND status NOT IN ('Cancelled');
            """;

        await using (var cmd = new NpgsqlCommand(checkBillsSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("primary_id", primaryTableId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var billId = reader.GetGuid(0);
                var status = reader.GetString(1);
                var allocated = reader.GetDecimal(3);
                var paid = reader.GetDecimal(4);

                if (paid > 0 || allocated > 0 || !string.Equals(status, "Open", StringComparison.OrdinalIgnoreCase))
                {
                    throw new PaymentPolicyRequiredException(
                        billId,
                        $"Bill '{billId}' on merged primary table has payment data (paid: {paid}, allocated: {allocated}, status: {status}). Unmerging tables after payment activity requires V1.2 payment-aware policy.");
                }
            }
        }

        // 5. Restore Orders and Bills to their original participant tables
        var restoredOrderIds = new List<Guid>();
        var restoredBillIds = new List<Guid>();
        var newParticipantRowVersions = new Dictionary<Guid, long>();

        foreach (var mergeRecord in mergeRecords)
        {
            var expectedVersion = expectedVersionMap[mergeRecord.MergedTableId];

            if (mergeRecord.OriginalOrderId.HasValue)
            {
                const string restoreOrderSql = $"""
                    UPDATE {OrdersTable}
                    SET table_id = @part_id,
                        updated_at = @now,
                        row_version = row_version + 1
                    WHERE order_id = @order_id AND table_id = @primary_id AND status NOT IN ('Completed', 'Cancelled');
                    """;
                await using var cmd = new NpgsqlCommand(restoreOrderSql, connection, transaction);
                cmd.Parameters.AddWithValue("part_id", mergeRecord.MergedTableId);
                cmd.Parameters.AddWithValue("now", now);
                cmd.Parameters.AddWithValue("order_id", mergeRecord.OriginalOrderId.Value);
                cmd.Parameters.AddWithValue("primary_id", primaryTableId);
                var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
                if (rows > 0)
                    restoredOrderIds.Add(mergeRecord.OriginalOrderId.Value);
            }

            if (mergeRecord.OriginalBillId.HasValue)
            {
                const string restoreBillSql = $"""
                    UPDATE {BillsTable}
                    SET table_id = @part_id,
                        updated_at = @now,
                        row_version = row_version + 1
                    WHERE bill_id = @bill_id AND table_id = @primary_id AND status NOT IN ('Paid', 'Cancelled');
                    """;
                await using var cmd = new NpgsqlCommand(restoreBillSql, connection, transaction);
                cmd.Parameters.AddWithValue("part_id", mergeRecord.MergedTableId);
                cmd.Parameters.AddWithValue("now", now);
                cmd.Parameters.AddWithValue("bill_id", mergeRecord.OriginalBillId.Value);
                cmd.Parameters.AddWithValue("primary_id", primaryTableId);
                var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
                if (rows > 0)
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
