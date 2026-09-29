using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.CustomerAccounts.AccountPayments;

/// <summary>Postgres-backed <see cref="IAccountPaymentRepository"/> (migration 165).</summary>
public sealed class PostgresAccountPaymentRepository : IAccountPaymentRepository
{
    private const string Columns =
        "account_payment_id, customer_id, method, amount, currency_code, status, idempotency_key, " +
        "evidence_type, evidence_reference, requested_by, requested_at, updated_at, row_version";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresAccountPaymentRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<AccountPayment> RequestAsync(AccountPayment payment, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var stored = await RequestAsync(payment, connection, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return stored;
    }

    public async Task<AccountPayment> RequestAsync(
        AccountPayment payment, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);
        if (payment.Status != AccountPaymentStatus.Requested)
            throw new InvalidAccountPaymentTransitionException("A new account payment starts as Requested.");

        await using (var insert = new NpgsqlCommand(
            $"""
            INSERT INTO customer_account.account_payments ({Columns})
            VALUES (@id, @customer_id, @method, @amount, @currency_code, 'Requested', @idempotency_key,
                    NULL, NULL, @requested_by, @requested_at, @requested_at, 1)
            ON CONFLICT (idempotency_key) DO NOTHING;
            """, connection, transaction))
        {
            insert.Parameters.Add("id", NpgsqlDbType.Uuid).Value = payment.Id;
            insert.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = payment.CustomerId;
            insert.Parameters.Add("method", NpgsqlDbType.Text).Value = payment.Method.ToString();
            insert.Parameters.Add("amount", NpgsqlDbType.Numeric).Value = payment.Amount;
            insert.Parameters.Add("currency_code", NpgsqlDbType.Char).Value = payment.CurrencyCode;
            insert.Parameters.Add("idempotency_key", NpgsqlDbType.Text).Value = payment.IdempotencyKey;
            insert.Parameters.Add("requested_by", NpgsqlDbType.Uuid).Value = (object?)payment.RequestedBy ?? DBNull.Value;
            insert.Parameters.Add("requested_at", NpgsqlDbType.TimestampTz).Value = payment.RequestedAt.UtcDateTime;
            if (await insert.ExecuteNonQueryAsync(cancellationToken) == 1)
            {
                await AppendHistoryAsync(connection, transaction, payment.Id, null, AccountPaymentStatus.Requested,
                    null, null, payment.RequestedBy, payment.RequestedAt, cancellationToken);
                return payment;
            }
        }

        var existing = await ReadOneAsync(connection, transaction, "idempotency_key = @key",
            command => command.Parameters.Add("key", NpgsqlDbType.Text).Value = payment.IdempotencyKey, cancellationToken)
            ?? throw new InvalidOperationException($"Idempotency key '{payment.IdempotencyKey}' conflicted but no payment holds it.");
        if (existing.CustomerId != payment.CustomerId || existing.Method != payment.Method || existing.Amount != payment.Amount)
            throw new AccountPaymentIdempotencyKeyReusedException(payment.IdempotencyKey);
        return existing;
    }

    public async Task<AccountPayment> SaveTransitionAsync(
        AccountPayment updated, long expectedRowVersion, string? reason, Guid? changedBy, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var saved = await SaveTransitionAsync(updated, expectedRowVersion, reason, changedBy, connection, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return saved;
    }

    public async Task<AccountPayment> SaveTransitionAsync(
        AccountPayment updated, long expectedRowVersion, string? reason, Guid? changedBy,
        NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updated);

        var current = await ReadOneAsync(connection, transaction, "account_payment_id = @id FOR UPDATE",
            command => command.Parameters.Add("id", NpgsqlDbType.Uuid).Value = updated.Id, cancellationToken)
            ?? throw new AccountPaymentNotFoundException(updated.Id);
        if (current.RowVersion != expectedRowVersion)
            throw new AccountPaymentConcurrencyException(updated.Id);
        if (!AccountPayment.CanTransition(current.Status, updated.Status))
            throw new InvalidAccountPaymentTransitionException($"Account payment {updated.Id} cannot move from {current.Status} to {updated.Status}.");

        await using (var update = new NpgsqlCommand(
            """
            UPDATE customer_account.account_payments
            SET status = @status,
                evidence_type = @evidence_type,
                evidence_reference = @evidence_reference,
                updated_at = @updated_at,
                row_version = row_version + 1
            WHERE account_payment_id = @id AND row_version = @expected_row_version;
            """, connection, transaction))
        {
            update.Parameters.Add("id", NpgsqlDbType.Uuid).Value = updated.Id;
            update.Parameters.Add("status", NpgsqlDbType.Text).Value = updated.Status.ToString();
            update.Parameters.Add("evidence_type", NpgsqlDbType.Text).Value = (object?)updated.Evidence?.Type.ToString() ?? DBNull.Value;
            update.Parameters.Add("evidence_reference", NpgsqlDbType.Text).Value = (object?)updated.Evidence?.Reference ?? DBNull.Value;
            update.Parameters.Add("updated_at", NpgsqlDbType.TimestampTz).Value = updated.UpdatedAt.UtcDateTime;
            update.Parameters.Add("expected_row_version", NpgsqlDbType.Bigint).Value = expectedRowVersion;
            try
            {
                if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
                    throw new AccountPaymentConcurrencyException(updated.Id);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation
                && ex.ConstraintName == "ux_account_payments_evidence")
            {
                throw new AccountPaymentEvidenceAlreadyLinkedException(updated.Evidence!);
            }
        }

        await AppendHistoryAsync(connection, transaction, updated.Id, current.Status, updated.Status,
            updated.Evidence?.Reference, reason, changedBy, updated.UpdatedAt, cancellationToken);

        return new AccountPayment(updated.Id, updated.CustomerId, updated.Method, updated.Amount, updated.IdempotencyKey,
            updated.RequestedAt, updated.CurrencyCode, updated.Status, updated.Evidence, updated.RequestedBy,
            updated.UpdatedAt, expectedRowVersion + 1);
    }

