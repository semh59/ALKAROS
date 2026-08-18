using System.Data;
using System.Data.Common;

namespace ALKAROS.Observability.AlertFoundation;

/// <summary>
/// PostgreSQL implementation of <see cref="IAlertRepository"/> (V1-ALT-001, PDF:III.28).
/// Manages alert lifecycle, deduplication, and append-only audit trail.
/// </summary>
public sealed class PostgresAlertRepository : IAlertRepository
{
    private const string AlertsTable = "observability.alerts";
    private const string EventsTable = "observability.alert_events";

    private readonly DbDataSource _dataSource;

    public PostgresAlertRepository(DbDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<AlertRecord?> GetByIdAsync(
        Guid alertId,
        CancellationToken cancellationToken = default)
    {
        if (alertId == Guid.Empty)
            throw new ArgumentException("Alert ID cannot be empty.", nameof(alertId));

        const string sql = $"""
            SELECT alert_id, alert_type, severity, status, title, message,
                   deduplication_key, source_reference_type, source_reference_id,
                   opened_at, acknowledged_at, acknowledged_by, resolved_at, resolved_by,
                   resolution_reason, row_version
            FROM {AlertsTable}
            WHERE alert_id = @id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "id", alertId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadAlertRecord(reader);
    }

    public async Task<IReadOnlyList<AlertRecord>> GetActiveAlertsAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT alert_id, alert_type, severity, status, title, message,
                   deduplication_key, source_reference_type, source_reference_id,
                   opened_at, acknowledged_at, acknowledged_by, resolved_at, resolved_by,
                   resolution_reason, row_version
            FROM {AlertsTable}
            WHERE status IN ('Open', 'Acknowledged', 'Escalated')
            ORDER BY opened_at DESC;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        var list = new List<AlertRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(ReadAlertRecord(reader));
        }

        return list;
    }

    public async Task<IReadOnlyList<AlertRecord>> GetBySourceReferenceAsync(
        string sourceReferenceType,
        Guid sourceReferenceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceReferenceType))
            throw new ArgumentException("Source reference type cannot be null or whitespace.", nameof(sourceReferenceType));

        if (sourceReferenceId == Guid.Empty)
            throw new ArgumentException("Source reference ID cannot be empty.", nameof(sourceReferenceId));

        const string sql = $"""
            SELECT alert_id, alert_type, severity, status, title, message,
                   deduplication_key, source_reference_type, source_reference_id,
                   opened_at, acknowledged_at, acknowledged_by, resolved_at, resolved_by,
                   resolution_reason, row_version
            FROM {AlertsTable}
            WHERE source_reference_type = @type AND source_reference_id = @id
            ORDER BY opened_at DESC;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "type", sourceReferenceType);
        AddParameter(cmd, "id", sourceReferenceId);

        var list = new List<AlertRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(ReadAlertRecord(reader));
        }

