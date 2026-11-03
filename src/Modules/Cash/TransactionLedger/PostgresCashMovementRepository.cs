using Npgsql;

namespace ALKAROS.Cash.TransactionLedger;

public sealed class PostgresCashMovementRepository : ICashMovementRepository
{
    private const string Columns =
        "request_id, cash_session_id, amount, reason, idempotency_key, status, requested_by, requested_at, " +
        "decided_by, decided_at, decision_note, self_approved, cash_transaction_id";

    private const string KeyLookupSql =
        $"SELECT {Columns} FROM cash.cash_movement_requests WHERE cash_session_id = @session AND idempotency_key = @key;";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresCashMovementRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<CashMovementRequest?> GetRequestBySessionAndKeyAsync(
        Guid cashSessionId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(KeyLookupSql);
        return await ReadByKeyAsync(command, cashSessionId, idempotencyKey, cancellationToken);
    }

    public async Task<CashMovementRequest?> GetRequestBySessionAndKeyAsync(
        Guid cashSessionId, string idempotencyKey, NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = KeyLookupSql;
        return await ReadByKeyAsync(command, cashSessionId, idempotencyKey, cancellationToken);
    }

    private static async Task<CashMovementRequest?> ReadByKeyAsync(
        NpgsqlCommand command, Guid cashSessionId, string idempotencyKey, CancellationToken cancellationToken)
    {
        command.Parameters.AddWithValue("session", cashSessionId);
        command.Parameters.AddWithValue("key", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadRequest(reader) : null;
    }

    public async Task InsertRequestAsync(
        CashMovementRequest request, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO cash.cash_movement_requests (
                request_id, cash_session_id, amount, reason, idempotency_key, status, requested_by, requested_at)
            VALUES (@id, @session, @amount, @reason, @key, @status, @requested_by, @requested_at);
            """;
        command.Parameters.AddWithValue("id", request.Id);
        command.Parameters.AddWithValue("session", request.CashSessionId);
        command.Parameters.AddWithValue("amount", request.Amount);
        command.Parameters.AddWithValue("reason", request.Reason);
        command.Parameters.AddWithValue("key", request.IdempotencyKey);
        command.Parameters.AddWithValue("status", request.Status);
        command.Parameters.AddWithValue("requested_by", request.RequestedBy);
        command.Parameters.AddWithValue("requested_at", request.RequestedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<CashMovementRequest?> GetRequestForUpdateAsync(
        Guid requestId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT {Columns} FROM cash.cash_movement_requests WHERE request_id = @id FOR UPDATE;";
        command.Parameters.AddWithValue("id", requestId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadRequest(reader) : null;
    }

    public async Task MarkDecidedAsync(
        Guid requestId, string status, Guid decidedBy, DateTimeOffset decidedAt, string? decisionNote, bool selfApproved,
        Guid? cashTransactionId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE cash.cash_movement_requests
            SET status = @status, decided_by = @decided_by, decided_at = @decided_at, decision_note = @note,
                self_approved = @self_approved, cash_transaction_id = @transaction_id
            WHERE request_id = @id AND status = 'Requested';
            """;
        command.Parameters.AddWithValue("id", requestId);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("decided_by", decidedBy);
        command.Parameters.AddWithValue("decided_at", decidedAt);
        command.Parameters.AddWithValue("note", (object?)decisionNote ?? DBNull.Value);
        command.Parameters.AddWithValue("self_approved", selfApproved);
        command.Parameters.AddWithValue("transaction_id", (object?)cashTransactionId ?? DBNull.Value);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new CashMovementRequestStateException(requestId, "not open");
    }

    public async Task<IReadOnlyList<PendingCashMovementRequest>> ListPendingAsync(
        Guid cashSessionId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT r.request_id, r.amount, r.reason, r.requested_by, u.display_name, r.requested_at
            FROM cash.cash_movement_requests r
            LEFT JOIN identity.users u ON u.user_id = r.requested_by
            WHERE r.cash_session_id = @session AND r.status = 'Requested'
            ORDER BY r.requested_at
            LIMIT {ICashMovementRepository.MaxListedRequests};
            """);
        command.Parameters.AddWithValue("session", cashSessionId);
        var result = new List<PendingCashMovementRequest>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new PendingCashMovementRequest(
                reader.GetGuid(0), reader.GetDecimal(1), reader.GetString(2), reader.GetGuid(3),
                reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetFieldValue<DateTimeOffset>(5)));
        }

        return result;
    }

    public async Task<bool> HasPendingAsync(Guid cashSessionId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            "SELECT EXISTS (SELECT 1 FROM cash.cash_movement_requests WHERE cash_session_id = @session AND status = 'Requested');");
        command.Parameters.AddWithValue("session", cashSessionId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<IReadOnlyList<CashMovementEntry>> ListMovementsAsync(
        Guid cashSessionId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT t.cash_transaction_id, t.type, t.direction, t.amount, t.notes, t.recorded_by, recorder.display_name,
                   t.occurred_at, r.decided_by, approver.display_name, COALESCE(r.self_approved, FALSE)
            FROM cash.cash_transactions t
            LEFT JOIN cash.cash_movement_requests r ON r.cash_transaction_id = t.cash_transaction_id
            LEFT JOIN identity.users recorder ON recorder.user_id = t.recorded_by
            LEFT JOIN identity.users approver ON approver.user_id = r.decided_by
            WHERE t.cash_session_id = @session AND t.type IN ('CashIn', 'CashOut')
            ORDER BY t.occurred_at DESC, t.cash_transaction_id
            LIMIT {ICashMovementRepository.MaxListedMovements + 1};
            """);
        command.Parameters.AddWithValue("session", cashSessionId);
        var result = new List<CashMovementEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new CashMovementEntry(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetGuid(5), reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetFieldValue<DateTimeOffset>(7),
                reader.IsDBNull(8) ? null : reader.GetGuid(8), reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.GetBoolean(10)));
        }

        return result;
    }

    private static CashMovementRequest ReadRequest(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetGuid(1), reader.GetDecimal(2), reader.GetString(3), reader.GetString(4),
        reader.GetString(5), reader.GetGuid(6), reader.GetFieldValue<DateTimeOffset>(7),
        reader.IsDBNull(8) ? null : reader.GetGuid(8),
        reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9),
        reader.IsDBNull(10) ? null : reader.GetString(10),
        reader.GetBoolean(11),
        reader.IsDBNull(12) ? null : reader.GetGuid(12));
}
