using Npgsql;

namespace ALKAROS.Inventory.Transactions;

public sealed class PostgresInventoryTransactionRunner : IInventoryTransactionRunner
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresInventoryTransactionRunner(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<T> RunAsync<T>(Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> operation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        var result = await operation(connection, transaction);
        await transaction.CommitAsync(ct);
        return result;
    }
}
