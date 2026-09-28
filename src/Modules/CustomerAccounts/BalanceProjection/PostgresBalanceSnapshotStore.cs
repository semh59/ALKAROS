using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.CustomerAccounts.BalanceProjection;

/// <summary>
/// Postgres-backed <see cref="IBalanceSnapshotStore"/> against
/// <c>customer_account.balance_snapshots</c> (migration 162, V14-ACC-002).
/// </summary>
public sealed class PostgresBalanceSnapshotStore : IBalanceSnapshotStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresBalanceSnapshotStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<BalanceSnapshot> TakeSnapshotAsync(
        Guid customerId, DateOnly snapshotDate, decimal balance, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        await using var insert = _dataSource.CreateCommand(
            """
            INSERT INTO customer_account.balance_snapshots (id, customer_id, snapshot_date, balance, created_at)
            VALUES (@id, @customer_id, @snapshot_date, @balance, @created_at)
            ON CONFLICT (customer_id, snapshot_date) DO NOTHING
            RETURNING id, customer_id, snapshot_date, balance, created_at;
            """);
        insert.Parameters.Add("id", NpgsqlDbType.Uuid).Value = id;
        insert.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = customerId;
        insert.Parameters.Add("snapshot_date", NpgsqlDbType.Date).Value = snapshotDate.ToDateTime(TimeOnly.MinValue);
        insert.Parameters.Add("balance", NpgsqlDbType.Numeric).Value = balance;
        insert.Parameters.Add("created_at", NpgsqlDbType.TimestampTz).Value = createdAt;

        await using (var reader = await insert.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
                return ReadRecord(reader);
        }

        // Idempotent: a snapshot for this customer/date already exists.
        return (await GetSnapshotAsync(customerId, snapshotDate, cancellationToken))!;
    }

    public async Task<BalanceSnapshot?> GetSnapshotAsync(Guid customerId, DateOnly snapshotDate, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT id, customer_id, snapshot_date, balance, created_at
            FROM customer_account.balance_snapshots
            WHERE customer_id = @customer_id AND snapshot_date = @snapshot_date;
            """);
        command.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = customerId;
        command.Parameters.Add("snapshot_date", NpgsqlDbType.Date).Value = snapshotDate.ToDateTime(TimeOnly.MinValue);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadRecord(reader) : null;
    }

    public async Task<IReadOnlyList<BalanceSnapshot>> GetSnapshotsAsync(
        Guid customerId, int limit = 1000, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT id, customer_id, snapshot_date, balance, created_at
            FROM customer_account.balance_snapshots
            WHERE customer_id = @customer_id
            ORDER BY snapshot_date ASC
            LIMIT @limit;
            """);
        command.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = customerId;
        command.Parameters.Add("limit", NpgsqlDbType.Integer).Value = limit;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<BalanceSnapshot>();
        while (await reader.ReadAsync(cancellationToken))
            results.Add(ReadRecord(reader));
        return results;
    }

    private static BalanceSnapshot ReadRecord(NpgsqlDataReader reader) =>
        new(
            reader.GetFieldValue<Guid>(0),
            reader.GetFieldValue<Guid>(1),
            DateOnly.FromDateTime(reader.GetFieldValue<DateTime>(2)),
            reader.GetFieldValue<decimal>(3),
            reader.GetFieldValue<DateTimeOffset>(4));
}
