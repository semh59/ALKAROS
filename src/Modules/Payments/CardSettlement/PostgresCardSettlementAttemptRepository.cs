using Npgsql;

namespace ALKAROS.Payments.CardSettlement;

public sealed class PostgresCardSettlementAttemptRepository : ICardSettlementAttemptRepository
{
    private const string Attempts = "payments.card_settlement_attempts";

    public async Task<CardSettlementAttempt?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key cannot be empty.", nameof(idempotencyKey));

        await using var command = CreateCommand(connection, transaction,
            $"""
            SELECT card_settlement_attempt_id, idempotency_key, provider_correlation_id, payment_id,
                   outcome, approved_amount, allocation_id, reason, fiscal_handoff_queued, created_at
            FROM {Attempts}
            WHERE idempotency_key = @idempotency_key;
            """);
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new CardSettlementAttempt(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetGuid(3),
            Enum.Parse<CardSettlementOutcome>(reader.GetString(4)),
            reader.IsDBNull(5) ? null : reader.GetDecimal(5),
            reader.IsDBNull(6) ? null : reader.GetGuid(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.GetBoolean(8),
            reader.GetDateTime(9));
    }

    public async Task InsertAsync(
        CardSettlementAttempt attempt,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        await using var command = CreateCommand(connection, transaction,
            $"""
            INSERT INTO {Attempts} (
                card_settlement_attempt_id, idempotency_key, provider_correlation_id, payment_id,
                outcome, approved_amount, allocation_id, reason, fiscal_handoff_queued, created_at)
            VALUES (
                @card_settlement_attempt_id, @idempotency_key, @provider_correlation_id, @payment_id,
                @outcome, @approved_amount, @allocation_id, @reason, @fiscal_handoff_queued, @created_at);
            """);
        command.Parameters.AddWithValue("card_settlement_attempt_id", attempt.Id);
        command.Parameters.AddWithValue("idempotency_key", attempt.IdempotencyKey);
        command.Parameters.AddWithValue("provider_correlation_id", attempt.ProviderCorrelationId);
        command.Parameters.AddWithValue("payment_id", attempt.PaymentId);
        command.Parameters.AddWithValue("outcome", attempt.Outcome.ToString());
        command.Parameters.AddWithValue("approved_amount", (object?)attempt.ApprovedAmount ?? DBNull.Value);
        command.Parameters.AddWithValue("allocation_id", (object?)attempt.AllocationId ?? DBNull.Value);
        command.Parameters.AddWithValue("reason", (object?)attempt.Reason ?? DBNull.Value);
        command.Parameters.AddWithValue("fiscal_handoff_queued", attempt.FiscalHandoffQueued);
        command.Parameters.AddWithValue("created_at", attempt.CreatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static NpgsqlCommand CreateCommand(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }
}
