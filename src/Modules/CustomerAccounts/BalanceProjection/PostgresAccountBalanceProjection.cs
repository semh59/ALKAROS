using ALKAROS.CustomerAccounts.TransactionLedger;
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
    private readonly IAccountTransactionLedger _ledger;

    public PostgresAccountBalanceProjection(NpgsqlDataSource dataSource, IAccountTransactionLedger ledger)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
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

    public async Task<CustomerAccountBalance> RebuildAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var transactions = await _ledger.GetByCustomerAsync(customerId, limit: int.MaxValue, cancellationToken);
        var total = transactions.Aggregate(0m, (sum, transaction) => sum + transaction.SignedBalanceEffect);
        var lastTransactionAt = transactions.Count > 0 ? transactions.Max(transaction => transaction.OccurredAt) : (DateTimeOffset?)null;
        var updatedAt = DateTimeOffset.UtcNow;

        // No transactions at all: nothing to rebuild from, and no row
        // should exist either (the trigger never created one). Remove any
        // stale row rather than leaving a zero balance behind.
        if (lastTransactionAt is null)
        {
            await using var delete = _dataSource.CreateCommand(
                "DELETE FROM customer_account.balances WHERE customer_id = @customer_id;");
            delete.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = customerId;
            await delete.ExecuteNonQueryAsync(cancellationToken);
            return new CustomerAccountBalance(customerId, 0, null, updatedAt);
        }

        await using var upsert = _dataSource.CreateCommand(
            """
            INSERT INTO customer_account.balances (customer_id, current_balance, last_transaction_at, updated_at)
            VALUES (@customer_id, @current_balance, @last_transaction_at, @updated_at)
            ON CONFLICT (customer_id) DO UPDATE
                SET current_balance = EXCLUDED.current_balance,
                    last_transaction_at = EXCLUDED.last_transaction_at,
                    updated_at = EXCLUDED.updated_at;
            """);
        upsert.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = customerId;
        upsert.Parameters.Add("current_balance", NpgsqlDbType.Numeric).Value = total;
        upsert.Parameters.Add("last_transaction_at", NpgsqlDbType.TimestampTz).Value = lastTransactionAt.Value;
        upsert.Parameters.Add("updated_at", NpgsqlDbType.TimestampTz).Value = updatedAt;
        await upsert.ExecuteNonQueryAsync(cancellationToken);

        return new CustomerAccountBalance(customerId, total, lastTransactionAt, updatedAt);
    }
}
