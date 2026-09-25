using Npgsql;

namespace ALKAROS.Payments.ManualResolution;

public sealed class PostgresManualCardConfirmationRepository : IManualCardConfirmationRepository
{
    private const string Columns =
        "confirmation_id, payment_id, bill_id, slip_number, amount, status, requested_by, requested_at, request_note, " +
        "decided_by, decided_at, decision_note, row_version";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresManualCardConfirmationRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task InsertAsync(
        ManualCardConfirmation confirmation, NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(confirmation);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO payments.manual_card_confirmations
                (confirmation_id, payment_id, bill_id, slip_number, amount, status, requested_by, requested_at, request_note, row_version)
            VALUES (@id, @payment_id, @bill_id, @slip, @amount, 'Pending', @requested_by, @requested_at, @note, 1);
            """, connection, transaction);
        command.Parameters.AddWithValue("id", confirmation.Id);
        command.Parameters.AddWithValue("payment_id", confirmation.PaymentId);
        command.Parameters.AddWithValue("bill_id", confirmation.BillId);
        command.Parameters.AddWithValue("slip", confirmation.SlipNumber);
        command.Parameters.AddWithValue("amount", confirmation.Amount);
        command.Parameters.AddWithValue("requested_by", confirmation.RequestedBy);
        command.Parameters.AddWithValue("requested_at", confirmation.RequestedAt);
        command.Parameters.AddWithValue("note", (object?)confirmation.RequestNote ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ManualCardConfirmation?> GetByIdAsync(
        Guid id, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using var command = new NpgsqlCommand(
            $"SELECT {Columns} FROM payments.manual_card_confirmations WHERE confirmation_id = @id;", connection, transaction);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task<ManualCardConfirmation?> GetPendingByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            $"SELECT {Columns} FROM payments.manual_card_confirmations WHERE payment_id = @payment_id AND status = 'Pending' LIMIT 1;");
        command.Parameters.AddWithValue("payment_id", paymentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task DecideAsync(
        Guid id, ManualCardConfirmationStatus status, Guid decidedBy, DateTimeOffset decidedAt, string? note,
        long expectedRowVersion, NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        await using var command = new NpgsqlCommand(
            """
            UPDATE payments.manual_card_confirmations
            SET status = @status, decided_by = @decided_by, decided_at = @decided_at, decision_note = @note,
                row_version = row_version + 1
            WHERE confirmation_id = @id AND status = 'Pending' AND row_version = @expected;
            """, connection, transaction);
        command.Parameters.AddWithValue("status", status.ToString());
        command.Parameters.AddWithValue("decided_by", decidedBy);
        command.Parameters.AddWithValue("decided_at", decidedAt);
        command.Parameters.AddWithValue("note", (object?)note ?? DBNull.Value);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("expected", expectedRowVersion);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            throw new InvalidOperationException($"Manual card confirmation {id} was decided or changed concurrently.");
    }

    public async Task<IReadOnlyList<ManualCardConfirmation>> ListAsync(
        ManualCardConfirmationStatus? status, int limit, CancellationToken cancellationToken = default)
    {
        var bounded = Math.Clamp(limit, 1, 500);
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT {Columns} FROM payments.manual_card_confirmations
            WHERE @status::text IS NULL OR status = @status
            ORDER BY requested_at DESC
            LIMIT {bounded};
            """);
        command.Parameters.AddWithValue("status", (object?)status?.ToString() ?? DBNull.Value);
        var results = new List<ManualCardConfirmation>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            results.Add(Read(reader));
        return results;
    }

    private static ManualCardConfirmation Read(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetDecimal(4),
        Enum.Parse<ManualCardConfirmationStatus>(reader.GetString(5)), reader.GetGuid(6),
        reader.GetFieldValue<DateTimeOffset>(7), reader.IsDBNull(8) ? null : reader.GetString(8),
        reader.IsDBNull(9) ? null : reader.GetGuid(9),
        reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10),
        reader.IsDBNull(11) ? null : reader.GetString(11), reader.GetInt64(12));
}
