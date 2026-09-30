using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Privacy.RetentionExecution;

public sealed class PostgresRetentionExecutionService(NpgsqlDataSource dataSource) : IRetentionExecutionService
{
    private const string UniqueViolation = "23505";
    private const string IntegrityViolation = "23000";
    private const long RunLockKey = 7_301_500_001L;

    private static readonly RetentionClass[] Classes = Enum.GetValues<RetentionClass>();

    public async Task<RetentionPlan> PlanAsync(
        DateTimeOffset asOf, IReadOnlySet<Guid>? extraHeld = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        return await SelectAsync(connection, transaction, asOf, extraHeld, cancellationToken);
    }

    public async Task<RetentionRunResult> ExecuteAsync(
        DateTimeOffset asOf, string requestedBy, IReadOnlySet<Guid>? extraHeld = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedBy);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SerializeAsync(connection, transaction, cancellationToken);

        var plan = await SelectAsync(connection, transaction, asOf, extraHeld, cancellationToken);
        var runId = Guid.NewGuid();
        await using (var run = new NpgsqlCommand(
            """
            INSERT INTO privacy.retention_runs (run_id, policy_version, as_of, requested_by, item_count)
            VALUES (@run_id, @policy_version, @as_of, @requested_by, @item_count);
            """,
            connection,
            transaction))
        {
            run.Parameters.Add("run_id", NpgsqlDbType.Uuid).Value = runId;
            run.Parameters.Add("policy_version", NpgsqlDbType.Integer).Value = plan.PolicyVersion;
            run.Parameters.Add("as_of", NpgsqlDbType.TimestampTz).Value = asOf;
            run.Parameters.Add("requested_by", NpgsqlDbType.Text).Value = requestedBy;
            run.Parameters.Add("item_count", NpgsqlDbType.Integer).Value = plan.Candidates.Count;
            await run.ExecuteNonQueryAsync(cancellationToken);
        }

