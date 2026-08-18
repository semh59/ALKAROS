using System.Data;
using System.Data.Common;

namespace ALKAROS.Reconciliation.CaseFoundation;

/// <summary>
/// Repository interface for reconciliation cases and append-only action audit trail (V1-REC-001).
/// </summary>
public interface IReconciliationRepository
{
    Task<ReconciliationCaseRecord> CreateOrDeduplicateCaseAsync(
        CreateCaseRequest request,
        CancellationToken cancellationToken = default);

    Task<ReconciliationCaseRecord> TransitionCaseStatusAsync(
        TransitionCaseStatusRequest request,
        CancellationToken cancellationToken = default);

    Task AddCaseNoteAsync(
        AddCaseNoteRequest request,
        CancellationToken cancellationToken = default);

    Task<ReconciliationCaseRecord?> GetCaseByIdAsync(
        Guid caseId,
        CancellationToken cancellationToken = default);

    Task<ReconciliationCaseRecord?> GetActiveCaseByDedupKeyAsync(
        string deduplicationKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CaseActionRecord>> GetCaseActionsAsync(
        Guid caseId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReconciliationCaseRecord>> GetCasesByStatusAsync(
        CaseStatus status,
        int limit = 50,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// PostgreSQL implementation of <see cref="IReconciliationRepository"/> (V1-REC-001).
/// </summary>
public sealed class PostgresReconciliationRepository : IReconciliationRepository
{
    private const string CasesTable = "reconciliation.cases";
    private const string ActionsTable = "reconciliation.case_actions";

    private readonly DbDataSource _dataSource;

    public PostgresReconciliationRepository(DbDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<ReconciliationCaseRecord> CreateOrDeduplicateCaseAsync(
        CreateCaseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        // 1. Check for existing active case with row lock
        const string checkSql = $"""
            SELECT case_id, deduplication_key, case_type, source_a_ref, source_b_ref, discrepancy_amount, severity, status, opened_at, resolved_at, row_version, details::text
            FROM {CasesTable}
            WHERE deduplication_key = @key AND status IN ('Open', 'Investigating', 'Escalated')
            FOR UPDATE;
            """;

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = checkSql;
            AddParameter(cmd, "key", request.DeduplicationKey.Trim());

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var existingCase = ReadCaseRecord(reader);
                await reader.CloseAsync();

                // Record Deduplicated action in audit trail
                await InsertActionInternalAsync(
                    connection,
                    transaction,
                    existingCase.CaseId,
                    ActionType.Deduplicated,
                    request.PerformedBy,
                    request.DetailsJson,
                    cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                return existingCase;
            }
        }

        // 2. Create new case
        var newCaseId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        const string insertSql = $"""
            INSERT INTO {CasesTable} (
                case_id, deduplication_key, case_type, source_a_ref, source_b_ref, discrepancy_amount, severity, status, opened_at, resolved_at, row_version, details
            ) VALUES (
                @id, @key, @type, @srcA, @srcB, @amount, @severity, 'Open', @now, NULL, 1, @details::jsonb
            );
            """;

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = insertSql;
            AddParameter(cmd, "id", newCaseId);
            AddParameter(cmd, "key", request.DeduplicationKey.Trim());
            AddParameter(cmd, "type", request.CaseType.ToString());
            AddParameter(cmd, "srcA", request.SourceARef);
            AddParameter(cmd, "srcB", request.SourceBRef);
            AddParameter(cmd, "amount", request.DiscrepancyAmount);
            AddParameter(cmd, "severity", request.Severity.ToString());
            AddParameter(cmd, "now", now);
            AddParameter(cmd, "details", (object?)request.DetailsJson ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // 3. Record Created action in audit trail
        await InsertActionInternalAsync(
            connection,
            transaction,
            newCaseId,
            ActionType.Created,
            request.PerformedBy,
            request.DetailsJson,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new ReconciliationCaseRecord(
            newCaseId,
            request.DeduplicationKey.Trim(),
            request.CaseType,
            request.SourceARef,
            request.SourceBRef,
            request.DiscrepancyAmount,
            request.Severity,
            CaseStatus.Open,
            now,
            null,
            1,
            request.DetailsJson);
    }

    public async Task<ReconciliationCaseRecord> TransitionCaseStatusAsync(
        TransitionCaseStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        // Fetch current case with lock
        const string selectSql = $"""
            SELECT case_id, deduplication_key, case_type, source_a_ref, source_b_ref, discrepancy_amount, severity, status, opened_at, resolved_at, row_version, details::text
            FROM {CasesTable}
            WHERE case_id = @id
            FOR UPDATE;
            """;

        ReconciliationCaseRecord currentCase;
        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = selectSql;
            AddParameter(cmd, "id", request.CaseId);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new CaseNotFoundException(request.CaseId);

            currentCase = ReadCaseRecord(reader);
        }

        // Validate state transition
        ValidateTransition(currentCase.Status, request.NewStatus);

        // Optimistic concurrency check
        if (currentCase.RowVersion != request.ExpectedVersion)
        {
            throw new ReconciliationConcurrencyException(request.CaseId, request.ExpectedVersion);
        }

        var isTerminal = request.NewStatus is CaseStatus.Resolved or CaseStatus.Dismissed;
        var now = DateTimeOffset.UtcNow;
        DateTimeOffset? resolvedAt = isTerminal ? now : null;

        const string updateSql = $"""
            UPDATE {CasesTable}
            SET status = @newStatus, resolved_at = @resolvedAt, row_version = row_version + 1
            WHERE case_id = @id AND row_version = @expectedVersion;
            """;

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = updateSql;
            AddParameter(cmd, "id", request.CaseId);
            AddParameter(cmd, "newStatus", request.NewStatus.ToString());
            AddParameter(cmd, "resolvedAt", (object?)resolvedAt ?? DBNull.Value);
            AddParameter(cmd, "expectedVersion", request.ExpectedVersion);

            var rowsAffected = await cmd.ExecuteNonQueryAsync(cancellationToken);
            if (rowsAffected == 0)
            {
                throw new ReconciliationConcurrencyException(request.CaseId, request.ExpectedVersion);
            }
        }

        // Record Action
        var actionType = request.NewStatus switch
        {
            CaseStatus.Resolved => ActionType.Resolved,
            CaseStatus.Dismissed => ActionType.Dismissed,
            CaseStatus.Escalated => ActionType.Escalated,
            _ => ActionType.StatusChanged
        };

        var details = !string.IsNullOrWhiteSpace(request.ReasonOrNote)
            ? $"{{\"reason\":\"{request.ReasonOrNote.Replace("\"", "\\\"")}\"}}"
            : null;

        await InsertActionInternalAsync(
            connection,
            transaction,
            request.CaseId,
            actionType,
            request.PerformedBy,
            details,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return currentCase with
        {
            Status = request.NewStatus,
            ResolvedAt = resolvedAt,
            RowVersion = currentCase.RowVersion + 1
        };
    }

    public async Task AddCaseNoteAsync(
        AddCaseNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Note))
            throw new ArgumentException("Note cannot be null or whitespace.", nameof(request));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        // Verify case exists
        var existing = await GetCaseByIdInternalAsync(connection, transaction, request.CaseId, cancellationToken);
        if (existing is null)
            throw new CaseNotFoundException(request.CaseId);

        var details = $"{{\"note\":\"{request.Note.Replace("\"", "\\\"")}\"}}";

