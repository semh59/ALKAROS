using System.Globalization;
using Npgsql;

namespace ALKAROS.Billing.SplitDesign;

/// <summary>
/// PostgreSQL implementation of <see cref="ISplitDesignRepository"/>.
/// </summary>
public sealed class PostgresSplitDesignRepository : ISplitDesignRepository
{
    private const string AllocationsTable = "billing.bill_allocations";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresSplitDesignRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<IReadOnlyList<BillAllocation>> GetAllocationsByBillIdAsync(
        Guid billId,
        CancellationToken cancellationToken = default)
    {
        if (billId == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(billId));

        var sql = $"""
            SELECT bill_allocation_id, bill_id, bill_item_id, owner_type,
                   owner_reference, allocated_quantity, allocated_amount, tax_amount,
                   created_at, created_by, row_version
            FROM {AllocationsTable}
            WHERE bill_id = @bill_id
            ORDER BY created_at ASC;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("bill_id", billId);

        var list = new List<BillAllocation>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new BillAllocation(
                id: reader.GetGuid(0),
                billId: reader.GetGuid(1),
                billItemId: reader.IsDBNull(2) ? null : reader.GetGuid(2),
                ownerType: Enum.Parse<AllocationOwnerType>(reader.GetString(3)),
                ownerReference: reader.GetString(4),
                allocatedQuantity: reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                allocatedAmount: reader.GetDecimal(6),
                taxAmount: reader.GetDecimal(7),
                createdAt: reader.GetFieldValue<DateTimeOffset>(8),
                createdBy: reader.IsDBNull(9) ? null : reader.GetGuid(9),
                rowVersion: reader.GetInt64(10)));
        }

        return list;
    }

    public async Task SaveSplitDesignAsync(
        Guid billId,
        IReadOnlyList<BillAllocation> allocations,
        CancellationToken cancellationToken = default)
    {
        if (billId == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(billId));
        ArgumentNullException.ThrowIfNull(allocations);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Delete existing allocations for this bill
        await using (var deleteCommand = new NpgsqlCommand(
            $"DELETE FROM {AllocationsTable} WHERE bill_id = @bill_id;",
            connection,
            transaction))
        {
            deleteCommand.Parameters.AddWithValue("bill_id", billId);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        // Insert new allocations
        var insertSql = $"""
            INSERT INTO {AllocationsTable} (
                bill_allocation_id, bill_id, bill_item_id, owner_type,
                owner_reference, allocated_quantity, allocated_amount, tax_amount,
                created_at, created_by, row_version)
            VALUES (
                @bill_allocation_id, @bill_id, @bill_item_id, @owner_type,
                @owner_reference, @allocated_quantity, @allocated_amount, @tax_amount,
                @created_at, @created_by, @row_version);
            """;

        foreach (var allocation in allocations)
        {
            await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
            insertCommand.Parameters.AddWithValue("bill_allocation_id", allocation.Id);
            insertCommand.Parameters.AddWithValue("bill_id", allocation.BillId);
            insertCommand.Parameters.AddWithValue("bill_item_id", (object?)allocation.BillItemId ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("owner_type", allocation.OwnerType.ToString());
            insertCommand.Parameters.AddWithValue("owner_reference", allocation.OwnerReference);
            insertCommand.Parameters.AddWithValue("allocated_quantity", (object?)allocation.AllocatedQuantity ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("allocated_amount", allocation.AllocatedAmount);
            insertCommand.Parameters.AddWithValue("tax_amount", allocation.TaxAmount);
            insertCommand.Parameters.AddWithValue("created_at", allocation.CreatedAt);
            insertCommand.Parameters.AddWithValue("created_by", (object?)allocation.CreatedBy ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("row_version", allocation.RowVersion);

            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteSplitDesignAsync(Guid billId, CancellationToken cancellationToken = default)
    {
        if (billId == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(billId));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            $"DELETE FROM {AllocationsTable} WHERE bill_id = @bill_id;",
            connection);
        command.Parameters.AddWithValue("bill_id", billId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<decimal> GetTotalAllocatedAmountAsync(Guid billId, CancellationToken cancellationToken = default)
    {
        if (billId == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(billId));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            $"SELECT COALESCE(SUM(allocated_amount), 0) FROM {AllocationsTable} WHERE bill_id = @bill_id;",
            connection);
        command.Parameters.AddWithValue("bill_id", billId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? 0m : Convert.ToDecimal(result, CultureInfo.InvariantCulture);
    }
}
