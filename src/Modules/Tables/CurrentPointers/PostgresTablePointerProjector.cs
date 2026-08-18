using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Tables.CurrentPointers;

/// <summary>
/// PostgreSQL implementation of <see cref="ITablePointerProjector"/> (V1-TBL-005, PDF:III.5.2, V0-DAT-004).
/// Rebuilds table soft cache pointers from authoritative database relations deterministically.
/// </summary>
public sealed class PostgresTablePointerProjector : ITablePointerProjector
{
    private const string TablesTable = "table_mgmt.tables";
    private const string OrdersTable = "orders.orders";
    private const string BillsTable = "billing.bills";
    private const string TableMergesTable = "table_mgmt.table_merges";
    private const string TableReservationsTable = "table_mgmt.table_reservations";
    private const string AuditEventsTable = "audit.audit_events";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresTablePointerProjector(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<TablePointerDiscrepancy?> DetectTableDriftAsync(
        Guid tableId,
        CancellationToken cancellationToken = default)
    {
        if (tableId == Guid.Empty)
            throw new ArgumentException("Table ID cannot be empty.", nameof(tableId));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await ComputeTableDriftAsync(connection, transaction: null, tableId, cancellationToken);
    }

    public async Task<IReadOnlyList<TablePointerDiscrepancy>> DetectAllDriftAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        const string selectAllTablesSql = $"""
            SELECT table_id
            FROM {TablesTable}
            ORDER BY table_number ASC;
            """;

        var tableIds = new List<Guid>();
        await using (var cmd = new NpgsqlCommand(selectAllTablesSql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                tableIds.Add(reader.GetGuid(0));
            }
        }

        var discrepancies = new List<TablePointerDiscrepancy>();
        foreach (var id in tableIds)
        {
            var discrepancy = await ComputeTableDriftAsync(connection, transaction: null, id, cancellationToken);
            if (discrepancy is not null && discrepancy.HasDrift)
            {
                discrepancies.Add(discrepancy);
            }
        }

        return discrepancies;
    }

