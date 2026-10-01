using ALKAROS.Cash.Contracts;
using ALKAROS.Cash.TransactionLedger;
using Npgsql;

namespace ALKAROS.Cash.SessionLifecycle;

public sealed class PostgresCashSessionRepository : ICashSessionRepository
{
    private const string Sessions = "cash.cash_sessions";
    private const string Counts = "cash.cash_counts";
    private const int MaxUnpagedRows = 5000;
    private static readonly TimeSpan CountRetryWindow = TimeSpan.FromSeconds(30);

    private readonly NpgsqlDataSource _dataSource;

    public PostgresCashSessionRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<CashSessionRecord?> GetByIdAsync(Guid cashSessionId, CancellationToken cancellationToken = default)
    {
        if (cashSessionId == Guid.Empty)
            throw new ArgumentException("Cash session id cannot be empty.", nameof(cashSessionId));

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT cash_session_id, cashier_user_id, terminal_id, status,
                   opening_balance, expected_cash, actual_cash, difference,
                   opened_at, closed_at, closed_by, is_supervisor_override,
                   override_reason, reconciled_at, reconciled_by, reconciliation_notes,
                   row_version
            FROM {Sessions}
            WHERE cash_session_id = @id;
            """);
        command.Parameters.AddWithValue("id", cashSessionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadRecord(reader);
    }

    public async Task<IReadOnlyList<CashSessionSnapshot>> GetByTerminalIdAsync(Guid terminalId, CancellationToken cancellationToken = default)
    {
        if (terminalId == Guid.Empty)
            throw new ArgumentException("Terminal id cannot be empty.", nameof(terminalId));

        var result = new List<CashSessionSnapshot>();
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT cash_session_id, cashier_user_id, terminal_id, status,
                   opening_balance, expected_cash, actual_cash, difference,
                   opened_at, closed_at, row_version
            FROM {Sessions}
            WHERE terminal_id = @terminal_id
            ORDER BY opened_at
            LIMIT {MaxUnpagedRows + 1};
            """);
        command.Parameters.AddWithValue("terminal_id", terminalId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadSnapshot(reader));

        if (result.Count > MaxUnpagedRows)
            throw new InvalidOperationException(
                $"GetByTerminalIdAsync returned more than {MaxUnpagedRows} rows; narrow the filter or paginate.");

