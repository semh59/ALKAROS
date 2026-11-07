using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.CustomerAccounts.Aging;

/// <summary>Reads the whole ledger of every customer who owes or is owed anything and ages it in memory.</summary>
public sealed class PostgresReceivablesAgingService : IReceivablesAgingService
{
    /// <summary>A report that outgrows this fails loud instead of silently dropping customers.</summary>
    public const int MaxLedgerRows = 500_000;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresReceivablesAgingService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<IReadOnlyList<CustomerAging>> GetAsync(
        DateOnly asOf, BusinessCalendar calendar, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        // A customer whose ledger nets to zero but whose cached balance does not is included, so the report can flag it.
        await using var command = _dataSource.CreateCommand(
            """
            WITH effects AS (
                SELECT customer_id, occurred_at, id,
                       CASE WHEN transaction_type = 'Adjustment' OR direction = 'Debit' THEN amount ELSE -amount END AS effect
                FROM customer_account.account_transactions
            ),
            relevant AS (
                SELECT customer_id FROM effects GROUP BY customer_id HAVING SUM(effect) <> 0
                UNION
                SELECT customer_id FROM customer_account.balances WHERE current_balance <> 0
            )
            SELECT e.customer_id, e.occurred_at, e.effect, COALESCE(b.current_balance, 0)
            FROM effects e
            JOIN relevant r ON r.customer_id = e.customer_id
            LEFT JOIN customer_account.balances b ON b.customer_id = e.customer_id
            ORDER BY e.customer_id, e.occurred_at, e.id
            LIMIT @limit;
            """);
        command.Parameters.Add("limit", NpgsqlDbType.Integer).Value = MaxLedgerRows + 1;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var result = new List<CustomerAging>();
        Guid? current = null;
        var projected = 0m;
        var entries = new List<AgingLedgerEntry>();
        var rowCount = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            if (++rowCount > MaxLedgerRows)
                throw new InvalidOperationException($"The receivables ledger exceeds {MaxLedgerRows} rows; the aging report must be made incremental.");
            var customerId = reader.GetFieldValue<Guid>(0);
            if (current is not null && current != customerId)
            {
                result.Add(ReceivablesAgingCalculator.Compute(current.Value, entries, asOf, calendar, projected));
                entries.Clear();
            }

            current = customerId;
            projected = reader.GetFieldValue<decimal>(3);
            entries.Add(new AgingLedgerEntry(reader.GetFieldValue<DateTimeOffset>(1), reader.GetFieldValue<decimal>(2)));
        }

        if (current is not null)
            result.Add(ReceivablesAgingCalculator.Compute(current.Value, entries, asOf, calendar, projected));

        return result.OrderByDescending(aging => aging.TotalDebt).ThenBy(aging => aging.CustomerId).ToArray();
    }
}