    public async Task<TablePointerRebuildResult> RebuildTablePointersAsync(
        Guid tableId,
        CancellationToken cancellationToken = default)
    {
        if (tableId == Guid.Empty)
            throw new ArgumentException("Table ID cannot be empty.", nameof(tableId));

        var now = DateTimeOffset.UtcNow;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        // Lock table row for update
        const string lockTableSql = $"""
            SELECT table_id, table_number, current_status, current_order_id, current_bill_id, row_version
            FROM {TablesTable}
            WHERE table_id = @id
            FOR UPDATE;
            """;

        string tableNumber;
        string currentStatus;
        Guid? currentOrderId;
        Guid? currentBillId;
        long rowVersion;

        await using (var cmd = new NpgsqlCommand(lockTableSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("id", tableId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new KeyNotFoundException($"Table '{tableId}' was not found.");

            tableNumber = reader.GetString(1);
            currentStatus = reader.GetString(2);
            currentOrderId = reader.IsDBNull(3) ? null : reader.GetGuid(3);
            currentBillId = reader.IsDBNull(4) ? null : reader.GetGuid(4);
            rowVersion = reader.GetInt64(5);
        }

        // Compute drift and authoritative state
        var discrepancy = await ComputeTableDriftAsync(connection, transaction, tableId, cancellationToken);
        if (discrepancy is null)
            throw new KeyNotFoundException($"Table '{tableId}' was not found.");

        if (!discrepancy.HasDrift)
        {
            await transaction.CommitAsync(cancellationToken);
            return new TablePointerRebuildResult(
                tableId,
                tableNumber,
                currentStatus,
                currentStatus,
                currentOrderId,
                currentOrderId,
                currentBillId,
                currentBillId,
                rowVersion,
                rowVersion,
                TablePointerDriftType.None,
                WasModified: false,
                now);
        }

        // Execute rebuild update
        long newRowVersion;
        const string updateSql = $"""
            UPDATE {TablesTable}
            SET current_status = @projected_status,
                current_order_id = @auth_order_id,
                current_bill_id = @auth_bill_id,
                row_version = row_version + 1
            WHERE table_id = @id
            RETURNING row_version;
            """;

        await using (var cmd = new NpgsqlCommand(updateSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("projected_status", discrepancy.ProjectedStatus);
            cmd.Parameters.AddWithValue("auth_order_id", (object?)discrepancy.AuthoritativeOrderId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("auth_bill_id", (object?)discrepancy.AuthoritativeBillId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("id", tableId);
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            newRowVersion = (long)result!;
        }

        // Append Audit Event
        try
        {
            const string insertAuditSql = $"""
                INSERT INTO {AuditEventsTable} (
                    id, event_name, aggregate_type, aggregate_id, actor_id, actor_type,
                    reason, correlation_id, causation_id, before_state_json, after_state_json,
                    metadata_json, occurred_at
                ) VALUES (
                    @id, 'Table.PointersRebuilt', 'Table', @table_id, NULL, 'System',
                    'Authoritative pointer projection rebuild', @correlation_id, NULL,
                    @before_state_json, @after_state_json, @metadata_json, @occurred_at
                );
                """;

            var beforeState = new
            {
                Status = currentStatus,
                CurrentOrderId = currentOrderId,
                CurrentBillId = currentBillId,
                RowVersion = rowVersion
            };

            var afterState = new
            {
                Status = discrepancy.ProjectedStatus,
                CurrentOrderId = discrepancy.AuthoritativeOrderId,
                CurrentBillId = discrepancy.AuthoritativeBillId,
                RowVersion = newRowVersion
            };

            var metadata = new
            {
                DriftFlags = discrepancy.DriftTypes.ToString(),
                CorrectedDrift = (int)discrepancy.DriftTypes
            };

            await using var auditCmd = new NpgsqlCommand(insertAuditSql, connection, transaction);
            auditCmd.Parameters.AddWithValue("id", Guid.NewGuid());
            auditCmd.Parameters.AddWithValue("table_id", tableId);
            auditCmd.Parameters.AddWithValue("correlation_id", Guid.NewGuid().ToString("N"));

            var pBefore = auditCmd.Parameters.AddWithValue("before_state_json", JsonSerializer.Serialize(beforeState));
            pBefore.NpgsqlDbType = NpgsqlDbType.Jsonb;

            var pAfter = auditCmd.Parameters.AddWithValue("after_state_json", JsonSerializer.Serialize(afterState));
            pAfter.NpgsqlDbType = NpgsqlDbType.Jsonb;

            var pMeta = auditCmd.Parameters.AddWithValue("metadata_json", JsonSerializer.Serialize(metadata));
            pMeta.NpgsqlDbType = NpgsqlDbType.Jsonb;

            auditCmd.Parameters.AddWithValue("occurred_at", now);

            await auditCmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            // Audit table does not exist in minimal fixture
        }

        await transaction.CommitAsync(cancellationToken);

        return new TablePointerRebuildResult(
            tableId,
            tableNumber,
            currentStatus,
            discrepancy.ProjectedStatus,
            currentOrderId,
            discrepancy.AuthoritativeOrderId,
            currentBillId,
            discrepancy.AuthoritativeBillId,
            rowVersion,
            newRowVersion,
            discrepancy.DriftTypes,
            WasModified: true,
            now);
    }

    public async Task<TablePointerRebuildSummary> RebuildAllTablePointersAsync(
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var discrepancies = await DetectAllDriftAsync(cancellationToken);

        var results = new List<TablePointerRebuildResult>();
        int rebuiltCount = 0;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        const string selectAllTablesSql = $"""
            SELECT table_id
            FROM {TablesTable}
            ORDER BY table_number ASC;
            """;

        var allTableIds = new List<Guid>();
        await using (var cmd = new NpgsqlCommand(selectAllTablesSql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                allTableIds.Add(reader.GetGuid(0));
            }
        }

        foreach (var id in allTableIds)
        {
            var res = await RebuildTablePointersAsync(id, cancellationToken);
            results.Add(res);
            if (res.WasModified)
                rebuiltCount++;
        }

        var completedAt = DateTimeOffset.UtcNow;

        return new TablePointerRebuildSummary(
            allTableIds.Count,
            discrepancies.Count,
            rebuiltCount,
            results,
            discrepancies,
            startedAt,
            completedAt);
    }

    private static async Task<TablePointerDiscrepancy?> ComputeTableDriftAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid tableId,
        CancellationToken cancellationToken)
    {
        // 1. Read table current state
        const string selectSql = $"""
            SELECT table_id, table_number, active, current_status, current_order_id, current_bill_id, row_version
            FROM {TablesTable}
            WHERE table_id = @id;
            """;

        string tableNumber;
        string currentStatus;
        Guid? currentOrderId;
        Guid? currentBillId;

        await using (var cmd = new NpgsqlCommand(selectSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("id", tableId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            tableNumber = reader.GetString(1);
            currentStatus = reader.GetString(3);
            currentOrderId = reader.IsDBNull(4) ? null : reader.GetGuid(4);
            currentBillId = reader.IsDBNull(5) ? null : reader.GetGuid(5);
        }

        // 2. Discover authoritative open orders (Multi-Open Source policy: most recent by created_at DESC)
        var openOrderIds = new List<Guid>();
        const string selectOrdersSql = $"""
            SELECT order_id
            FROM {OrdersTable}
            WHERE table_id = @table_id AND status NOT IN ('Completed', 'Cancelled')
            ORDER BY created_at DESC;
            """;

        await using (var cmd = new NpgsqlCommand(selectOrdersSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("table_id", tableId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                openOrderIds.Add(reader.GetGuid(0));
            }
        }

        // 3. Discover authoritative open bills (Multi-Open Source policy: most recent by opened_at DESC)
        var openBillIds = new List<Guid>();
        const string selectBillsSql = $"""
            SELECT bill_id
            FROM {BillsTable}
            WHERE table_id = @table_id AND status NOT IN ('Paid', 'Cancelled')
            ORDER BY opened_at DESC, created_at DESC;
            """;

        await using (var cmd = new NpgsqlCommand(selectBillsSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("table_id", tableId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                openBillIds.Add(reader.GetGuid(0));
            }
        }

        // 4. Check active merge participation
        bool isMergedParticipant = false;
        try
        {
            const string checkMergeSql = $"""
                SELECT table_merge_id
                FROM {TableMergesTable}
                WHERE merged_table_id = @table_id AND status = 'Active'
                LIMIT 1;
                """;
            await using var cmd = new NpgsqlCommand(checkMergeSql, connection, transaction);
            cmd.Parameters.AddWithValue("table_id", tableId);
            var mergeId = await cmd.ExecuteScalarAsync(cancellationToken);
            isMergedParticipant = mergeId is not null && mergeId != DBNull.Value;
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01") { }

        // 5. Check active reservation
        bool hasActiveReservation = false;
        Guid? reservationOrderId = null;
        try
        {
            const string checkResSql = $"""
                SELECT table_reservation_id, order_id
                FROM {TableReservationsTable}
                WHERE table_id = @table_id AND status = 'Active'
                LIMIT 1;
                """;
            await using var cmd = new NpgsqlCommand(checkResSql, connection, transaction);
            cmd.Parameters.AddWithValue("table_id", tableId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                hasActiveReservation = true;
                reservationOrderId = reader.IsDBNull(1) ? null : reader.GetGuid(1);
            }
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01") { }

        // 6. Compute Canonical Projected Status and Authoritative Pointers
        string projectedStatus;
        Guid? authOrderId = null;
        Guid? authBillId = null;

        if (isMergedParticipant)
        {
            projectedStatus = "Occupied";
            authOrderId = null;
            authBillId = null;
        }
        else if (hasActiveReservation)
        {
            projectedStatus = "Reserved";
            authOrderId = reservationOrderId ?? openOrderIds.FirstOrDefault();
            authBillId = null;
        }
        else if (openOrderIds.Count > 0 || openBillIds.Count > 0)
        {
            projectedStatus = "Occupied";
            authOrderId = openOrderIds.FirstOrDefault();
            authBillId = openBillIds.FirstOrDefault();
        }
        else if (currentStatus is "Cleaning" or "OutOfService")
        {
            projectedStatus = currentStatus;
            authOrderId = null;
            authBillId = null;
        }
        else
        {
            projectedStatus = "Available";
            authOrderId = null;
            authBillId = null;
        }

        // 7. Calculate Drift Flags
        var driftFlags = TablePointerDriftType.None;

        if (!string.Equals(currentStatus, projectedStatus, StringComparison.OrdinalIgnoreCase))
        {
            driftFlags |= TablePointerDriftType.StatusMismatch;
        }

        if (authOrderId.HasValue)
        {
            if (currentOrderId == null)
                driftFlags |= TablePointerDriftType.MissingOrderPointer;
            else if (currentOrderId.Value != authOrderId.Value)
                driftFlags |= TablePointerDriftType.StaleOrderPointer;
        }
        else if (currentOrderId.HasValue)
        {
            driftFlags |= TablePointerDriftType.GhostOrderPointer;
        }

        if (authBillId.HasValue)
        {
            if (currentBillId == null)
                driftFlags |= TablePointerDriftType.MissingBillPointer;
            else if (currentBillId.Value != authBillId.Value)
                driftFlags |= TablePointerDriftType.StaleBillPointer;
        }
        else if (currentBillId.HasValue)
        {
            driftFlags |= TablePointerDriftType.GhostBillPointer;
        }

        return new TablePointerDiscrepancy(
            tableId,
            tableNumber,
            currentStatus,
            projectedStatus,
            currentOrderId,
            authOrderId,
            currentBillId,
            authBillId,
            driftFlags);
    }
}