        if (plan.Candidates.Count > 0)
        {
            await using var items = new NpgsqlCommand(
                """
                INSERT INTO privacy.retention_run_items (run_id, data_class, subject_id, due_since)
                SELECT @run_id, i.data_class, i.subject_id, i.due_since
                FROM unnest(@classes, @subjects, @dues) AS i (data_class, subject_id, due_since);

                INSERT INTO privacy.retention_work_items (data_class, subject_id, run_id, policy_version, due_since)
                SELECT i.data_class, i.subject_id, @run_id, @policy_version, i.due_since
                FROM unnest(@classes, @subjects, @dues) AS i (data_class, subject_id, due_since);
                """,
                connection,
                transaction);
            items.Parameters.Add("run_id", NpgsqlDbType.Uuid).Value = runId;
            items.Parameters.Add("policy_version", NpgsqlDbType.Integer).Value = plan.PolicyVersion;
            items.Parameters.Add("classes", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
                plan.Candidates.Select(candidate => candidate.DataClass.ToString()).ToArray();
            items.Parameters.Add("subjects", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value =
                plan.Candidates.Select(candidate => candidate.SubjectId).ToArray();
            items.Parameters.Add("dues", NpgsqlDbType.Array | NpgsqlDbType.TimestampTz).Value =
                plan.Candidates.Select(candidate => candidate.DueSince).ToArray();
            await items.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new RetentionRunResult(runId, plan);
    }

    public async Task<int> PublishPolicyAsync(
        IReadOnlyDictionary<RetentionClass, int> years, string note, Guid publishedBy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(years);
        ArgumentException.ThrowIfNullOrWhiteSpace(note);
        var missing = Classes.Where(dataClass => !years.ContainsKey(dataClass)).ToArray();
        if (missing.Length > 0)
            throw new RetentionPolicyIncompleteException(missing);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SerializeAsync(connection, transaction, cancellationToken);

        int version;
        await using (var next = new NpgsqlCommand(
            "SELECT COALESCE(max(policy_version), 0) + 1 FROM privacy.retention_policies;", connection, transaction))
        {
            version = (int)(await next.ExecuteScalarAsync(cancellationToken))!;
        }

        await using (var policy = new NpgsqlCommand(
            "INSERT INTO privacy.retention_policies (policy_version, published_by, note) VALUES (@version, @by, @note);",
            connection,
            transaction))
        {
            policy.Parameters.Add("version", NpgsqlDbType.Integer).Value = version;
            policy.Parameters.Add("by", NpgsqlDbType.Uuid).Value = publishedBy;
            policy.Parameters.Add("note", NpgsqlDbType.Text).Value = note;
            await policy.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var rules = new NpgsqlCommand(
            """
            INSERT INTO privacy.retention_rules (policy_version, data_class, retention_years)
            SELECT @version, r.data_class, r.years FROM unnest(@classes, @years) AS r (data_class, years);
            """,
            connection,
            transaction))
        {
            rules.Parameters.Add("version", NpgsqlDbType.Integer).Value = version;
            rules.Parameters.Add("classes", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
                Classes.Select(dataClass => dataClass.ToString()).ToArray();
            rules.Parameters.Add("years", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value =
                Classes.Select(dataClass => years[dataClass]).ToArray();
            await rules.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return version;
    }

    public async Task<Guid> PlaceHoldAsync(
        RetentionClass dataClass, Guid subjectId, string reason, Guid placedBy, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var holdId = Guid.NewGuid();
        await using var command = dataSource.CreateCommand(
            """
            INSERT INTO privacy.legal_holds (hold_id, data_class, subject_id, reason, placed_by)
            VALUES (@hold_id, @class, @subject_id, @reason, @placed_by);
            """);
        command.Parameters.Add("hold_id", NpgsqlDbType.Uuid).Value = holdId;
        command.Parameters.Add("class", NpgsqlDbType.Text).Value = dataClass.ToString();
        command.Parameters.Add("subject_id", NpgsqlDbType.Uuid).Value = subjectId;
        command.Parameters.Add("reason", NpgsqlDbType.Text).Value = reason;
        command.Parameters.Add("placed_by", NpgsqlDbType.Uuid).Value = placedBy;
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == UniqueViolation)
        {
            throw new RetentionHoldAlreadyActiveException(dataClass, subjectId);
        }

        return holdId;
    }

    public async Task ReleaseHoldAsync(Guid holdId, Guid releasedBy, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            """
            UPDATE privacy.legal_holds SET released_by = @released_by, released_at = now()
            WHERE hold_id = @hold_id AND released_at IS NULL;
            """);
        command.Parameters.Add("hold_id", NpgsqlDbType.Uuid).Value = holdId;
        command.Parameters.Add("released_by", NpgsqlDbType.Uuid).Value = releasedBy;
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            throw new RetentionHoldNotFoundException(holdId);
    }

    public async Task<IReadOnlyList<RetentionWorkItem>> PendingAsync(
        RetentionClass dataClass, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        await using var command = dataSource.CreateCommand(
            """
            SELECT w.subject_id, w.due_since, w.policy_version
            FROM privacy.retention_work_items w
            WHERE w.data_class = @class AND w.status = 'Pending'
              AND NOT EXISTS (SELECT 1 FROM privacy.legal_holds h
                              WHERE h.data_class = w.data_class AND h.subject_id = w.subject_id AND h.released_at IS NULL)
            ORDER BY w.due_since, w.subject_id
            LIMIT @limit;
            """);
        command.Parameters.Add("class", NpgsqlDbType.Text).Value = dataClass.ToString();
        command.Parameters.Add("limit", NpgsqlDbType.Integer).Value = limit;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<RetentionWorkItem>();
        while (await reader.ReadAsync(cancellationToken))
            items.Add(new RetentionWorkItem(dataClass, reader.GetGuid(0), reader.GetFieldValue<DateTimeOffset>(1), reader.GetInt32(2)));
        return items;
    }

    public async Task<int> CompleteAsync(
        RetentionClass dataClass,
        IReadOnlyCollection<Guid> subjectIds,
        string completedBy,
        NpgsqlTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subjectIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(completedBy);
        if (subjectIds.Count == 0)
            return 0;

        await using var owned = transaction is null ? await dataSource.OpenConnectionAsync(cancellationToken) : null;
        await using var command = new NpgsqlCommand(
            """
            UPDATE privacy.retention_work_items SET status = 'Done', completed_at = now(), completed_by = @completed_by
            WHERE data_class = @class AND status = 'Pending' AND subject_id = ANY(@subjects);
            """,
            transaction?.Connection ?? owned,
            transaction);
        command.Parameters.Add("completed_by", NpgsqlDbType.Text).Value = completedBy;
        command.Parameters.Add("class", NpgsqlDbType.Text).Value = dataClass.ToString();
        command.Parameters.Add("subjects", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = subjectIds.ToArray();
        try
        {
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == IntegrityViolation)
        {
            throw new RetentionSubjectHeldException(dataClass, subjectIds.First());
        }
    }

    private static async Task SerializeAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@key);", connection, transaction);
        command.Parameters.Add("key", NpgsqlDbType.Bigint).Value = RunLockKey;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<RetentionPlan> SelectAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateTimeOffset asOf,
        IReadOnlySet<Guid>? extraHeld,
        CancellationToken cancellationToken)
    {
        var version = 0;
        var years = new Dictionary<RetentionClass, int>();
        await using (var policy = new NpgsqlCommand(
            """
            SELECT r.policy_version, r.data_class, r.retention_years
            FROM privacy.retention_rules r
            WHERE r.policy_version = (SELECT max(policy_version) FROM privacy.retention_policies);
            """,
            connection,
            transaction))
        await using (var reader = await policy.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                version = reader.GetInt32(0);
                years[Enum.Parse<RetentionClass>(reader.GetString(1))] = reader.GetInt32(2);
            }
        }

        var missing = Classes.Where(dataClass => !years.ContainsKey(dataClass)).ToArray();
        if (missing.Length > 0)
            throw new RetentionPolicyIncompleteException(missing);

        var held = (extraHeld ?? new HashSet<Guid>()).ToArray();
        var candidates = new List<RetentionCandidate>();
        foreach (var dataClass in Classes)
        {
            await using var command = new NpgsqlCommand(RetentionSelection.Due(dataClass), connection, transaction);
            command.Parameters.Add("marker", NpgsqlDbType.Text).Value = RetentionSelection.AnonymizedMarker;
            command.Parameters.Add("cut", NpgsqlDbType.TimestampTz).Value = asOf.AddYears(-years[dataClass]);
            command.Parameters.Add("class", NpgsqlDbType.Text).Value = dataClass.ToString();
            command.Parameters.Add("extra_held", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = held;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                candidates.Add(new RetentionCandidate(
                    dataClass, reader.GetGuid(0), reader.GetFieldValue<DateTimeOffset>(1).AddYears(years[dataClass])));
            }
        }

        return new RetentionPlan(version, asOf, candidates);
    }
}
