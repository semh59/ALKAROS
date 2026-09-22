using System.Data;
using System.Data.Common;
using ALKAROS.Operations.OffsiteBackup;

namespace ALKAROS.Operations.RestoreVerification;

/// <summary>
/// PostgreSQL-backed <see cref="IRestoreAttemptStore"/>
/// (<c>operations.restore_attempts</c>, migration 139).
/// </summary>
public sealed class PostgresRestoreAttemptStore : IRestoreAttemptStore
{
    private const string Table = "operations.restore_attempts";

    private readonly DbDataSource _dataSource;

    public PostgresRestoreAttemptStore(DbDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task RecordAsync(RestoreAttemptRecord attempt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        const string sql = $"""
            INSERT INTO {Table} (
                artifact_id, data_class, started_at, duration_ms, succeeded, within_rto_target,
                integrity_checks_passed, integrity_checks_total, failure_reason
            ) VALUES (
                @artifactId, @dataClass, @startedAt, @durationMs, @succeeded, @withinRto,
                @checksPassed, @checksTotal, @failureReason
            );
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "artifactId", attempt.ArtifactId);
        AddParameter(cmd, "dataClass", attempt.DataClass.ToString());
        AddParameter(cmd, "startedAt", attempt.StartedAtUtc);
        AddParameter(cmd, "durationMs", (long)attempt.Duration.TotalMilliseconds);
        AddParameter(cmd, "succeeded", attempt.Succeeded);
        AddParameter(cmd, "withinRto", attempt.WithinRtoTarget);
        AddParameter(cmd, "checksPassed", attempt.IntegrityChecksPassed);
        AddParameter(cmd, "checksTotal", attempt.IntegrityChecksTotal);
        AddParameter(cmd, "failureReason", attempt.FailureReason);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RestoreAttemptRecord>> GetByDataClassAsync(DataClass dataClass, int limit = 500, CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT artifact_id, data_class, started_at, duration_ms, succeeded, within_rto_target,
                   integrity_checks_passed, integrity_checks_total, failure_reason
            FROM {Table}
            WHERE data_class = @dataClass
            ORDER BY started_at DESC
            LIMIT @limit;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "dataClass", dataClass.ToString());
        AddParameter(cmd, "limit", limit);

        var results = new List<RestoreAttemptRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            results.Add(ReadAttempt(reader));

        return results;
    }

    private static RestoreAttemptRecord ReadAttempt(DbDataReader reader) => new(
        reader.GetString(0),
        Enum.Parse<DataClass>(reader.GetString(1), ignoreCase: true),
        reader.GetFieldValue<DateTimeOffset>(2),
        TimeSpan.FromMilliseconds(reader.GetInt64(3)),
        reader.GetBoolean(4),
        reader.GetBoolean(5),
        reader.GetInt32(6),
        reader.GetInt32(7),
        reader.IsDBNull(8) ? null : reader.GetString(8));

    private static void AddParameter(DbCommand cmd, string name, object? value)
    {
        var param = cmd.CreateParameter();
        param.ParameterName = name;
        param.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(param);
    }
}
