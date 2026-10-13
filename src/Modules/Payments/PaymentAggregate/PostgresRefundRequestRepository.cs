using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Payments.PaymentAggregate;

/// <summary>One row of payments.refund_requests.</summary>
public sealed record RefundRequest(
    Guid Id,
    Guid BillId,
    Guid PaymentId,
    decimal Amount,
    string Method,
    string Reason,
    string Status,
    Guid RequestedBy,
    DateTimeOffset RequestedAt,
    Guid? DecidedBy,
    DateTimeOffset? DecidedAt,
    string? SlipNumber);

/// <summary>Owns payments.refund_requests; every write runs in the caller's transaction.</summary>
public static class PostgresRefundRequestRepository
{
    private const string Columns =
        "refund_request_id, bill_id, payment_id, amount, method, reason, status, requested_by, requested_at, "
        + "decided_by, decided_at, slip_number";

    /// <summary>Inserts a Requested row; returns false when the payment already has an open request.</summary>
    public static async Task<bool> TryInsertAsync(
        RefundRequest request, NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var insert = new NpgsqlCommand(
                """
                INSERT INTO payments.refund_requests
                    (refund_request_id, bill_id, payment_id, amount, method, reason, status, requested_by, requested_at)
                VALUES (@id, @bill, @payment, @amount, @method, @reason, 'Requested', @by, @at);
                """, connection, transaction);
            insert.Parameters.Add("id", NpgsqlDbType.Uuid).Value = request.Id;
            insert.Parameters.Add("bill", NpgsqlDbType.Uuid).Value = request.BillId;
            insert.Parameters.Add("payment", NpgsqlDbType.Uuid).Value = request.PaymentId;
            insert.Parameters.Add("amount", NpgsqlDbType.Numeric).Value = request.Amount;
            insert.Parameters.Add("method", NpgsqlDbType.Text).Value = request.Method;
            insert.Parameters.Add("reason", NpgsqlDbType.Text).Value = request.Reason;
            insert.Parameters.Add("by", NpgsqlDbType.Uuid).Value = request.RequestedBy;
            insert.Parameters.Add("at", NpgsqlDbType.TimestampTz).Value = request.RequestedAt.UtcDateTime;
            await insert.ExecuteNonQueryAsync(cancellationToken);
            return true;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return false;
        }
    }

    public static async Task<RefundRequest?> LoadForUpdateAsync(
        Guid requestId, NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        await using var command = new NpgsqlCommand(
            $"SELECT {Columns} FROM payments.refund_requests WHERE refund_request_id = @id FOR UPDATE;",
            connection, transaction);
        command.Parameters.Add("id", NpgsqlDbType.Uuid).Value = requestId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public static async Task<IReadOnlyList<RefundRequest>> ListAsync(
        NpgsqlDataSource dataSource, string? status, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            $"""
            SELECT {Columns} FROM payments.refund_requests
            WHERE @status::text IS NULL OR status = @status
            ORDER BY requested_at DESC
            LIMIT 200;
            """);
        command.Parameters.Add("status", NpgsqlDbType.Text).Value = (object?)status ?? DBNull.Value;
        var result = new List<RefundRequest>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(Read(reader));
        return result;
    }

    /// <summary>Records the approval: <c>Completed</c> when the money went out at once, else <c>AwaitingManualRefund</c>.</summary>
    public static Task MarkApprovedAsync(
        Guid requestId, bool completed, Guid deciderId, DateTimeOffset at, NpgsqlConnection connection,
        NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
        => ExecuteAsync(
            """
            UPDATE payments.refund_requests
            SET status = @status, decided_by = @by, decided_at = @at,
                completed_by = CASE WHEN @completed THEN requested_by END,
                completed_at = CASE WHEN @completed THEN @at END,
                row_version = row_version + 1
            WHERE refund_request_id = @id;
            """, connection, transaction, cancellationToken,
            ("status", NpgsqlDbType.Text, completed ? "Completed" : "AwaitingManualRefund"),
            ("completed", NpgsqlDbType.Boolean, completed),
            ("by", NpgsqlDbType.Uuid, deciderId), ("at", NpgsqlDbType.TimestampTz, at.UtcDateTime),
            ("id", NpgsqlDbType.Uuid, requestId));

    public static Task MarkRejectedAsync(
        Guid requestId, Guid deciderId, string note, DateTimeOffset at, NpgsqlConnection connection,
        NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
        => ExecuteAsync(
            """
            UPDATE payments.refund_requests
            SET status = 'Rejected', decided_by = @by, decided_at = @at, decision_note = @note, row_version = row_version + 1
            WHERE refund_request_id = @id;
            """, connection, transaction, cancellationToken,
            ("by", NpgsqlDbType.Uuid, deciderId), ("at", NpgsqlDbType.TimestampTz, at.UtcDateTime),
            ("note", NpgsqlDbType.Text, note), ("id", NpgsqlDbType.Uuid, requestId));

    public static Task MarkCompletedAsync(
        Guid requestId, string slipNumber, string? note, Guid completedBy, DateTimeOffset at,
        NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
        => ExecuteAsync(
            """
            UPDATE payments.refund_requests
            SET status = 'Completed', slip_number = @slip, decision_note = COALESCE(@note, decision_note),
                completed_by = @by, completed_at = @at, row_version = row_version + 1
            WHERE refund_request_id = @id;
            """, connection, transaction, cancellationToken,
            ("slip", NpgsqlDbType.Text, slipNumber), ("note", NpgsqlDbType.Text, (object?)note ?? DBNull.Value),
            ("by", NpgsqlDbType.Uuid, completedBy), ("at", NpgsqlDbType.TimestampTz, at.UtcDateTime),
            ("id", NpgsqlDbType.Uuid, requestId));

    private static async Task ExecuteAsync(
        string sql, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken,
        params (string Name, NpgsqlDbType Type, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, type, value) in parameters)
            command.Parameters.Add(name, type).Value = value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static RefundRequest Read(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetDecimal(3), reader.GetString(4),
        reader.GetString(5), reader.GetString(6), reader.GetGuid(7),
        new DateTimeOffset(reader.GetDateTime(8), TimeSpan.Zero),
        reader.IsDBNull(9) ? null : reader.GetGuid(9),
        reader.IsDBNull(10) ? null : new DateTimeOffset(reader.GetDateTime(10), TimeSpan.Zero),
        reader.IsDBNull(11) ? null : reader.GetString(11));
}
