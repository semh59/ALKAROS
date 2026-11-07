using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.CustomerAccounts.BalanceProjection;

/// <summary>
/// Postgres-backed <see cref="IAccountBalanceProjection"/> against
/// <c>customer_account.balances</c> (migration 162, V14-ACC-002).
/// </summary>
public sealed class PostgresAccountBalanceProjection : IAccountBalanceProjection
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresAccountBalanceProjection(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<CustomerAccountBalance?> GetBalanceAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT customer_id, current_balance, last_transaction_at, updated_at
            FROM customer_account.balances
            WHERE customer_id = @customer_id;
            """);
        command.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = customerId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new CustomerAccountBalance(
            reader.GetFieldValue<Guid>(0),
            reader.GetFieldValue<decimal>(1),
            reader.GetFieldValue<DateTimeOffset>(2),
            reader.GetFieldValue<DateTimeOffset>(3));
    }

    public async Task<IReadOnlyList<Guid>> FindDriftedCustomersAsync(int limit, CancellationToken cancellationToken = default)
    {
        // Same sign rule as the trigger; a customer with neither a ledger nor a non-zero row is not drift.
        await using var command = _dataSource.CreateCommand(
            """
            SELECT COALESCE(l.customer_id, b.customer_id)
            FROM (SELECT customer_id,
                         SUM(CASE WHEN transaction_type = 'Adjustment' OR direction = 'Debit' THEN amount ELSE -amount END) AS total
                  FROM customer_account.account_transactions
                  GROUP BY customer_id) l
            FULL JOIN customer_account.balances b ON b.customer_id = l.customer_id
            WHERE COALESCE(l.total, 0) <> COALESCE(b.current_balance, 0)
            ORDER BY 1
            LIMIT @limit;
            """);
        command.Parameters.Add("limit", NpgsqlDbType.Integer).Value = limit;
        var drifted = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            drifted.Add(reader.GetFieldValue<Guid>(0));
        return drifted;
    }

    public async Task<CustomerAccountBalance> RebuildAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // The ledger trigger updates this row inside every ledger insert, so
        // holding its lock until commit makes concurrent ledger writes wait;
        // the placeholder gives a customer without a row something to lock.
        // DO UPDATE (not DO NOTHING) is what takes the row lock.
        await using (var lockRow = connection.CreateCommand())
        {
            lockRow.Transaction = transaction;
            lockRow.CommandText =
                """
                INSERT INTO customer_account.balances (customer_id, current_balance, last_transaction_at, updated_at)
                VALUES (@customer_id, 0, now(), now())
                ON CONFLICT (customer_id) DO UPDATE SET updated_at = now();
                """;
            lockRow.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = customerId;
            await lockRow.ExecuteNonQueryAsync(cancellationToken);
        }

        // Same sign rule as the trigger (migration 162): Adjustment carries its
        // own sign, every other type is +amount for Debit and -amount for Credit.
        await using var rebuild = connection.CreateCommand();
        rebuild.Transaction = transaction;
        rebuild.CommandText =
            """
            SELECT COALESCE(SUM(CASE WHEN transaction_type = 'Adjustment' OR direction = 'Debit' THEN amount ELSE -amount END), 0),
                   MAX(occurred_at)
            FROM customer_account.account_transactions
            WHERE customer_id = @customer_id;
            """;
        rebuild.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = customerId;
        decimal total;
        DateTimeOffset? lastTransactionAt;
        await using (var reader = await rebuild.ExecuteReaderAsync(cancellationToken))
        {
            await reader.ReadAsync(cancellationToken);
            total = reader.GetFieldValue<decimal>(0);
            lastTransactionAt = reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1);
        }

        var updatedAt = DateTimeOffset.UtcNow;
        await using var write = connection.CreateCommand();
        write.Transaction = transaction;
        write.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = customerId;

        // No transactions at all: the trigger never created a row, so the
        // placeholder (or any stale row) is removed rather than left as zero.
        if (lastTransactionAt is null)
        {
            write.CommandText = "DELETE FROM customer_account.balances WHERE customer_id = @customer_id;";
        }
        else
        {
            write.CommandText =
                """
                UPDATE customer_account.balances
                SET current_balance = @current_balance, last_transaction_at = @last_transaction_at, updated_at = @updated_at
                WHERE customer_id = @customer_id;
                """;
            write.Parameters.Add("current_balance", NpgsqlDbType.Numeric).Value = total;
            write.Parameters.Add("last_transaction_at", NpgsqlDbType.TimestampTz).Value = lastTransactionAt.Value;
            write.Parameters.Add("updated_at", NpgsqlDbType.TimestampTz).Value = updatedAt;
        }
        await write.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new CustomerAccountBalance(customerId, total, lastTransactionAt, updatedAt);
    }
}
