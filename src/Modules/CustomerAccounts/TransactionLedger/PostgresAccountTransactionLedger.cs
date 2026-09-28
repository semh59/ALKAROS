using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.CustomerAccounts.TransactionLedger;

/// <summary>
/// Postgres-backed <see cref="IAccountTransactionLedger"/> against
/// <c>customer_account.account_transactions</c> (migration 161,
/// V14-ACC-001). Never issues an UPDATE or DELETE - the table's own
/// <c>prevent_transaction_modification</c> trigger would reject one anyway.
/// </summary>
public sealed class PostgresAccountTransactionLedger : IAccountTransactionLedger
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresAccountTransactionLedger(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<AccountTransaction> RecordAsync(RecordAccountTransactionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var id = Guid.NewGuid();

        await using var insert = _dataSource.CreateCommand(
            """
            INSERT INTO customer_account.account_transactions
                (id, customer_id, transaction_type, amount, source_reference_type, source_reference_id, note, created_by, occurred_at)
            VALUES (@id, @customer_id, @transaction_type, @amount, @source_reference_type, @source_reference_id, @note, @created_by, @occurred_at)
            ON CONFLICT (customer_id, transaction_type, source_reference_type, source_reference_id) DO NOTHING
            RETURNING id, customer_id, transaction_type, direction, amount, source_reference_type, source_reference_id, note, created_by, occurred_at;
            """);
        insert.Parameters.Add("id", NpgsqlDbType.Uuid).Value = id;
        insert.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = request.CustomerId;
        insert.Parameters.Add("transaction_type", NpgsqlDbType.Text).Value = request.TransactionType.ToString();
        insert.Parameters.Add("amount", NpgsqlDbType.Numeric).Value = request.Amount;
        insert.Parameters.Add("source_reference_type", NpgsqlDbType.Text).Value = request.SourceReferenceType;
        insert.Parameters.Add("source_reference_id", NpgsqlDbType.Uuid).Value = request.SourceReferenceId;
        insert.Parameters.Add("note", NpgsqlDbType.Text).Value = (object?)request.Note ?? DBNull.Value;
        insert.Parameters.Add("created_by", NpgsqlDbType.Uuid).Value = (object?)request.CreatedBy ?? DBNull.Value;
        insert.Parameters.Add("occurred_at", NpgsqlDbType.TimestampTz).Value = request.OccurredAt;

        await using (var reader = await insert.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
                return ReadRecord(reader);
        }

        // ON CONFLICT DO NOTHING hit the idempotency key - the same source
        // event was already recorded. Return the existing row rather than
        // creating a duplicate or raising an error.
        await using var existing = _dataSource.CreateCommand(
            """
            SELECT id, customer_id, transaction_type, direction, amount, source_reference_type, source_reference_id, note, created_by, occurred_at
            FROM customer_account.account_transactions
            WHERE customer_id = @customer_id AND transaction_type = @transaction_type
                AND source_reference_type = @source_reference_type AND source_reference_id = @source_reference_id;
            """);
        existing.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = request.CustomerId;
        existing.Parameters.Add("transaction_type", NpgsqlDbType.Text).Value = request.TransactionType.ToString();
        existing.Parameters.Add("source_reference_type", NpgsqlDbType.Text).Value = request.SourceReferenceType;
        existing.Parameters.Add("source_reference_id", NpgsqlDbType.Uuid).Value = request.SourceReferenceId;
        await using var existingReader = await existing.ExecuteReaderAsync(cancellationToken);
        if (await existingReader.ReadAsync(cancellationToken))
            return ReadRecord(existingReader);

        throw new InvalidOperationException(
            "Insert conflicted on the idempotency key but no existing row was found; this should be unreachable.");
    }

    public async Task<AccountTransaction?> GetAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT id, customer_id, transaction_type, direction, amount, source_reference_type, source_reference_id, note, created_by, occurred_at
            FROM customer_account.account_transactions
            WHERE id = @id;
            """);
        command.Parameters.Add("id", NpgsqlDbType.Uuid).Value = transactionId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadRecord(reader) : null;
    }

    public async Task<IReadOnlyList<AccountTransaction>> GetByCustomerAsync(
        Guid customerId, int limit = 1000, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT id, customer_id, transaction_type, direction, amount, source_reference_type, source_reference_id, note, created_by, occurred_at
            FROM customer_account.account_transactions
            WHERE customer_id = @customer_id
            ORDER BY occurred_at ASC
            LIMIT @limit;
            """);
        command.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = customerId;
        command.Parameters.Add("limit", NpgsqlDbType.Integer).Value = limit;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<AccountTransaction>();
        while (await reader.ReadAsync(cancellationToken))
            results.Add(ReadRecord(reader));
        return results;
    }

    private static AccountTransaction ReadRecord(NpgsqlDataReader reader)
    {
        var id = reader.GetFieldValue<Guid>(0);
        var customerId = reader.GetFieldValue<Guid>(1);

        var rawType = reader.GetString(2);
        if (!Enum.TryParse<AccountTransactionType>(rawType, out var transactionType))
            throw new FormatException($"Account transaction {id} has an unrecognized transaction_type '{rawType}'.");

        var rawDirection = reader.GetString(3);
        if (!Enum.TryParse<AccountTransactionDirection>(rawDirection, out var direction))
            throw new FormatException($"Account transaction {id} has an unrecognized direction '{rawDirection}'.");

        var amount = reader.GetFieldValue<decimal>(4);
        var sourceReferenceType = reader.GetString(5);
        var sourceReferenceId = reader.GetFieldValue<Guid>(6);
        var note = reader.IsDBNull(7) ? null : reader.GetString(7);
        var createdBy = reader.IsDBNull(8) ? (Guid?)null : reader.GetFieldValue<Guid>(8);
        var occurredAt = reader.GetFieldValue<DateTimeOffset>(9);

        return new AccountTransaction(id, customerId, transactionType, direction, amount, sourceReferenceType, sourceReferenceId, note, createdBy, occurredAt);
    }
}