    public async Task<AccountPayment?> GetAsync(Guid accountPaymentId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadOneAsync(connection, null, "account_payment_id = @id",
            command => command.Parameters.Add("id", NpgsqlDbType.Uuid).Value = accountPaymentId, cancellationToken);
    }

    public async Task<AccountPayment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadOneAsync(connection, null, "idempotency_key = @key",
            command => command.Parameters.Add("key", NpgsqlDbType.Text).Value = idempotencyKey, cancellationToken);
    }

    public async Task<IReadOnlyList<AccountPayment>> GetByCustomerAsync(Guid customerId, int limit = 500, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        await using var command = _dataSource.CreateCommand(
            $"SELECT {Columns} FROM customer_account.account_payments WHERE customer_id = @customer_id ORDER BY requested_at DESC, account_payment_id LIMIT @limit;");
        command.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = customerId;
        command.Parameters.Add("limit", NpgsqlDbType.Integer).Value = limit;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var payments = new List<AccountPayment>();
        while (await reader.ReadAsync(cancellationToken))
            payments.Add(Map(reader));
        return payments;
    }

    public async Task<IReadOnlyList<AccountPaymentStatusChange>> GetHistoryAsync(Guid accountPaymentId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT old_status, new_status, evidence_reference, reason, changed_by, changed_at
            FROM customer_account.account_payment_status_history
            WHERE account_payment_id = @id
            ORDER BY changed_at, account_payment_status_history_id
            LIMIT 100;
            """);
        command.Parameters.Add("id", NpgsqlDbType.Uuid).Value = accountPaymentId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var history = new List<AccountPaymentStatusChange>();
        while (await reader.ReadAsync(cancellationToken))
        {
            history.Add(new AccountPaymentStatusChange(
                reader.IsDBNull(0) ? null : Enum.Parse<AccountPaymentStatus>(reader.GetString(0)),
                Enum.Parse<AccountPaymentStatus>(reader.GetString(1)),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetGuid(4),
                reader.GetFieldValue<DateTimeOffset>(5)));
        }

        return history;
    }

    private static async Task AppendHistoryAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid accountPaymentId,
        AccountPaymentStatus? oldStatus, AccountPaymentStatus newStatus, string? evidenceReference,
        string? reason, Guid? changedBy, DateTimeOffset changedAt, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO customer_account.account_payment_status_history
                (account_payment_status_history_id, account_payment_id, old_status, new_status, evidence_reference, reason, changed_by, changed_at)
            VALUES (@id, @payment_id, @old_status, @new_status, @evidence_reference, @reason, @changed_by, @changed_at);
            """, connection, transaction);
        command.Parameters.Add("id", NpgsqlDbType.Uuid).Value = Guid.NewGuid();
        command.Parameters.Add("payment_id", NpgsqlDbType.Uuid).Value = accountPaymentId;
        command.Parameters.Add("old_status", NpgsqlDbType.Text).Value = (object?)oldStatus?.ToString() ?? DBNull.Value;
        command.Parameters.Add("new_status", NpgsqlDbType.Text).Value = newStatus.ToString();
        command.Parameters.Add("evidence_reference", NpgsqlDbType.Text).Value = (object?)evidenceReference ?? DBNull.Value;
        command.Parameters.Add("reason", NpgsqlDbType.Text).Value = (object?)reason ?? DBNull.Value;
        command.Parameters.Add("changed_by", NpgsqlDbType.Uuid).Value = (object?)changedBy ?? DBNull.Value;
        command.Parameters.Add("changed_at", NpgsqlDbType.TimestampTz).Value = changedAt.UtcDateTime;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<AccountPayment?> ReadOneAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string where,
        Action<NpgsqlCommand> bind, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            $"SELECT {Columns} FROM customer_account.account_payments WHERE {where};", connection, transaction);
        bind(command);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    private static AccountPayment Map(NpgsqlDataReader reader)
    {
        var evidence = reader.IsDBNull(7)
            ? null
            : new AccountPaymentEvidence(Enum.Parse<AccountPaymentEvidenceType>(reader.GetString(7)), reader.GetString(8));
        return new AccountPayment(
            reader.GetGuid(0),
            reader.GetGuid(1),
            Enum.Parse<AccountPaymentMethod>(reader.GetString(2)),
            reader.GetDecimal(3),
            reader.GetString(6),
            reader.GetFieldValue<DateTimeOffset>(10),
            reader.GetString(4),
            Enum.Parse<AccountPaymentStatus>(reader.GetString(5)),
            evidence,
            reader.IsDBNull(9) ? null : reader.GetGuid(9),
            reader.GetFieldValue<DateTimeOffset>(11),
            reader.GetInt64(12));
    }
}
