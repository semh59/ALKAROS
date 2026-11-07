using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.CustomerAccounts.BalanceProjection;

/// <summary>
/// Postgres-backed <see cref="IBalanceSnapshotStore"/> against
/// <c>customer_account.balance_snapshots</c> (migration 162, V14-ACC-002).
/// </summary>
public sealed class PostgresBalanceSnapshotStore : IBalanceSnapshotStore
{
    /// <summary>One report reads at most this many snapshots (customers times lookback dates); more fails loud.</summary>
    public const int MaxSnapshotsRead = 100_000;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresBalanceSnapshotStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<int> TakeClosingSnapshotsAsync(
        DateOnly snapshotDate, DateTimeOffset cutoff, CancellationToken cancellationToken = default)
    {
        // Same sign rule as the balance trigger (migration 162): Adjustment carries its own sign.
        await using var insert = _dataSource.CreateCommand(
            """
            INSERT INTO customer_account.balance_snapshots (id, customer_id, snapshot_date, balance, created_at)
            SELECT gen_random_uuid(), customer_id, @snapshot_date,
                   SUM(CASE WHEN transaction_type = 'Adjustment' OR direction = 'Debit' THEN amount ELSE -amount END),
                   now()
            FROM customer_account.account_transactions
            WHERE occurred_at < @cutoff
            GROUP BY customer_id
            ON CONFLICT (customer_id, snapshot_date) DO NOTHING;
            """);
        insert.Parameters.Add("snapshot_date", NpgsqlDbType.Date).Value = snapshotDate.ToDateTime(TimeOnly.MinValue);
        insert.Parameters.Add("cutoff", NpgsqlDbType.TimestampTz).Value = cutoff.UtcDateTime;
        return await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<DateOnly>> GetSnapshotDatesAsync(DateOnly from, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            "SELECT DISTINCT snapshot_date FROM customer_account.balance_snapshots WHERE snapshot_date >= @from LIMIT 10000;");
        command.Parameters.Add("from", NpgsqlDbType.Date).Value = from.ToDateTime(TimeOnly.MinValue);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var dates = new HashSet<DateOnly>();
        while (await reader.ReadAsync(cancellationToken))
            dates.Add(DateOnly.FromDateTime(reader.GetFieldValue<DateTime>(0)));
        return dates;
    }

    public async Task<IReadOnlyList<BalanceSnapshot>> GetSnapshotsOnAsync(
        IReadOnlyCollection<DateOnly> dates, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dates);
        await using var command = _dataSource.CreateCommand(
            """
            SELECT id, customer_id, snapshot_date, balance, created_at
            FROM customer_account.balance_snapshots
            WHERE snapshot_date = ANY(@dates)
            ORDER BY customer_id, snapshot_date
            LIMIT @limit;
            """);
        command.Parameters.Add("limit", NpgsqlDbType.Integer).Value = MaxSnapshotsRead + 1;
        command.Parameters.Add("dates", NpgsqlDbType.Array | NpgsqlDbType.Date).Value =
            dates.Select(date => date.ToDateTime(TimeOnly.MinValue)).ToArray();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<BalanceSnapshot>();
        while (await reader.ReadAsync(cancellationToken))
        {
            if (results.Count == MaxSnapshotsRead)
                throw new InvalidOperationException($"More than {MaxSnapshotsRead} snapshots requested at once.");
            results.Add(new BalanceSnapshot(
                reader.GetFieldValue<Guid>(0),
                reader.GetFieldValue<Guid>(1),
                DateOnly.FromDateTime(reader.GetFieldValue<DateTime>(2)),
                reader.GetFieldValue<decimal>(3),
                reader.GetFieldValue<DateTimeOffset>(4)));
        }

        return results;
    }
}
