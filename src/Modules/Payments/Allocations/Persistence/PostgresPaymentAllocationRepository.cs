using ALKAROS.Billing.BillFoundation;
using ALKAROS.Payments.PaymentAggregate;
using Npgsql;

namespace ALKAROS.Payments.Allocations.Persistence;

public sealed class PostgresPaymentAllocationRepository : IPaymentAllocationRepository
{
    private const string Allocations = "payments.payment_allocations";
    private const int MaxUnpagedRows = 5000;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresPaymentAllocationRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<PaymentAllocation> AllocateAsync(
        Payment payment,
        Bill bill,
        decimal amount,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(bill);
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key cannot be empty.", nameof(idempotencyKey));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Idempotency fast path (PDF:II.2.6): a replay returns the existing
        // row instead of re-validating or inserting a second one.
        var existing = await ReadByIdempotencyKeyAsync(connection, transaction, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return existing;
        }

        // Per-bill advisory lock: serializes concurrent allocation attempts
        // against the SAME bill (same pattern as
        // PostgresStockBalanceRepository) so the remaining-amount check
        // below can never race — two concurrent allocations both reading
        // the same "already allocated" sum and both passing would together
        // over-allocate the bill.
        await LockBillAsync(connection, transaction, bill.Id, cancellationToken);

        var alreadyAllocated = await SumAllocatedAsync(connection, transaction, bill.Id, cancellationToken);
        var allocation = PaymentAllocationFactory.Create(payment, bill, amount, alreadyAllocated, idempotencyKey);

        await InsertAsync(connection, transaction, allocation, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return allocation;
    }

    public async Task<PaymentAllocation?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key cannot be empty.", nameof(idempotencyKey));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        return await ReadByIdempotencyKeyAsync(connection, transaction, idempotencyKey, cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentAllocation>> GetByBillIdAsync(Guid billId, CancellationToken cancellationToken = default)
    {
        if (billId == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(billId));

        var result = new List<PaymentAllocation>();
        await using var command = _dataSource.CreateCommand(
            $"""
            SELECT payment_allocation_id, payment_id, bill_id, amount, currency_code, idempotency_key, allocated_at
            FROM {Allocations}
            WHERE bill_id = @bill_id
            ORDER BY allocated_at
            LIMIT {MaxUnpagedRows + 1};
            """);
        command.Parameters.AddWithValue("bill_id", billId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadRow(reader));

        if (result.Count > MaxUnpagedRows)
            throw new InvalidOperationException(
                $"GetByBillIdAsync returned more than {MaxUnpagedRows} rows; narrow the filter or paginate.");

        return result;
    }

    private static async Task LockBillAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid billId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtext($1)::bigint);", connection, transaction);
        command.Parameters.AddWithValue($"payment-allocation:{billId:N}");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<decimal> SumAllocatedAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid billId, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction,
            $"SELECT COALESCE(SUM(amount), 0) FROM {Allocations} WHERE bill_id = @bill_id;");
        command.Parameters.AddWithValue("bill_id", billId);
        return (decimal)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task InsertAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, PaymentAllocation allocation, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction,
            $"""
            INSERT INTO {Allocations} (
                payment_allocation_id, payment_id, bill_id, amount, currency_code, idempotency_key, allocated_at)
            VALUES (@payment_allocation_id, @payment_id, @bill_id, @amount, @currency_code, @idempotency_key, @allocated_at);
            """);
        command.Parameters.AddWithValue("payment_allocation_id", allocation.Id);
        command.Parameters.AddWithValue("payment_id", allocation.PaymentId);
        command.Parameters.AddWithValue("bill_id", allocation.BillId);
        command.Parameters.AddWithValue("amount", allocation.Amount);
        command.Parameters.AddWithValue("currency_code", allocation.CurrencyCode);
        command.Parameters.AddWithValue("idempotency_key", allocation.IdempotencyKey);
        command.Parameters.AddWithValue("allocated_at", allocation.AllocatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<PaymentAllocation?> ReadByIdempotencyKeyAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string idempotencyKey, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction,
            $"""
            SELECT payment_allocation_id, payment_id, bill_id, amount, currency_code, idempotency_key, allocated_at
            FROM {Allocations}
            WHERE idempotency_key = @idempotency_key;
            """);
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadRow(reader);
    }

    private static PaymentAllocation ReadRow(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetGuid(1),
        reader.GetGuid(2),
        reader.GetDecimal(3),
        reader.GetString(4),
        reader.GetString(5),
        reader.GetDateTime(6));

    private static NpgsqlCommand CreateCommand(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }
}
