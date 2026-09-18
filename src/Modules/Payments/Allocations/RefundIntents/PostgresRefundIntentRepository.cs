using ALKAROS.Payments.Allocations.Persistence;
using Npgsql;

namespace ALKAROS.Payments.Allocations.RefundIntents;

public sealed class PostgresRefundIntentRepository : IRefundIntentRepository
{
    private const string RefundIntents = "payments.refund_intents";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresRefundIntentRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<RefundIntent> CreateAsync(
        Guid paymentId,
        PaymentAllocation allocation,
        decimal requestedAmount,
        string idempotencyKey,
        Guid? requestedBy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(allocation);
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key cannot be empty.", nameof(idempotencyKey));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Idempotency fast path (PDF:II.2.6): a replay returns the existing
        // row instead of re-validating or inserting a second one.
        var existing = await ReadByIdempotencyKeyAsync(connection, transaction, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return existing;
        }

        // Per-allocation advisory lock: serializes concurrent refund-intent
        // requests against the SAME allocation (same pattern
        // PostgresPaymentAllocationRepository already uses per-bill) so the
        // remaining-eligibility check below can never race.
        await LockAllocationAsync(connection, transaction, allocation.Id, cancellationToken);

        var alreadyPending = await SumPendingAsync(connection, transaction, allocation.Id, cancellationToken);
        var refundIntent = RefundIntentFactory.Create(allocation, paymentId, requestedAmount, alreadyPending, idempotencyKey, requestedBy);

        await InsertAsync(connection, transaction, refundIntent, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return refundIntent;
    }

    public async Task<RefundIntent?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key cannot be empty.", nameof(idempotencyKey));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        return await ReadByIdempotencyKeyAsync(connection, transaction, idempotencyKey, cancellationToken);
    }

    public async Task<IReadOnlyList<RefundIntent>> GetByAllocationIdAsync(Guid paymentAllocationId, CancellationToken cancellationToken = default)
    {
        if (paymentAllocationId == Guid.Empty)
            throw new ArgumentException("Payment allocation id cannot be empty.", nameof(paymentAllocationId));

        var result = new List<RefundIntent>();
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT refund_intent_id, payment_id, payment_allocation_id, requested_amount, status,
                   idempotency_key, requested_by, requested_at, rejected_at, rejection_reason, row_version
            FROM {RefundIntents}
            WHERE payment_allocation_id = @payment_allocation_id
            ORDER BY requested_at, refund_intent_id;
            """);
        command.Parameters.AddWithValue("payment_allocation_id", paymentAllocationId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadRow(reader));

        return result;
    }

    public async Task<long> SaveAsync(RefundIntent refundIntent, long expectedRowVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refundIntent);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"""
            UPDATE {RefundIntents}
            SET status = @status,
                rejected_at = @rejected_at,
                rejection_reason = @rejection_reason,
                row_version = row_version + 1
            WHERE refund_intent_id = @refund_intent_id AND row_version = @expected_row_version
            RETURNING row_version;
            """;
        command.Parameters.AddWithValue("refund_intent_id", refundIntent.Id);
        command.Parameters.AddWithValue("status", refundIntent.Status.ToString());
        command.Parameters.AddWithValue("rejected_at", (object?)refundIntent.RejectedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("rejection_reason", (object?)refundIntent.RejectionReason ?? DBNull.Value);
        command.Parameters.AddWithValue("expected_row_version", expectedRowVersion);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null)
            throw new InvalidOperationException(
                $"Refund intent {refundIntent.Id} not found or concurrent modification " +
                $"(expected row version {expectedRowVersion}).");

        await transaction.CommitAsync(cancellationToken);
        return (long)result;
    }

    private static async Task LockAllocationAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid allocationId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtext($1)::bigint);", connection, transaction);
        command.Parameters.AddWithValue($"refund-intent:{allocationId:N}");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<decimal> SumPendingAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid allocationId, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction,
            $"""
            SELECT COALESCE(SUM(requested_amount), 0)
            FROM {RefundIntents}
            WHERE payment_allocation_id = @payment_allocation_id AND status = 'Pending';
            """);
        command.Parameters.AddWithValue("payment_allocation_id", allocationId);
        return (decimal)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task InsertAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, RefundIntent refundIntent, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction,
            $"""
            INSERT INTO {RefundIntents} (
                refund_intent_id, payment_id, payment_allocation_id, requested_amount, status,
                idempotency_key, requested_by, requested_at, rejected_at, rejection_reason, row_version)
            VALUES (
                @refund_intent_id, @payment_id, @payment_allocation_id, @requested_amount, @status,
                @idempotency_key, @requested_by, @requested_at, @rejected_at, @rejection_reason, @row_version);
            """);
        command.Parameters.AddWithValue("refund_intent_id", refundIntent.Id);
        command.Parameters.AddWithValue("payment_id", refundIntent.PaymentId);
        command.Parameters.AddWithValue("payment_allocation_id", refundIntent.PaymentAllocationId);
        command.Parameters.AddWithValue("requested_amount", refundIntent.RequestedAmount);
        command.Parameters.AddWithValue("status", refundIntent.Status.ToString());
        command.Parameters.AddWithValue("idempotency_key", refundIntent.IdempotencyKey);
        command.Parameters.AddWithValue("requested_by", (object?)refundIntent.RequestedBy ?? DBNull.Value);
        command.Parameters.AddWithValue("requested_at", refundIntent.RequestedAt);
        command.Parameters.AddWithValue("rejected_at", (object?)refundIntent.RejectedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("rejection_reason", (object?)refundIntent.RejectionReason ?? DBNull.Value);
        command.Parameters.AddWithValue("row_version", refundIntent.RowVersion);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<RefundIntent?> ReadByIdempotencyKeyAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string idempotencyKey, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction,
            $"""
            SELECT refund_intent_id, payment_id, payment_allocation_id, requested_amount, status,
                   idempotency_key, requested_by, requested_at, rejected_at, rejection_reason, row_version
            FROM {RefundIntents}
            WHERE idempotency_key = @idempotency_key;
            """);
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadRow(reader);
    }

    private static RefundIntent ReadRow(NpgsqlDataReader reader) => new(
        id: reader.GetGuid(0),
        paymentId: reader.GetGuid(1),
        paymentAllocationId: reader.GetGuid(2),
        requestedAmount: reader.GetDecimal(3),
        idempotencyKey: reader.GetString(5),
        status: Enum.Parse<RefundIntentStatus>(reader.GetString(4)),
        requestedBy: reader.IsDBNull(6) ? null : reader.GetGuid(6),
        requestedAt: reader.GetDateTime(7),
        rejectedAt: reader.IsDBNull(8) ? null : reader.GetDateTime(8),
        rejectionReason: reader.IsDBNull(9) ? null : reader.GetString(9),
        rowVersion: reader.GetInt64(10));

    private static NpgsqlCommand CreateCommand(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }
}
