using ALKAROS.Billing.Adjustments;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Payments.PaymentAggregate;
using Npgsql;

namespace ALKAROS.Payments.Allocations.Persistence;

public sealed class PostgresPaymentAllocationRepository : IPaymentAllocationRepository
{
    private const string Allocations = "payments.payment_allocations";
    private const int MaxUnpagedRows = 5000;

    private readonly NpgsqlDataSource _dataSource;
    private readonly IBillAdjustmentRepository _adjustments;

    public PostgresPaymentAllocationRepository(NpgsqlDataSource dataSource, IBillAdjustmentRepository adjustments)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _adjustments = adjustments ?? throw new ArgumentNullException(nameof(adjustments));
    }

    public async Task<PaymentAllocation> AllocateAsync(
        Payment payment,
        Bill bill,
        decimal amount,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var allocation = await AllocateAsync(payment, bill, amount, idempotencyKey, connection, transaction, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return allocation;
    }

    public async Task<PaymentAllocation> AllocateAsync(
        Payment payment,
        Bill bill,
        decimal amount,
        string idempotencyKey,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(bill);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key cannot be empty.", nameof(idempotencyKey));

        // Idempotency fast path (PDF:II.2.6): a replay returns the existing
        // row instead of re-validating or inserting a second one.
        var existing = await ReadByIdempotencyKeyAsync(connection, transaction, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            // V1-RMD-258: defense-in-depth — a genuine cross-bill
            // idempotency-key collision (a key reused for a different bill
            // than it was first recorded under, e.g. a key-generation bug
            // in a caller) must never silently return the wrong bill's
            // allocation as if it were a valid replay for this one.
            if (existing.BillId != bill.Id)
                throw new CrossBillPaymentAllocationException(payment.Id, existing.BillId, bill.Id);
            return existing;
        }

        // Per-bill advisory lock: serializes concurrent allocation attempts
        // against the SAME bill (same pattern as
        // PostgresStockBalanceRepository) so the remaining-amount check
        // below can never race — two concurrent allocations both reading
        // the same "already allocated" sum and both passing would together
        // over-allocate the bill.
        await LockBillAsync(connection, transaction, bill.Id, cancellationToken);

        // V1-RMD-409 (V1-RMD-393 F-07): the caller's Bill was read before any lock; the recall flow can have
        // cancelled it since. Every tender method allocates through here, so this one check under the lock covers
        // cash, EFT and card alike.
        var currentStatus = await ReadBillStatusAsync(connection, transaction, bill.Id, cancellationToken);
        if (string.Equals(currentStatus, nameof(BillState.Cancelled), StringComparison.Ordinal))
            throw new BillNotPayableException(bill.Id, currentStatus!);

        var alreadyAllocated = await SumAllocatedAsync(connection, transaction, bill.Id, cancellationToken);
        // V1-RMD-298: the real (discount/tip-adjusted) ceiling, not the bill's own never-updated
        // PayableAmount - see PaymentAllocationFactory.Create's own comment on this parameter.
        var billAdjustments = await _adjustments.GetByBillIdAsync(bill.Id, cancellationToken);
        var adjustedPayableAmount = AdjustmentCalculator.Calculate(bill, billAdjustments).AdjustedPayableAmount;
        var allocation = PaymentAllocationFactory.Create(payment, bill, amount, alreadyAllocated, adjustedPayableAmount, idempotencyKey);

        await InsertAsync(connection, transaction, allocation, cancellationToken);
        return allocation;
    }

    public async Task<PaymentAllocation?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        return await GetByIdempotencyKeyAsync(idempotencyKey, connection, transaction, cancellationToken);
    }

    public async Task<PaymentAllocation?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key cannot be empty.", nameof(idempotencyKey));

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

    private static async Task<string?> ReadBillStatusAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid billId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT status FROM billing.bills WHERE bill_id = $1;", connection, transaction);
        command.Parameters.AddWithValue(billId);
        return await command.ExecuteScalarAsync(cancellationToken) as string;
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
