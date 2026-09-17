using Npgsql;

namespace ALKAROS.Payments.PaymentAggregate;

public sealed class PostgresPaymentRepository : IPaymentRepository
{
    private const string Payments = "payments.payments";
    private const string History = "payments.payment_status_history";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresPaymentRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Payment id cannot be empty.", nameof(id));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            System.Data.IsolationLevel.RepeatableRead, cancellationToken);

        var row = await ReadPaymentAsync(connection, transaction, id, cancellationToken);
        if (row is null)
            return null;

        var history = await ReadHistoryAsync(connection, transaction, id, cancellationToken);
        return row.ToDomain(history.Select(h => h.ToDomain()).ToList());
    }

    public async Task<IReadOnlyList<Payment>> GetByBillIdAsync(Guid billId, CancellationToken cancellationToken = default)
    {
        if (billId == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(billId));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            System.Data.IsolationLevel.RepeatableRead, cancellationToken);

        var rows = await ReadPaymentsByBillAsync(connection, transaction, billId, cancellationToken);
        var result = new List<Payment>(rows.Count);
        foreach (var row in rows)
        {
            var history = await ReadHistoryAsync(connection, transaction, row.Id, cancellationToken);
            result.Add(row.ToDomain(history.Select(h => h.ToDomain()).ToList()));
        }

        return result;
    }

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await AddAsync(payment, connection, transaction, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AddAsync(
        Payment payment,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        await InsertPaymentAsync(connection, transaction, payment, cancellationToken);

        foreach (var row in payment.History)
            await InsertHistoryAsync(connection, transaction, row, cancellationToken);
    }

    public async Task<long> SaveAsync(Payment payment, long expectedRowVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var newRowVersion = await SaveAsync(payment, expectedRowVersion, connection, transaction, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return newRowVersion;
    }

    public async Task<long> SaveAsync(
        Payment payment,
        long expectedRowVersion,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        var newRowVersion = await UpdatePaymentAsync(connection, transaction, payment, expectedRowVersion, cancellationToken);

        var knownHistoryIds = (await ReadHistoryIdsAsync(connection, transaction, payment.Id, cancellationToken)).ToHashSet();
        foreach (var row in payment.History)
        {
            if (!knownHistoryIds.Contains(row.Id))
                await InsertHistoryAsync(connection, transaction, row, cancellationToken);
        }

        return newRowVersion;
    }

    private static async Task InsertPaymentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Payment payment,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction,
            $"""
            INSERT INTO {Payments} (
                payment_id, bill_id, status, currency_code,
                requested_amount, tendered_amount, approved_amount, change_amount,
                initiated_at, tendered_at, approved_at, declined_at, cancelled_at,
                created_at, updated_at, row_version)
            VALUES (@payment_id, @bill_id, @status, @currency_code,
                    @requested_amount, @tendered_amount, @approved_amount, @change_amount,
                    @initiated_at, @tendered_at, @approved_at, @declined_at, @cancelled_at,
                    @created_at, @updated_at, @row_version);
            """);
        BindPayment(command, payment);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> UpdatePaymentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Payment payment,
        long expectedRowVersion,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction,
            $"""
            UPDATE {Payments}
            SET status = @status,
                tendered_amount = @tendered_amount,
                approved_amount = @approved_amount,
                change_amount = @change_amount,
                tendered_at = @tendered_at,
                approved_at = @approved_at,
                declined_at = @declined_at,
                cancelled_at = @cancelled_at,
                updated_at = @updated_at,
                row_version = row_version + 1
            WHERE payment_id = @payment_id AND row_version = @expected_row_version
            RETURNING row_version;
            """);
        BindPayment(command, payment);
        command.Parameters.AddWithValue("expected_row_version", expectedRowVersion);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null)
            throw new InvalidOperationException(
                $"Payment {payment.Id} not found or concurrent modification " +
                $"(expected row version {expectedRowVersion}).");

        return (long)result;
    }

    private static void BindPayment(NpgsqlCommand command, Payment payment)
    {
        command.Parameters.AddWithValue("payment_id", payment.Id);
        command.Parameters.AddWithValue("bill_id", payment.BillId);
        command.Parameters.AddWithValue("status", payment.Status.ToString());
        command.Parameters.AddWithValue("currency_code", payment.CurrencyCode);
        command.Parameters.AddWithValue("requested_amount", payment.RequestedAmount);
        command.Parameters.AddWithValue("tendered_amount", (object?)payment.TenderedAmount ?? DBNull.Value);
        command.Parameters.AddWithValue("approved_amount", (object?)payment.ApprovedAmount ?? DBNull.Value);
        command.Parameters.AddWithValue("change_amount", payment.ChangeAmount);
        command.Parameters.AddWithValue("initiated_at", payment.InitiatedAt);
        command.Parameters.AddWithValue("tendered_at", (object?)payment.TenderedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("approved_at", (object?)payment.ApprovedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("declined_at", (object?)payment.DeclinedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("cancelled_at", (object?)payment.CancelledAt ?? DBNull.Value);
        command.Parameters.AddWithValue("created_at", payment.CreatedAt);
        command.Parameters.AddWithValue("updated_at", payment.UpdatedAt);
        command.Parameters.AddWithValue("row_version", payment.RowVersion);
    }

    private static async Task InsertHistoryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PaymentStatusHistoryEntry row,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction,
            $"""
            INSERT INTO {History} (
                payment_status_history_id, payment_id, old_status, new_status, reason, changed_by, changed_at)
            VALUES (@payment_status_history_id, @payment_id, @old_status, @new_status, @reason, @changed_by, @changed_at);
            """);
        command.Parameters.AddWithValue("payment_status_history_id", row.Id);
        command.Parameters.AddWithValue("payment_id", row.PaymentId);
        command.Parameters.AddWithValue("old_status", row.OldStatus.ToString());
        command.Parameters.AddWithValue("new_status", row.NewStatus.ToString());
        command.Parameters.AddWithValue("reason", (object?)row.Reason ?? DBNull.Value);
        command.Parameters.AddWithValue("changed_by", (object?)row.ChangedBy ?? DBNull.Value);
        command.Parameters.AddWithValue("changed_at", row.ChangedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<PaymentRow?> ReadPaymentAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid id, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction,
            $"""
            SELECT payment_id, bill_id, status, currency_code,
                   requested_amount, tendered_amount, approved_amount, change_amount,
                   initiated_at, tendered_at, approved_at, declined_at, cancelled_at,
                   created_at, updated_at, row_version
            FROM {Payments}
            WHERE payment_id = @id;
            """);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new PaymentRow(
            reader.GetGuid(0),
            reader.GetGuid(1),
            Enum.Parse<PaymentStatus>(reader.GetString(2)),
            reader.GetString(3),
            reader.GetDecimal(4),
            reader.IsDBNull(5) ? null : reader.GetDecimal(5),
            reader.IsDBNull(6) ? null : reader.GetDecimal(6),
            reader.GetDecimal(7),
            reader.GetDateTime(8),
            reader.IsDBNull(9) ? null : reader.GetDateTime(9),
            reader.IsDBNull(10) ? null : reader.GetDateTime(10),
            reader.IsDBNull(11) ? null : reader.GetDateTime(11),
            reader.IsDBNull(12) ? null : reader.GetDateTime(12),
            reader.GetDateTime(13),
            reader.GetDateTime(14),
            reader.GetInt64(15));
    }

    private static async Task<IReadOnlyList<PaymentRow>> ReadPaymentsByBillAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid billId, CancellationToken cancellationToken)
    {
        var result = new List<PaymentRow>();

        await using var command = CreateCommand(connection, transaction,
            $"""
            SELECT payment_id, bill_id, status, currency_code,
                   requested_amount, tendered_amount, approved_amount, change_amount,
                   initiated_at, tendered_at, approved_at, declined_at, cancelled_at,
                   created_at, updated_at, row_version
            FROM {Payments}
            WHERE bill_id = @bill_id
            ORDER BY initiated_at, payment_id;
            """);
        command.Parameters.AddWithValue("bill_id", billId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new PaymentRow(
                reader.GetGuid(0),
                reader.GetGuid(1),
                Enum.Parse<PaymentStatus>(reader.GetString(2)),
                reader.GetString(3),
                reader.GetDecimal(4),
                reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                reader.GetDecimal(7),
                reader.GetDateTime(8),
                reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                reader.IsDBNull(12) ? null : reader.GetDateTime(12),
                reader.GetDateTime(13),
                reader.GetDateTime(14),
                reader.GetInt64(15)));
        }

        return result;
    }

    private static async Task<IReadOnlyList<HistoryRow>> ReadHistoryAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid paymentId, CancellationToken cancellationToken)
    {
        var result = new List<HistoryRow>();

        await using var command = CreateCommand(connection, transaction,
            $"""
            SELECT payment_status_history_id, payment_id, old_status, new_status, reason, changed_by, changed_at
            FROM {History}
            WHERE payment_id = @payment_id
            ORDER BY changed_at;
            """);
        command.Parameters.AddWithValue("payment_id", paymentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new HistoryRow(
                reader.GetGuid(0),
                reader.GetGuid(1),
                Enum.Parse<PaymentStatus>(reader.GetString(2)),
                Enum.Parse<PaymentStatus>(reader.GetString(3)),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetGuid(5),
                reader.GetDateTime(6)));
        }

        return result;
    }

    private static async Task<IReadOnlyList<Guid>> ReadHistoryIdsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid paymentId, CancellationToken cancellationToken)
    {
        var result = new List<Guid>();

        await using var command = CreateCommand(connection, transaction,
            $"SELECT payment_status_history_id FROM {History} WHERE payment_id = @payment_id;");
        command.Parameters.AddWithValue("payment_id", paymentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(reader.GetGuid(0));

        return result;
    }

    private static NpgsqlCommand CreateCommand(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }

    private sealed record PaymentRow(
        Guid Id,
        Guid BillId,
        PaymentStatus Status,
        string CurrencyCode,
        decimal RequestedAmount,
        decimal? TenderedAmount,
        decimal? ApprovedAmount,
        decimal ChangeAmount,
        DateTimeOffset InitiatedAt,
        DateTimeOffset? TenderedAt,
        DateTimeOffset? ApprovedAt,
        DateTimeOffset? DeclinedAt,
        DateTimeOffset? CancelledAt,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        long RowVersion)
    {
        public Payment ToDomain(IReadOnlyList<PaymentStatusHistoryEntry> history) => new(
            Id, BillId, RequestedAmount, CurrencyCode,
            TenderedAmount, ApprovedAmount, ChangeAmount,
            Status, history,
            InitiatedAt, TenderedAt, ApprovedAt, DeclinedAt, CancelledAt,
            RowVersion, CreatedAt, UpdatedAt);
    }

    private sealed record HistoryRow(
        Guid Id,
        Guid PaymentId,
        PaymentStatus OldStatus,
        PaymentStatus NewStatus,
        string? Reason,
        Guid? ChangedBy,
        DateTimeOffset ChangedAt)
    {
        public PaymentStatusHistoryEntry ToDomain() => new(Id, PaymentId, OldStatus, NewStatus, Reason, ChangedBy, ChangedAt);
    }
}
