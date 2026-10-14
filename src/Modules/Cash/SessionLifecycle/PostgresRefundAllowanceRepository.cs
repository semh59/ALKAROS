using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Cash.SessionLifecycle;

/// <summary>Outcome of changing the refund allowance of a cashier's open session; <c>SessionId</c> is null without one.</summary>
public sealed record RefundAllowanceChange(Guid? SessionId, bool Applied, decimal Previous, decimal Used);

/// <summary>Owns the shift refund allowance columns of cash.cash_sessions; every write runs in the caller's transaction.</summary>
public static class PostgresRefundAllowanceRepository
{
    /// <summary>Sets the allowance of the cashier's newest open session unless it is below what was already used.</summary>
    public static async Task<RefundAllowanceChange> SetAsync(
        Guid cashierUserId, decimal amount, NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        await using var command = new NpgsqlCommand(
            """
            WITH target AS (
                SELECT cash_session_id, refund_allowance AS previous FROM cash.cash_sessions
                WHERE cashier_user_id = @user AND status = 'Open'
                ORDER BY opened_at DESC LIMIT 1 FOR UPDATE),
            changed AS (
                UPDATE cash.cash_sessions s SET refund_allowance = @amount
                FROM target t
                WHERE s.cash_session_id = t.cash_session_id AND s.refund_allowance_used <= @amount
                RETURNING s.cash_session_id, s.refund_allowance_used)
            SELECT (SELECT cash_session_id FROM target), (SELECT previous FROM target),
                   (SELECT cash_session_id FROM changed), (SELECT refund_allowance_used FROM changed);
            """, connection, transaction);
        command.Parameters.Add("user", NpgsqlDbType.Uuid).Value = cashierUserId;
        command.Parameters.Add("amount", NpgsqlDbType.Numeric).Value = amount;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        if (reader.IsDBNull(0))
            return new RefundAllowanceChange(null, false, 0m, 0m);
        var sessionId = reader.GetGuid(0);
        var previous = reader.GetDecimal(1);
        return reader.IsDBNull(2)
            ? new RefundAllowanceChange(sessionId, false, previous, 0m)
            : new RefundAllowanceChange(sessionId, true, previous, reader.GetDecimal(3));
    }

    /// <summary>Takes <paramref name="amount"/> out of the allowance in one statement; false when it does not fit.</summary>
    public static async Task<bool> TrySpendAsync(
        Guid sessionId, decimal amount, NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        await using var command = new NpgsqlCommand(
            """
            UPDATE cash.cash_sessions SET refund_allowance_used = refund_allowance_used + @amount
            WHERE cash_session_id = @id AND status = 'Open' AND refund_allowance_used + @amount <= refund_allowance;
            """, connection, transaction);
        command.Parameters.Add("amount", NpgsqlDbType.Numeric).Value = amount;
        command.Parameters.Add("id", NpgsqlDbType.Uuid).Value = sessionId;
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }
}