        return result;
    }

    public async Task<decimal?> GetSuggestedOpeningBalanceAsync(Guid terminalId, CancellationToken cancellationToken = default)
    {
        if (terminalId == Guid.Empty)
            throw new ArgumentException("Terminal id cannot be empty.", nameof(terminalId));

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT actual_cash
            FROM {Sessions}
            WHERE terminal_id = @terminal_id
              AND status IN ('Closed', 'Reconciled')
            ORDER BY closed_at DESC NULLS LAST
            LIMIT 1;
            """);
        command.Parameters.AddWithValue("terminal_id", terminalId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : (decimal)result;
    }

    public async Task AddAsync(CashSessionRecord session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await InsertSessionAsync(connection, null, session, cancellationToken);
    }

    public async Task AddAsync(CashSessionRecord session, CashTransaction openingEntry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(openingEntry);
        if (openingEntry.Type != CashTransactionType.Opening || openingEntry.CashSessionId != session.Snapshot.CashSessionId)
            throw new ArgumentException("The opening entry must be this session's Opening ledger entry.", nameof(openingEntry));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await InsertSessionAsync(connection, transaction, session, cancellationToken);
        await new PostgresCashTransactionLedgerRepository(_dataSource)
            .RecordAsync(openingEntry, connection, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task InsertSessionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CashSessionRecord session,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            $"""
            INSERT INTO {Sessions} (
                cash_session_id, cashier_user_id, terminal_id, status,
                opening_balance, expected_cash, actual_cash, difference,
                opened_at, closed_at, closed_by, is_supervisor_override,
                override_reason, reconciled_at, reconciled_by, reconciliation_notes,
                row_version, created_at, updated_at)
            VALUES (
                @cash_session_id, @cashier_user_id, @terminal_id, @status,
                @opening_balance, @expected_cash, @actual_cash, @difference,
                @opened_at, @closed_at, @closed_by, @is_supervisor_override,
                @override_reason, @reconciled_at, @reconciled_by, @reconciliation_notes,
                @row_version, @created_at, @updated_at);
            """, connection, transaction);
        BindRecord(command, session);
        command.Parameters.AddWithValue("created_at", session.Snapshot.OpenedAt);
        command.Parameters.AddWithValue("updated_at", session.Snapshot.OpenedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<long> SaveAsync(CashSessionRecord session, long expectedRowVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        await using var command = _dataSource.CreateCommand(
            $"""
            UPDATE {Sessions}
            SET status = @status,
                expected_cash = @expected_cash,
                actual_cash = @actual_cash,
                difference = @difference,
                closed_at = @closed_at,
                closed_by = @closed_by,
                is_supervisor_override = @is_supervisor_override,
                override_reason = @override_reason,
                reconciled_at = @reconciled_at,
                reconciled_by = @reconciled_by,
                reconciliation_notes = @reconciliation_notes,
                updated_at = @updated_at,
                row_version = row_version + 1
            WHERE cash_session_id = @cash_session_id AND row_version = @expected_row_version
            RETURNING row_version;
            """);
        BindRecord(command, session);
        command.Parameters.AddWithValue("updated_at", DateTimeOffset.UtcNow);
        command.Parameters.AddWithValue("expected_row_version", expectedRowVersion);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null)
            throw new InvalidOperationException(
                $"Cash session {session.Snapshot.CashSessionId} not found or concurrent modification " +
                $"(expected row version {expectedRowVersion}).");

        return (long)result;
    }

    public async Task<Guid> RecordCountAsync(
        Guid cashSessionId,
        decimal countedAmount,
        Guid countedBy,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        if (cashSessionId == Guid.Empty)
            throw new ArgumentException("Cash session id cannot be empty.", nameof(cashSessionId));

        // A retried request (same session, counter, amount and notes within the retry window) finds the row the first
        // attempt wrote instead of adding a second identical one.
        await using var command = _dataSource.CreateCommand(
            $"""
            WITH existing AS (
                SELECT cash_count_id FROM {Counts}
                WHERE cash_session_id = @cash_session_id AND counted_by = @counted_by AND counted_amount = @counted_amount
                  AND notes IS NOT DISTINCT FROM @notes::text AND counted_at > @retry_window_start
                ORDER BY counted_at DESC LIMIT 1),
            inserted AS (
                INSERT INTO {Counts} (cash_count_id, cash_session_id, counted_amount, counted_by, notes, counted_at)
                SELECT @cash_count_id, @cash_session_id, @counted_amount, @counted_by, @notes::text, @counted_at
                WHERE NOT EXISTS (SELECT 1 FROM existing)
                RETURNING cash_count_id)
            SELECT cash_count_id FROM inserted UNION ALL SELECT cash_count_id FROM existing LIMIT 1;
            """);
        var now = DateTimeOffset.UtcNow;
        command.Parameters.AddWithValue("cash_count_id", Guid.NewGuid());
        command.Parameters.AddWithValue("cash_session_id", cashSessionId);
        command.Parameters.AddWithValue("counted_amount", countedAmount);
        command.Parameters.AddWithValue("counted_by", countedBy);
        command.Parameters.AddWithValue("notes", (object?)notes ?? DBNull.Value);
        command.Parameters.AddWithValue("counted_at", now);
        command.Parameters.AddWithValue("retry_window_start", now - CountRetryWindow);
        return (Guid)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<IReadOnlyList<CashCountEntry>> GetCountsAsync(Guid cashSessionId, CancellationToken cancellationToken = default)
    {
        if (cashSessionId == Guid.Empty)
            throw new ArgumentException("Cash session id cannot be empty.", nameof(cashSessionId));

        var result = new List<CashCountEntry>();
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT cash_count_id, cash_session_id, counted_amount, counted_by, notes, counted_at
            FROM {Counts}
            WHERE cash_session_id = @cash_session_id
            ORDER BY counted_at
            LIMIT {MaxUnpagedRows + 1};
            """);
        command.Parameters.AddWithValue("cash_session_id", cashSessionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new CashCountEntry(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetDecimal(2),
                reader.GetGuid(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetDateTime(5)));
        }

        if (result.Count > MaxUnpagedRows)
            throw new InvalidOperationException(
                $"GetCountsAsync returned more than {MaxUnpagedRows} rows; narrow the filter or paginate.");

        return result;
    }

    private static void BindRecord(NpgsqlCommand command, CashSessionRecord record)
    {
        var snapshot = record.Snapshot;
        command.Parameters.AddWithValue("cash_session_id", snapshot.CashSessionId);
        command.Parameters.AddWithValue("cashier_user_id", snapshot.CashierUserId);
        command.Parameters.AddWithValue("terminal_id", snapshot.TerminalId);
        command.Parameters.AddWithValue("status", snapshot.Status.ToString());
        command.Parameters.AddWithValue("opening_balance", snapshot.OpeningBalance);
        command.Parameters.AddWithValue("expected_cash", snapshot.ExpectedCash);
        command.Parameters.AddWithValue("actual_cash", snapshot.ActualCash);
        command.Parameters.AddWithValue("difference", snapshot.Difference);
        command.Parameters.AddWithValue("opened_at", snapshot.OpenedAt);
        command.Parameters.AddWithValue("closed_at", (object?)snapshot.ClosedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("closed_by", (object?)record.ClosedBy ?? DBNull.Value);
        command.Parameters.AddWithValue("is_supervisor_override", record.IsSupervisorOverride);
        command.Parameters.AddWithValue("override_reason", (object?)record.OverrideReason ?? DBNull.Value);
        command.Parameters.AddWithValue("reconciled_at", (object?)record.ReconciledAt ?? DBNull.Value);
        command.Parameters.AddWithValue("reconciled_by", (object?)record.ReconciledBy ?? DBNull.Value);
        command.Parameters.AddWithValue("reconciliation_notes", (object?)record.ReconciliationNotes ?? DBNull.Value);
        command.Parameters.AddWithValue("row_version", snapshot.RowVersion);
    }

    private static CashSessionSnapshot ReadSnapshot(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetGuid(1),
        reader.GetGuid(2),
        Enum.Parse<CashSessionStatus>(reader.GetString(3)),
        reader.GetDecimal(4),
        reader.GetDecimal(5),
        reader.GetDecimal(6),
        reader.GetDecimal(7),
        reader.GetDateTime(8),
        reader.IsDBNull(9) ? null : reader.GetDateTime(9),
        reader.GetInt64(10));

    private static CashSessionRecord ReadRecord(NpgsqlDataReader reader)
    {
        var snapshot = new CashSessionSnapshot(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            Enum.Parse<CashSessionStatus>(reader.GetString(3)),
            reader.GetDecimal(4),
            reader.GetDecimal(5),
            reader.GetDecimal(6),
            reader.GetDecimal(7),
            reader.GetDateTime(8),
            reader.IsDBNull(9) ? null : reader.GetDateTime(9),
            reader.GetInt64(16));

        return new CashSessionRecord(
            snapshot,
            reader.IsDBNull(10) ? null : reader.GetGuid(10),
            reader.GetBoolean(11),
            reader.IsDBNull(12) ? null : reader.GetString(12),
            reader.IsDBNull(14) ? null : reader.GetGuid(14),
            reader.IsDBNull(15) ? null : reader.GetString(15),
            reader.IsDBNull(13) ? null : reader.GetDateTime(13));
    }
}