        await InsertActionInternalAsync(
            connection,
            transaction,
            request.CaseId,
            ActionType.NoteAdded,
            request.PerformedBy,
            details,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ReconciliationCaseRecord?> GetCaseByIdAsync(
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT case_id, deduplication_key, case_type, source_a_ref, source_b_ref, discrepancy_amount, severity, status, opened_at, resolved_at, row_version, details::text
            FROM {CasesTable}
            WHERE case_id = @id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "id", caseId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadCaseRecord(reader);
    }

    public async Task<ReconciliationCaseRecord?> GetActiveCaseByDedupKeyAsync(
        string deduplicationKey,
        CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT case_id, deduplication_key, case_type, source_a_ref, source_b_ref, discrepancy_amount, severity, status, opened_at, resolved_at, row_version, details::text
            FROM {CasesTable}
            WHERE deduplication_key = @key AND status IN ('Open', 'Investigating', 'Escalated');
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "key", deduplicationKey.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadCaseRecord(reader);
    }

    public async Task<IReadOnlyList<CaseActionRecord>> GetCaseActionsAsync(
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT action_id, case_id, action_type, performed_by, performed_at, details::text
            FROM {ActionsTable}
            WHERE case_id = @caseId
            ORDER BY performed_at ASC;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "caseId", caseId);

        var list = new List<CaseActionRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new CaseActionRecord(
                reader.GetGuid(0),
                reader.GetGuid(1),
                Enum.Parse<ActionType>(reader.GetString(2), ignoreCase: true),
                reader.GetGuid(3),
                reader.GetFieldValue<DateTimeOffset>(4),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return list;
    }

    public async Task<IReadOnlyList<ReconciliationCaseRecord>> GetCasesByStatusAsync(
        CaseStatus status,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT case_id, deduplication_key, case_type, source_a_ref, source_b_ref, discrepancy_amount, severity, status, opened_at, resolved_at, row_version, details::text
            FROM {CasesTable}
            WHERE status = @status
            ORDER BY opened_at DESC
            LIMIT @limit;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        AddParameter(cmd, "status", status.ToString());
        AddParameter(cmd, "limit", limit);

        var list = new List<ReconciliationCaseRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(ReadCaseRecord(reader));
        }

        return list;
    }