        return list;
    }

    public async Task<IReadOnlyList<AlertEventRecord>> GetEventsAsync(
        Guid alertId,
        CancellationToken cancellationToken = default)
    {
        if (alertId == Guid.Empty)
            throw new ArgumentException("Alert ID cannot be empty.", nameof(alertId));

        const string sql = $"""
            SELECT alert_event_id, alert_id, event_type, actor_id, payload::text, created_at
            FROM {EventsTable}
            WHERE alert_id = @alert_id
            ORDER BY created_at ASC;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "alert_id", alertId);

        var list = new List<AlertEventRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new AlertEventRecord(
                reader.GetGuid(0),
                reader.GetGuid(1),
                Enum.Parse<AlertEventType>(reader.GetString(2), ignoreCase: true),
                reader.IsDBNull(3) ? null : reader.GetGuid(3),
                reader.GetString(4),
                reader.GetFieldValue<DateTimeOffset>(5)));
        }

        return list;
    }

    public async Task<AlertRaiseResult> RaiseAlertAsync(
        RaiseAlertRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var now = DateTimeOffset.UtcNow;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        // 1. Check for Active Existing Alert for Deduplication
        AlertRecord? existingActiveAlert = null;

        if (!string.IsNullOrWhiteSpace(request.DeduplicationKey))
        {
            const string selectByDedupSql = $"""
                SELECT alert_id, alert_type, severity, status, title, message,
                       deduplication_key, source_reference_type, source_reference_id,
                       opened_at, acknowledged_at, acknowledged_by, resolved_at, resolved_by,
                       resolution_reason, row_version
                FROM {AlertsTable}
                WHERE deduplication_key = @key AND status IN ('Open', 'Acknowledged', 'Escalated')
                FOR UPDATE
                LIMIT 1;
                """;

            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = selectByDedupSql;
            AddParameter(cmd, "key", request.DeduplicationKey);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                existingActiveAlert = ReadAlertRecord(reader);
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.SourceReferenceType) && request.SourceReferenceId.HasValue)
        {
            const string selectBySourceSql = $"""
                SELECT alert_id, alert_type, severity, status, title, message,
                       deduplication_key, source_reference_type, source_reference_id,
                       opened_at, acknowledged_at, acknowledged_by, resolved_at, resolved_by,
                       resolution_reason, row_version
                FROM {AlertsTable}
                WHERE alert_type = @alert_type AND source_reference_type = @source_type
                  AND source_reference_id = @source_id AND status IN ('Open', 'Acknowledged', 'Escalated')
                FOR UPDATE
                LIMIT 1;
                """;

            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = selectBySourceSql;
            AddParameter(cmd, "alert_type", request.AlertType);
            AddParameter(cmd, "source_type", request.SourceReferenceType);
            AddParameter(cmd, "source_id", request.SourceReferenceId.Value);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                existingActiveAlert = ReadAlertRecord(reader);
            }
        }

        // 2. If Active Alert exists -> record Deduplicated event and return
        if (existingActiveAlert is not null)
        {
            // Record Deduplicated event
            await InsertAlertEventAsync(
                connection,
                transaction,
                existingActiveAlert.AlertId,
                AlertEventType.Deduplicated,
                request.ActorId,
                request.PayloadJson ?? "{}",
                now,
                cancellationToken);

            // Increment row_version on alert to register occurrence
            const string bumpVersionSql = $"""
                UPDATE {AlertsTable}
                SET row_version = row_version + 1
                WHERE alert_id = @id
                RETURNING row_version;
                """;

            long updatedVersion;
            await using (var bumpCmd = connection.CreateCommand())
            {
                bumpCmd.Transaction = transaction;
                bumpCmd.CommandText = bumpVersionSql;
                AddParameter(bumpCmd, "id", existingActiveAlert.AlertId);
                var res = await bumpCmd.ExecuteScalarAsync(cancellationToken);
                updatedVersion = (long)res!;
            }

            await transaction.CommitAsync(cancellationToken);

            var updatedAlert = existingActiveAlert with { RowVersion = updatedVersion };
            return new AlertRaiseResult(updatedAlert, IsNewAlert: false, WasDeduplicated: true);
        }

        // 3. Create New Alert
        var alertId = Guid.NewGuid();

        const string insertAlertSql = $"""
            INSERT INTO {AlertsTable} (
                alert_id, alert_type, severity, status, title, message,
                deduplication_key, source_reference_type, source_reference_id,
                opened_at, acknowledged_at, acknowledged_by, resolved_at, resolved_by,
                resolution_reason, row_version
            ) VALUES (
                @id, @alert_type, @severity, 'Open', @title, @message,
                @deduplication_key, @source_type, @source_id,
                @now, NULL, NULL, NULL, NULL, NULL, 1
            );
            """;

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = insertAlertSql;
            AddParameter(cmd, "id", alertId);
            AddParameter(cmd, "alert_type", request.AlertType);
            AddParameter(cmd, "severity", request.Severity.ToString());
            AddParameter(cmd, "title", request.Title);
            AddParameter(cmd, "message", request.Message);
            AddParameter(cmd, "deduplication_key", (object?)request.DeduplicationKey ?? DBNull.Value);
            AddParameter(cmd, "source_type", (object?)request.SourceReferenceType ?? DBNull.Value);
            AddParameter(cmd, "source_id", (object?)request.SourceReferenceId ?? DBNull.Value);
            AddParameter(cmd, "now", now);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // 4. Record Created Event
        await InsertAlertEventAsync(
            connection,
            transaction,
            alertId,
            AlertEventType.Created,
            request.ActorId,
            request.PayloadJson ?? "{}",
            now,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var newRecord = new AlertRecord(
            alertId,
            request.AlertType,
            request.Severity,
            AlertStatus.Open,
            request.Title,
            request.Message,
            request.DeduplicationKey,
            request.SourceReferenceType,
            request.SourceReferenceId,
            now,
            AcknowledgedAt: null,
            AcknowledgedBy: null,
            ResolvedAt: null,
            ResolvedBy: null,
            ResolutionReason: null,
            RowVersion: 1);

        return new AlertRaiseResult(newRecord, IsNewAlert: true, WasDeduplicated: false);
    }

    public async Task<AlertRecord> AcknowledgeAlertAsync(
        AcknowledgeAlertRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var now = DateTimeOffset.UtcNow;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var alert = await LockAlertAsync(connection, transaction, request.AlertId, cancellationToken);

        if (alert.Status is AlertStatus.Resolved)
        {
            throw new InvalidAlertStateException(request.AlertId, alert.Status, "Acknowledge");
        }

        if (alert.RowVersion != request.ExpectedRowVersion)
        {
            throw new AlertConcurrencyException(request.AlertId, request.ExpectedRowVersion, alert.RowVersion);
        }

        long newRowVersion;
        const string updateSql = $"""
            UPDATE {AlertsTable}
            SET status = 'Acknowledged',
                acknowledged_at = @now,
                acknowledged_by = @user_id,
                row_version = row_version + 1
            WHERE alert_id = @id AND row_version = @expected_version
            RETURNING row_version;
            """;

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = updateSql;
            AddParameter(cmd, "now", now);
            AddParameter(cmd, "user_id", request.AcknowledgedBy);
            AddParameter(cmd, "id", request.AlertId);
            AddParameter(cmd, "expected_version", request.ExpectedRowVersion);
            var res = await cmd.ExecuteScalarAsync(cancellationToken);
            if (res is null or DBNull)
                throw new AlertConcurrencyException(request.AlertId, request.ExpectedRowVersion, alert.RowVersion);

            newRowVersion = (long)res;
        }

        await InsertAlertEventAsync(
            connection,
            transaction,
            request.AlertId,
            AlertEventType.Acknowledged,
            request.AcknowledgedBy,
            request.Reason is not null ? $"{{\"reason\":\"{request.Reason}\"}}" : "{}",
            now,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return alert with
        {
            Status = AlertStatus.Acknowledged,
            AcknowledgedAt = now,
            AcknowledgedBy = request.AcknowledgedBy,
            RowVersion = newRowVersion
        };
    }

    public async Task<AlertRecord> EscalateAlertAsync(
        EscalateAlertRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var now = DateTimeOffset.UtcNow;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var alert = await LockAlertAsync(connection, transaction, request.AlertId, cancellationToken);

        if (alert.Status is AlertStatus.Resolved or AlertStatus.Suppressed)
        {
            throw new InvalidAlertStateException(request.AlertId, alert.Status, "Escalate");
        }

        if (alert.RowVersion != request.ExpectedRowVersion)
        {
            throw new AlertConcurrencyException(request.AlertId, request.ExpectedRowVersion, alert.RowVersion);
        }

        long newRowVersion;
        const string updateSql = $"""
            UPDATE {AlertsTable}
            SET status = 'Escalated',
                row_version = row_version + 1
            WHERE alert_id = @id AND row_version = @expected_version
            RETURNING row_version;
            """;

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = updateSql;
            AddParameter(cmd, "id", request.AlertId);
            AddParameter(cmd, "expected_version", request.ExpectedRowVersion);
            var res = await cmd.ExecuteScalarAsync(cancellationToken);
            if (res is null or DBNull)
                throw new AlertConcurrencyException(request.AlertId, request.ExpectedRowVersion, alert.RowVersion);

            newRowVersion = (long)res;
        }

        await InsertAlertEventAsync(
            connection,
            transaction,
            request.AlertId,
            AlertEventType.Escalated,
            request.EscalatedBy,
            request.Reason is not null ? $"{{\"reason\":\"{request.Reason}\"}}" : "{}",
            now,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return alert with
        {
            Status = AlertStatus.Escalated,
            RowVersion = newRowVersion
        };
    }

    public async Task<AlertRecord> SuppressAlertAsync(
        SuppressAlertRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var now = DateTimeOffset.UtcNow;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var alert = await LockAlertAsync(connection, transaction, request.AlertId, cancellationToken);

        if (alert.Status is AlertStatus.Resolved)
        {
            throw new InvalidAlertStateException(request.AlertId, alert.Status, "Suppress");
        }

        if (alert.RowVersion != request.ExpectedRowVersion)
        {
            throw new AlertConcurrencyException(request.AlertId, request.ExpectedRowVersion, alert.RowVersion);
        }

        long newRowVersion;
        const string updateSql = $"""
            UPDATE {AlertsTable}
            SET status = 'Suppressed',
                row_version = row_version + 1
            WHERE alert_id = @id AND row_version = @expected_version
            RETURNING row_version;
            """;

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = updateSql;
            AddParameter(cmd, "id", request.AlertId);
            AddParameter(cmd, "expected_version", request.ExpectedRowVersion);
            var res = await cmd.ExecuteScalarAsync(cancellationToken);
            if (res is null or DBNull)
                throw new AlertConcurrencyException(request.AlertId, request.ExpectedRowVersion, alert.RowVersion);

            newRowVersion = (long)res;
        }

        await InsertAlertEventAsync(
            connection,
            transaction,
            request.AlertId,
            AlertEventType.Suppressed,
            request.SuppressedBy,
            request.Reason is not null ? $"{{\"reason\":\"{request.Reason}\"}}" : "{}",
            now,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return alert with
        {
            Status = AlertStatus.Suppressed,
            RowVersion = newRowVersion
        };
    }

    public async Task<AlertRecord> ResolveAlertAsync(
        ResolveAlertRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var now = DateTimeOffset.UtcNow;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var alert = await LockAlertAsync(connection, transaction, request.AlertId, cancellationToken);

        if (alert.Status is AlertStatus.Resolved)
        {
            throw new InvalidAlertStateException(request.AlertId, alert.Status, "Resolve");
        }

        if (alert.RowVersion != request.ExpectedRowVersion)
        {
            throw new AlertConcurrencyException(request.AlertId, request.ExpectedRowVersion, alert.RowVersion);
        }

        long newRowVersion;
        const string updateSql = $"""
            UPDATE {AlertsTable}
            SET status = 'Resolved',
                resolved_at = @now,
                resolved_by = @user_id,
                resolution_reason = @reason,
                row_version = row_version + 1
            WHERE alert_id = @id AND row_version = @expected_version
            RETURNING row_version;
            """;

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = updateSql;
            AddParameter(cmd, "now", now);
            AddParameter(cmd, "user_id", request.ResolvedBy);
            AddParameter(cmd, "reason", request.ResolutionReason);
            AddParameter(cmd, "id", request.AlertId);
            AddParameter(cmd, "expected_version", request.ExpectedRowVersion);
            var res = await cmd.ExecuteScalarAsync(cancellationToken);
            if (res is null or DBNull)
                throw new AlertConcurrencyException(request.AlertId, request.ExpectedRowVersion, alert.RowVersion);

            newRowVersion = (long)res;
        }

        await InsertAlertEventAsync(
            connection,
            transaction,
            request.AlertId,
            AlertEventType.Resolved,
            request.ResolvedBy,
            $"{{\"resolution_reason\":\"{request.ResolutionReason}\"}}",
            now,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return alert with
        {
            Status = AlertStatus.Resolved,
            ResolvedAt = now,
            ResolvedBy = request.ResolvedBy,
            ResolutionReason = request.ResolutionReason,
            RowVersion = newRowVersion
        };
    }

    private static async Task<AlertRecord> LockAlertAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid alertId,
        CancellationToken cancellationToken)
    {
        const string selectSql = $"""
            SELECT alert_id, alert_type, severity, status, title, message,
                   deduplication_key, source_reference_type, source_reference_id,
                   opened_at, acknowledged_at, acknowledged_by, resolved_at, resolved_by,
                   resolution_reason, row_version
            FROM {AlertsTable}
            WHERE alert_id = @id
            FOR UPDATE;
            """;

        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = selectSql;
        AddParameter(cmd, "id", alertId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new AlertNotFoundException(alertId);

        return ReadAlertRecord(reader);
    }

    private static async Task InsertAlertEventAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid alertId,
        AlertEventType eventType,
        Guid? actorId,
        string payloadJson,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        const string insertSql = $"""
            INSERT INTO {EventsTable} (
                alert_event_id, alert_id, event_type, actor_id, payload, created_at
            ) VALUES (
                @id, @alert_id, @event_type, @actor_id, @payload::jsonb, @now
            );
            """;

        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = insertSql;
        AddParameter(cmd, "id", Guid.NewGuid());
        AddParameter(cmd, "alert_id", alertId);
        AddParameter(cmd, "event_type", eventType.ToString());
        AddParameter(cmd, "actor_id", (object?)actorId ?? DBNull.Value);
        AddParameter(cmd, "payload", payloadJson);
        AddParameter(cmd, "now", now);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static AlertRecord ReadAlertRecord(DbDataReader reader)
    {
        return new AlertRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            Enum.Parse<AlertSeverity>(reader.GetString(2), ignoreCase: true),
            Enum.Parse<AlertStatus>(reader.GetString(3), ignoreCase: true),
            reader.GetString(4),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetGuid(8),
            reader.GetFieldValue<DateTimeOffset>(9),
            reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10),
            reader.IsDBNull(11) ? null : reader.GetGuid(11),
            reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12),
            reader.IsDBNull(13) ? null : reader.GetGuid(13),
            reader.IsDBNull(14) ? null : reader.GetString(14),
            reader.GetInt64(15));
    }

    private static void AddParameter(DbCommand cmd, string name, object? value)
    {
        var param = cmd.CreateParameter();
        param.ParameterName = name;
        param.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(param);
    }
}
