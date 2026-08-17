namespace ALKAROS.Audit.EventStore;

using Npgsql;
using NpgsqlTypes;

/// <summary>
/// PostgreSQL implementation of the append-only audit log store (V1-OPS-001, PDF:II.9, PDF:III.24).
/// Enforces payload sanitization and fails closed on unauthorized modification attempts.
/// </summary>
public sealed class PostgresAuditEventStore : IAuditEventStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IAuditSanitizer _sanitizer;

    public PostgresAuditEventStore(
        NpgsqlDataSource dataSource,
        IAuditSanitizer? sanitizer = null)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _sanitizer = sanitizer ?? new AuditSanitizer();
    }

    public async Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        auditEvent.Validate();

        var sanitizedBefore = _sanitizer.SanitizeJson(auditEvent.BeforeStateJson);
        var sanitizedAfter = _sanitizer.SanitizeJson(auditEvent.AfterStateJson);
        var sanitizedMetadata = _sanitizer.SanitizeJson(auditEvent.MetadataJson);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO audit.audit_events (
                id, event_name, aggregate_type, aggregate_id, actor_id, actor_type,
                reason, correlation_id, causation_id, before_state_json, after_state_json,
                metadata_json, occurred_at
            ) VALUES (
                @id, @event_name, @aggregate_type, @aggregate_id, @actor_id, @actor_type,
                @reason, @correlation_id, @causation_id, @before_state_json, @after_state_json,
                @metadata_json, @occurred_at
            );
            """;
        cmd.Parameters.AddWithValue("id", auditEvent.Id);
        cmd.Parameters.AddWithValue("event_name", auditEvent.EventName);
        cmd.Parameters.AddWithValue("aggregate_type", auditEvent.AggregateType);
        cmd.Parameters.AddWithValue("aggregate_id", auditEvent.AggregateId);
        cmd.Parameters.AddWithValue("actor_id", (object?)auditEvent.ActorId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("actor_type", auditEvent.ActorType);
        cmd.Parameters.AddWithValue("reason", (object?)auditEvent.Reason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("correlation_id", auditEvent.CorrelationId);
        cmd.Parameters.AddWithValue("causation_id", (object?)auditEvent.CausationId ?? DBNull.Value);

        var pBefore = cmd.Parameters.AddWithValue("before_state_json", (object?)sanitizedBefore ?? DBNull.Value);
        pBefore.NpgsqlDbType = NpgsqlDbType.Jsonb;

        var pAfter = cmd.Parameters.AddWithValue("after_state_json", (object?)sanitizedAfter ?? DBNull.Value);
        pAfter.NpgsqlDbType = NpgsqlDbType.Jsonb;

        var pMeta = cmd.Parameters.AddWithValue("metadata_json", (object?)sanitizedMetadata ?? DBNull.Value);
        pMeta.NpgsqlDbType = NpgsqlDbType.Jsonb;

        cmd.Parameters.AddWithValue("occurred_at", auditEvent.OccurredAt);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AppendBatchAsync(IEnumerable<AuditEvent> auditEvents, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvents);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        foreach (var evt in auditEvents)
        {
            evt.Validate();
            var sanitizedBefore = _sanitizer.SanitizeJson(evt.BeforeStateJson);
            var sanitizedAfter = _sanitizer.SanitizeJson(evt.AfterStateJson);
            var sanitizedMetadata = _sanitizer.SanitizeJson(evt.MetadataJson);

            await using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                """
                INSERT INTO audit.audit_events (
                    id, event_name, aggregate_type, aggregate_id, actor_id, actor_type,
                    reason, correlation_id, causation_id, before_state_json, after_state_json,
                    metadata_json, occurred_at
                ) VALUES (
                    @id, @event_name, @aggregate_type, @aggregate_id, @actor_id, @actor_type,
                    @reason, @correlation_id, @causation_id, @before_state_json, @after_state_json,
                    @metadata_json, @occurred_at
                );
                """;
            cmd.Parameters.AddWithValue("id", evt.Id);
            cmd.Parameters.AddWithValue("event_name", evt.EventName);
            cmd.Parameters.AddWithValue("aggregate_type", evt.AggregateType);
            cmd.Parameters.AddWithValue("aggregate_id", evt.AggregateId);
            cmd.Parameters.AddWithValue("actor_id", (object?)evt.ActorId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("actor_type", evt.ActorType);
            cmd.Parameters.AddWithValue("reason", (object?)evt.Reason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("correlation_id", evt.CorrelationId);
            cmd.Parameters.AddWithValue("causation_id", (object?)evt.CausationId ?? DBNull.Value);

            var pBefore = cmd.Parameters.AddWithValue("before_state_json", (object?)sanitizedBefore ?? DBNull.Value);
            pBefore.NpgsqlDbType = NpgsqlDbType.Jsonb;

            var pAfter = cmd.Parameters.AddWithValue("after_state_json", (object?)sanitizedAfter ?? DBNull.Value);
            pAfter.NpgsqlDbType = NpgsqlDbType.Jsonb;

            var pMeta = cmd.Parameters.AddWithValue("metadata_json", (object?)sanitizedMetadata ?? DBNull.Value);
            pMeta.NpgsqlDbType = NpgsqlDbType.Jsonb;

            cmd.Parameters.AddWithValue("occurred_at", evt.OccurredAt);

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AuditEvent>> GetByAggregateAsync(
        string aggregateType,
        Guid aggregateId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aggregateType))
            throw new ArgumentException("Aggregate type cannot be empty.", nameof(aggregateType));
        if (aggregateId == Guid.Empty)
            throw new ArgumentException("Aggregate id cannot be empty.", nameof(aggregateId));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, event_name, aggregate_type, aggregate_id, actor_id, actor_type,
                   reason, correlation_id, causation_id, before_state_json, after_state_json,
                   metadata_json, occurred_at
            FROM audit.audit_events
            WHERE aggregate_type = @aggregate_type AND aggregate_id = @aggregate_id
            ORDER BY occurred_at ASC;
            """;
        cmd.Parameters.AddWithValue("aggregate_type", aggregateType);
        cmd.Parameters.AddWithValue("aggregate_id", aggregateId);

        var list = new List<AuditEvent>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ReadRow(reader));
        }

        return list;
    }

    public async Task<IReadOnlyList<AuditEvent>> GetByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("Correlation id cannot be empty.", nameof(correlationId));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, event_name, aggregate_type, aggregate_id, actor_id, actor_type,
                   reason, correlation_id, causation_id, before_state_json, after_state_json,
                   metadata_json, occurred_at
            FROM audit.audit_events
            WHERE correlation_id = @correlation_id
            ORDER BY occurred_at ASC;
            """;
        cmd.Parameters.AddWithValue("correlation_id", correlationId);

        var list = new List<AuditEvent>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ReadRow(reader));
        }

        return list;
    }

    private static AuditEvent ReadRow(NpgsqlDataReader reader)
    {
        return new AuditEvent(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetGuid(3),
            reader.GetString(5),
            reader.GetString(7),
            reader.IsDBNull(4) ? null : reader.GetGuid(4),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetString(11),
            reader.GetFieldValue<DateTimeOffset>(12));
    }
}