    private static void ValidateTransition(CaseStatus from, CaseStatus to)
    {
        if (from == to) return;

        // Terminal states cannot transition to anything
        if (from is CaseStatus.Resolved or CaseStatus.Dismissed)
        {
            throw new InvalidCaseStatusTransitionException(from, to);
        }

        var valid = (from, to) switch
        {
            (CaseStatus.Open, CaseStatus.Investigating) => true,
            (CaseStatus.Open, CaseStatus.Resolved) => true,
            (CaseStatus.Open, CaseStatus.Dismissed) => true,
            (CaseStatus.Open, CaseStatus.Escalated) => true,

            (CaseStatus.Investigating, CaseStatus.Resolved) => true,
            (CaseStatus.Investigating, CaseStatus.Dismissed) => true,
            (CaseStatus.Investigating, CaseStatus.Escalated) => true,

            (CaseStatus.Escalated, CaseStatus.Investigating) => true,
            (CaseStatus.Escalated, CaseStatus.Resolved) => true,
            (CaseStatus.Escalated, CaseStatus.Dismissed) => true,

            _ => false
        };

        if (!valid)
        {
            throw new InvalidCaseStatusTransitionException(from, to);
        }
    }

    private static async Task InsertActionInternalAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid caseId,
        ActionType actionType,
        Guid performedBy,
        string? detailsJson,
        CancellationToken cancellationToken)
    {
        const string sql = $"""
            INSERT INTO {ActionsTable} (
                action_id, case_id, action_type, performed_by, performed_at, details
            ) VALUES (
                @id, @caseId, @actionType, @by, @at, @details::jsonb
            );
            """;

        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = sql;
        AddParameter(cmd, "id", Guid.NewGuid());
        AddParameter(cmd, "caseId", caseId);
        AddParameter(cmd, "actionType", actionType.ToString());
        AddParameter(cmd, "by", performedBy);
        AddParameter(cmd, "at", DateTimeOffset.UtcNow);
        AddParameter(cmd, "details", (object?)detailsJson ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<ReconciliationCaseRecord?> GetCaseByIdInternalAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        const string sql = $"""
            SELECT case_id, deduplication_key, case_type, source_a_ref, source_b_ref, discrepancy_amount, severity, status, opened_at, resolved_at, row_version, details::text
            FROM {CasesTable}
            WHERE case_id = @id;
            """;

        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = sql;
        AddParameter(cmd, "id", caseId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadCaseRecord(reader);
    }

    private static ReconciliationCaseRecord ReadCaseRecord(DbDataReader reader)
    {
        return new ReconciliationCaseRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            Enum.Parse<CaseType>(reader.GetString(2), ignoreCase: true),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetDecimal(5),
            Enum.Parse<CaseSeverity>(reader.GetString(6), ignoreCase: true),
            Enum.Parse<CaseStatus>(reader.GetString(7), ignoreCase: true),
            reader.GetFieldValue<DateTimeOffset>(8),
            reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9),
            reader.GetInt32(10),
            reader.IsDBNull(11) ? null : reader.GetString(11));
    }

    private static void AddParameter(DbCommand cmd, string name, object? value)
    {
        var param = cmd.CreateParameter();
        param.ParameterName = name;
        param.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(param);
    }
}
