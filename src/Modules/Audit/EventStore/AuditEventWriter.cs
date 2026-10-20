namespace ALKAROS.Audit.EventStore;

using Npgsql;
using NpgsqlTypes;

/// <summary>The single place that inserts a row into <c>audit.audit_events</c>.</summary>
public static class AuditEventWriter
{
    private static readonly IAuditSanitizer DefaultSanitizer = new AuditSanitizer();

    /// <summary>
    /// Validates, sanitizes and inserts one audit row on the caller's connection (and transaction, when given). Every audit
    /// write goes through here, so no row skips the sanitizer or the validation.
    /// </summary>
    public static async Task WriteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        AuditEvent evt,
        IAuditSanitizer? sanitizer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(evt);
        evt.Validate();
        sanitizer ??= DefaultSanitizer;

        var sanitizedBefore = sanitizer.SanitizeJson(evt.BeforeStateJson);
        var sanitizedAfter = sanitizer.SanitizeJson(evt.AfterStateJson);
        var sanitizedMetadata = sanitizer.SanitizeJson(evt.MetadataJson);

        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
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
}
