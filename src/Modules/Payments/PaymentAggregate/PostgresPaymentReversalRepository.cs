using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Payments.PaymentAggregate;

/// <summary>One immutable row of payments.payment_reversals.</summary>
public sealed record PaymentReversal(
    Guid Id,
    Guid PaymentId,
    string ReversalType,
    decimal Amount,
    string Reason,
    Guid RefundedBy,
    string IdempotencyKey,
    DateTimeOffset OccurredAt);

public sealed class PaymentRefundException(string message) : PaymentException(message);

/// <summary>
/// Appends refunds to the reversal ledger and moves the payment on to Refunded or PartiallyRefunded in the caller's
/// transaction. Retrying with the same idempotency key returns the row already written.
/// </summary>
public sealed class PostgresPaymentReversalRepository(NpgsqlDataSource dataSource, IPaymentRepository payments)
{
    public async Task<PaymentReversal> AppendAsync(
        Guid paymentId,
        decimal amount,
        string reason,
        Guid refundedBy,
        string idempotencyKey,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateTimeOffset? at = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        if (string.IsNullOrWhiteSpace(reason))
            throw new PaymentRefundException("A refund reason is required.");
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new PaymentRefundException("An idempotency key is required.");
        if (amount <= 0)
            throw new InvalidPaymentAmountException($"Refund amount '{amount}' must be greater than zero.");

        // Serialises refunds of the same payment; the payment is read only after the lock is held.
        await using (var lockRow = new NpgsqlCommand(
            "SELECT 1 FROM payments.payments WHERE payment_id = @id FOR UPDATE;", connection, transaction))
        {
            lockRow.Parameters.Add("id", NpgsqlDbType.Uuid).Value = paymentId;
            if (await lockRow.ExecuteScalarAsync(cancellationToken) is null)
                throw new PaymentNotFoundException(paymentId);
        }

        var existing = await FindByKeyAsync(connection, transaction, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.PaymentId != paymentId || existing.Amount != amount)
                throw new PaymentRefundException("The idempotency key was already used for a different refund.");
            return existing;
        }

        var payment = await payments.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new PaymentNotFoundException(paymentId);
        var already = await SumAsync(connection, transaction, paymentId, cancellationToken);
        var total = already + amount;
        if (payment.ApprovedAmount is null || total > payment.ApprovedAmount.Value)
            throw new PaymentRefundException(
                $"Refund of '{amount}' would exceed the approved amount of payment '{paymentId}'.");

        var when = at ?? DateTimeOffset.UtcNow;
        var refunded = payment.ApplyRefund(total, reason, refundedBy, when);
        var reversal = new PaymentReversal(
            Guid.NewGuid(), paymentId,
            total == payment.ApprovedAmount ? "FullRefund" : "PartialRefund",
            amount, reason.Trim(), refundedBy, idempotencyKey, when);

        await using (var insert = new NpgsqlCommand(
            """
            INSERT INTO payments.payment_reversals
                (reversal_id, payment_id, reversal_type, amount, reason, refunded_by, idempotency_key, occurred_at)
            VALUES (@id, @payment_id, @type, @amount, @reason, @by, @key, @at);
            """, connection, transaction))
        {
            insert.Parameters.Add("id", NpgsqlDbType.Uuid).Value = reversal.Id;
            insert.Parameters.Add("payment_id", NpgsqlDbType.Uuid).Value = paymentId;
            insert.Parameters.Add("type", NpgsqlDbType.Text).Value = reversal.ReversalType;
            insert.Parameters.Add("amount", NpgsqlDbType.Numeric).Value = amount;
            insert.Parameters.Add("reason", NpgsqlDbType.Text).Value = reversal.Reason;
            insert.Parameters.Add("by", NpgsqlDbType.Uuid).Value = refundedBy;
            insert.Parameters.Add("key", NpgsqlDbType.Text).Value = idempotencyKey;
            insert.Parameters.Add("at", NpgsqlDbType.TimestampTz).Value = when.UtcDateTime;
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await payments.SaveAsync(refunded, payment.RowVersion, connection, transaction, cancellationToken);
        return reversal;
    }

    /// <summary>Total refunded so far for the payment, committed rows only.</summary>
    public async Task<decimal> GetRefundedTotalAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT COALESCE(SUM(amount), 0) FROM payments.payment_reversals WHERE payment_id = @id;");
        command.Parameters.AddWithValue("id", paymentId);
        return (decimal)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task<decimal> SumAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid paymentId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT COALESCE(SUM(amount), 0) FROM payments.payment_reversals WHERE payment_id = @id;",
            connection, transaction);
        command.Parameters.Add("id", NpgsqlDbType.Uuid).Value = paymentId;
        return (decimal)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task<PaymentReversal?> FindByKeyAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string key, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT reversal_id, payment_id, reversal_type, amount, reason, refunded_by, idempotency_key, occurred_at
            FROM payments.payment_reversals WHERE idempotency_key = @key;
            """, connection, transaction);
        command.Parameters.Add("key", NpgsqlDbType.Text).Value = key;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;
        return new PaymentReversal(
            reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetDecimal(3),
            reader.GetString(4), reader.GetGuid(5), reader.GetString(6),
            new DateTimeOffset(reader.GetDateTime(7), TimeSpan.Zero));
    }
}
