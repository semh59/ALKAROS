using ALKAROS.Cash.Contracts;
using Npgsql;

namespace ALKAROS.Cash.TransactionLedger;

public sealed class PostgresCashTransactionLedgerRepository : ICashTransactionLedgerRepository
{
    private const string Transactions = "cash.cash_transactions";
    private const int MaxUnpagedRows = 5000;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresCashTransactionLedgerRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task RecordAsync(CashTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        await using var command = _dataSource.CreateCommand(
            $"""
            INSERT INTO {Transactions} (
                cash_transaction_id, cash_session_id, type, direction, amount,
                related_payment_id, notes, recorded_by, occurred_at)
            VALUES (
                @cash_transaction_id, @cash_session_id, @type, @direction, @amount,
                @related_payment_id, @notes, @recorded_by, @occurred_at);
            """);
        command.Parameters.AddWithValue("cash_transaction_id", transaction.Id);
        command.Parameters.AddWithValue("cash_session_id", transaction.CashSessionId);
        command.Parameters.AddWithValue("type", transaction.Type.ToString());
        command.Parameters.AddWithValue("direction", transaction.Direction.ToString());
        command.Parameters.AddWithValue("amount", transaction.Amount);
        command.Parameters.AddWithValue("related_payment_id", (object?)transaction.RelatedPaymentId ?? DBNull.Value);
        command.Parameters.AddWithValue("notes", (object?)transaction.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("recorded_by", (object?)transaction.RecordedBy ?? DBNull.Value);
        command.Parameters.AddWithValue("occurred_at", transaction.OccurredAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CashTransaction>> GetBySessionIdAsync(Guid cashSessionId, CancellationToken cancellationToken = default)
    {
        if (cashSessionId == Guid.Empty)
            throw new ArgumentException("Cash session id cannot be empty.", nameof(cashSessionId));

        var result = new List<CashTransaction>();
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT cash_transaction_id, cash_session_id, type, direction, amount,
                   related_payment_id, notes, recorded_by, occurred_at
            FROM {Transactions}
            WHERE cash_session_id = @cash_session_id
            ORDER BY occurred_at
            LIMIT {MaxUnpagedRows + 1};
            """);
        command.Parameters.AddWithValue("cash_session_id", cashSessionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadRow(reader));

        if (result.Count > MaxUnpagedRows)
            throw new InvalidOperationException(
                $"GetBySessionIdAsync returned more than {MaxUnpagedRows} rows; narrow the filter or paginate.");

        return result;
    }

    public async Task<decimal> ComputeExpectedCashAsync(Guid cashSessionId, CancellationToken cancellationToken = default)
    {
        if (cashSessionId == Guid.Empty)
            throw new ArgumentException("Cash session id cannot be empty.", nameof(cashSessionId));

        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT COALESCE(SUM(CASE WHEN direction = 'In' THEN amount ELSE -amount END), 0)
            FROM {Transactions}
            WHERE cash_session_id = @cash_session_id
              AND type NOT IN ('CountAdjustment', 'ClosingDifference');
            """);
        command.Parameters.AddWithValue("cash_session_id", cashSessionId);

        return (decimal)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static CashTransaction ReadRow(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetGuid(1),
        Enum.Parse<CashTransactionType>(reader.GetString(2)),
        reader.GetDecimal(4),
        Enum.Parse<CashTransactionDirection>(reader.GetString(3)),
        reader.IsDBNull(5) ? null : reader.GetGuid(5),
        reader.IsDBNull(6) ? null : reader.GetString(6),
        reader.IsDBNull(7) ? null : reader.GetGuid(7),
        reader.GetDateTime(8));
}
