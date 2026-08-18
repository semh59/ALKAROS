using System.Data;
using System.Data.Common;

namespace ALKAROS.Observability.Foundation;

/// <summary>
/// PostgreSQL implementation of <see cref="IHealthCheckRepository"/> (V1-OBS-001, PDF:III.28.1).
/// Enforces approved retention policy validation and sensitive payload redaction before persistence.
/// </summary>
public sealed class PostgresHealthCheckRepository : IHealthCheckRepository
{
    private const string TableName = "observability.health_checks";

    private readonly DbDataSource _dataSource;
    private readonly IRedactionHook _redactionHook;

    public PostgresHealthCheckRepository(
        DbDataSource dataSource,
        IRedactionHook redactionHook)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _redactionHook = redactionHook ?? throw new ArgumentNullException(nameof(redactionHook));
    }

    public async Task<HealthCheckRecord> RecordHealthCheckAsync(
        RecordHealthCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        // 1. Enforce Approved Retention Policy ID (Acceptance Evidence #4)
        if (!RetentionPolicyCatalog.IsApproved(request.RetentionPolicyId))
        {
            throw new UnapprovedRetentionPolicyException(request.RetentionPolicyId);
        }

        // 2. Redact sensitive details before persistence
        var sanitizedDetails = request.DetailsJson is not null
            ? _redactionHook.RedactJson(request.DetailsJson)
            : null;

        var healthCheckId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        const string insertSql = $"""
            INSERT INTO {TableName} (
                health_check_id, check_type, target, status, checked_at, retention_policy_id, details
            ) VALUES (
                @id, @type, @target, @status, @now, @retention, @details::jsonb
            );
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = insertSql;
        AddParameter(cmd, "id", healthCheckId);
        AddParameter(cmd, "type", request.CheckType);
        AddParameter(cmd, "target", request.Target);
        AddParameter(cmd, "status", request.Status.ToString());
        AddParameter(cmd, "now", now);
        AddParameter(cmd, "retention", request.RetentionPolicyId.Trim().ToUpperInvariant());
        AddParameter(cmd, "details", (object?)sanitizedDetails ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken);

        return new HealthCheckRecord(
            healthCheckId,
            request.CheckType,
            request.Target,
            request.Status,
            now,
            request.RetentionPolicyId.Trim().ToUpperInvariant(),
            sanitizedDetails);
    }

    public async Task<HealthCheckRecord?> GetByIdAsync(
        Guid healthCheckId,
        CancellationToken cancellationToken = default)
    {
        if (healthCheckId == Guid.Empty)
            throw new ArgumentException("Health check ID cannot be empty.", nameof(healthCheckId));

        const string sql = $"""
            SELECT health_check_id, check_type, target, status, checked_at, retention_policy_id, details::text
            FROM {TableName}
            WHERE health_check_id = @id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "id", healthCheckId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadRecord(reader);
    }

    public async Task<IReadOnlyList<HealthCheckRecord>> GetLatestByTargetAsync(
        string target,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target))
            throw new ArgumentException("Target cannot be null or whitespace.", nameof(target));

        const string sql = $"""
            SELECT health_check_id, check_type, target, status, checked_at, retention_policy_id, details::text
            FROM {TableName}
            WHERE target = @target
            ORDER BY checked_at DESC
            LIMIT @limit;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "target", target);
        AddParameter(cmd, "limit", limit);

        var list = new List<HealthCheckRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(ReadRecord(reader));
        }

        return list;
    }

    public async Task<IReadOnlyList<HealthCheckRecord>> GetUnhealthyChecksAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT health_check_id, check_type, target, status, checked_at, retention_policy_id, details::text
            FROM {TableName}
            WHERE status != 'Healthy'
            ORDER BY checked_at DESC
            LIMIT 50;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        var list = new List<HealthCheckRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(ReadRecord(reader));
        }

        return list;
    }

    private static HealthCheckRecord ReadRecord(DbDataReader reader)
    {
        return new HealthCheckRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            Enum.Parse<HealthStatus>(reader.GetString(3), ignoreCase: true),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6));
    }

    private static void AddParameter(DbCommand cmd, string name, object? value)
    {
        var param = cmd.CreateParameter();
        param.ParameterName = name;
        param.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(param);
    }
}
