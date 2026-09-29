using System.Data;
using System.Globalization;
using ALKAROS.Billing.BillFoundation;
using Npgsql;

namespace ALKAROS.Billing.SplitDesign;

/// <summary>
/// PostgreSQL implementation of <see cref="ISplitDesignRepository"/>.
/// </summary>
public sealed class PostgresSplitDesignRepository : ISplitDesignRepository
{
    private const string AllocationsTable = "billing.bill_allocations";

    // Defensive ceiling for a filtered list read: a real filter returns
    // far fewer rows. Hitting this means the filter is too broad, or the
    // relation outgrew its assumption — fail loud, do not load unboundedly.
    private const int MaxUnpagedRows = 5000;

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
            ORDER BY created_at ASC
            LIMIT {MaxUnpagedRows + 1};
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

        if (list.Count > MaxUnpagedRows)
            throw new InvalidOperationException(
                $"GetAllocationsByBillIdAsync returned more than {MaxUnpagedRows} rows; narrow the filter or paginate.");

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
            if (allocation.BillId != billId)
                throw new ArgumentException($"Allocation bill ID '{allocation.BillId}' does not match target bill ID '{billId}'.", nameof(allocations));

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

    public async Task<OperationalSplitSaveResult> ReplaceOperationalSplitDesignAsync(
        Guid billId,
        long expectedBillRowVersion,
        IReadOnlyList<AllocationVersion> expectedAllocations,
        IReadOnlyList<BillAllocation> allocations,
        CancellationToken cancellationToken = default)
    {
        if (billId == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(billId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedBillRowVersion);
        ArgumentNullException.ThrowIfNull(expectedAllocations);
        ArgumentNullException.ThrowIfNull(allocations);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var bill = await LockBillAsync(connection, transaction, billId, cancellationToken)
            ?? throw new KeyNotFoundException($"Bill {billId} was not found.");
        if (bill.RowVersion != expectedBillRowVersion)
        {
            throw new SplitDesignConcurrencyException(
                "bill",
                billId,
                expectedBillRowVersion,
                bill.RowVersion);
        }

        if (bill.Status is not (BillState.Open
            or BillState.PartiallyAllocated
            or BillState.Allocated
            or BillState.Reopened))
        {
            throw new SplitDesignUnsupportedBillStateException(billId, bill.Status.ToString());
        }

        var currentVersions = await ReadAllocationVersionsAsync(
            connection,
            transaction,
            billId,
            cancellationToken);
        ValidateExpectedVersions(billId, expectedAllocations, currentVersions);
        await ValidateOperationalAllocationsAsync(
            connection,
            transaction,
            bill,
            allocations,
            cancellationToken);

        await DeleteAllocationsAsync(connection, transaction, billId, cancellationToken);
        await InsertAllocationsAsync(connection, transaction, billId, allocations, cancellationToken);
        var newBillRowVersion = await IncrementBillRowVersionAsync(
            connection,
            transaction,
            billId,
            expectedBillRowVersion,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OperationalSplitSaveResult(newBillRowVersion, allocations);
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

    private static async Task<LockedBill?> LockBillAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid billId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT status, table_id, payable_amount, tax_total, row_version
            FROM billing.bills
            WHERE bill_id = @bill_id
            FOR UPDATE;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("bill_id", billId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new LockedBill(
            billId,
            Enum.Parse<BillState>(reader.GetString(0)),
            reader.IsDBNull(1) ? null : reader.GetGuid(1),
            reader.GetDecimal(2),
            reader.GetDecimal(3),
            reader.GetInt64(4));
    }

    private static async Task<IReadOnlyDictionary<Guid, long>> ReadAllocationVersionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid billId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            $"SELECT bill_allocation_id, row_version FROM {AllocationsTable} WHERE bill_id = @bill_id FOR UPDATE;",
            connection,
            transaction);
        command.Parameters.AddWithValue("bill_id", billId);
        var result = new Dictionary<Guid, long>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(reader.GetGuid(0), reader.GetInt64(1));
        return result;
    }

    private static void ValidateExpectedVersions(
        Guid billId,
        IReadOnlyList<AllocationVersion> expected,
        IReadOnlyDictionary<Guid, long> current)
    {
        if (expected.Any(version => version.AllocationId == Guid.Empty || version.RowVersion <= 0))
            throw new ArgumentException("Expected allocation IDs and row versions must be positive.", nameof(expected));

        Dictionary<Guid, long> expectedById;
        try
        {
            expectedById = expected.ToDictionary(version => version.AllocationId, version => version.RowVersion);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException("Expected allocation IDs must be unique.", nameof(expected), exception);
        }

        foreach (var currentVersion in current)
        {
            if (!expectedById.TryGetValue(currentVersion.Key, out var expectedVersion))
                throw new SplitDesignConcurrencyException("split-design", billId, null, currentVersion.Value);
            if (expectedVersion != currentVersion.Value)
            {
                throw new SplitDesignConcurrencyException(
                    "allocation",
                    currentVersion.Key,
                    expectedVersion,
                    currentVersion.Value);
            }
        }

        var removed = expectedById.Keys.FirstOrDefault(id => !current.ContainsKey(id));
        if (removed != Guid.Empty)
        {
            throw new SplitDesignConcurrencyException(
                "allocation",
                removed,
                expectedById[removed],
                null);
        }
    }

    private static async Task ValidateOperationalAllocationsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        LockedBill bill,
        IReadOnlyList<BillAllocation> allocations,
        CancellationToken cancellationToken)
    {
        if (allocations.Count == 0)
            return;
        if (allocations.Count < 2)
            throw new ArgumentException("A split design requires at least two allocations.", nameof(allocations));
        if (allocations.Any(allocation => allocation.BillId != bill.Id))
            throw new ArgumentException("Every allocation must belong to the target bill.", nameof(allocations));
        if (allocations.Select(allocation => allocation.Id).Distinct().Count() != allocations.Count)
            throw new ArgumentException("Allocation IDs must be unique.", nameof(allocations));
        // V1-RMD-413 (V1-RMD-393 F-11): billing.bill_adjustments never mutates the bill's own totals, and the split
        // engine sizes a design against the discount/fee/tip-adjusted totals. Comparing against the raw bill totals
        // here refused every design on an adjusted bill. The adjustments are read under this transaction's bill row
        // lock, which a discount also takes, so they cannot change between this check and the commit.
        var (adjustedPayable, adjustedTax) = await ReadAdjustedTotalsAsync(connection, transaction, bill, cancellationToken);
        if (allocations.Sum(allocation => allocation.AllocatedAmount) != adjustedPayable)
            throw new InvalidOperationException("Allocation amounts must exactly equal the current bill payable amount.");
        if (allocations.Sum(allocation => allocation.TaxAmount) != adjustedTax)
            throw new InvalidOperationException("Allocation taxes must exactly equal the current bill tax total.");

        var parsedOwners = new List<OperationalAllocationOwner>(allocations.Count);
        foreach (var allocation in allocations)
        {
            if (!OperationalOwnerReference.TryParse(allocation.OwnerReference, out var owner))
                throw new ArgumentException("Allocation owner reference is not an operational owner reference.", nameof(allocations));
            parsedOwners.Add(owner!);
            ValidateModeShape(owner!.Mode, allocation);
        }

        if (parsedOwners.Select(owner => owner.Mode).Distinct().Count() != 1)
            throw new ArgumentException("All allocations must use one split mode.", nameof(allocations));

        await ValidateItemQuantitiesAsync(connection, transaction, bill.Id, allocations, cancellationToken);
        await ValidateSeatOwnersAsync(connection, transaction, bill, parsedOwners, cancellationToken);
    }

    /// <summary>
    /// The bill's payable amount and tax total after its adjustments, with the same arithmetic as
    /// <c>AdjustmentCalculator.Calculate</c>: deductions lower both, fees raise both, tips raise the payable only.
    /// </summary>
    private static async Task<(decimal Payable, decimal Tax)> ReadAdjustedTotalsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        LockedBill bill,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT
                COALESCE(SUM(gross_amount) FILTER (WHERE is_deduction), 0),
                COALESCE(SUM(tax_amount) FILTER (WHERE is_deduction), 0),
                COALESCE(SUM(gross_amount) FILTER (WHERE NOT is_deduction AND adjustment_type <> 'Tip'), 0),
                COALESCE(SUM(tax_amount) FILTER (WHERE NOT is_deduction AND adjustment_type <> 'Tip'), 0),
                COALESCE(SUM(gross_amount) FILTER (WHERE NOT is_deduction AND adjustment_type = 'Tip'), 0)
            FROM billing.bill_adjustments
            WHERE bill_id = @bill_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("bill_id", bill.Id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        var discountGross = BillMath.RoundCurrency(reader.GetDecimal(0));
        var discountTax = BillMath.RoundCurrency(reader.GetDecimal(1));
        var feeGross = BillMath.RoundCurrency(reader.GetDecimal(2));
        var feeTax = BillMath.RoundCurrency(reader.GetDecimal(3));
        var tipGross = BillMath.RoundCurrency(reader.GetDecimal(4));
        return (
            BillMath.RoundCurrency(bill.PayableAmount - discountGross + feeGross + tipGross),
            BillMath.RoundCurrency(Math.Max(0m, bill.TaxTotal - discountTax + feeTax)));
    }

    private static void ValidateModeShape(SplitMode mode, BillAllocation allocation)
    {
        var hasItem = allocation.BillItemId.HasValue;
        var hasQuantity = allocation.AllocatedQuantity.HasValue;
        if (hasItem != hasQuantity)
            throw new ArgumentException("Allocation item ID and quantity must be supplied together.", nameof(allocation));

        var valid = mode switch
        {
            SplitMode.EqualByPerson => allocation.OwnerType == AllocationOwnerType.Person && !hasItem,
            SplitMode.ByItem => allocation.OwnerType == AllocationOwnerType.Item && hasItem,
            SplitMode.ByAmount => allocation.OwnerType == AllocationOwnerType.Amount && !hasItem,
            SplitMode.Custom => allocation.OwnerType == AllocationOwnerType.Person,
            _ => false,
        };
        if (!valid)
            throw new ArgumentException("Allocation shape does not match its split mode.", nameof(allocation));
    }

    private static async Task ValidateItemQuantitiesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid billId,
        IReadOnlyList<BillAllocation> allocations,
        CancellationToken cancellationToken)
    {
        var itemGroups = allocations
            .Where(allocation => allocation.BillItemId.HasValue)
            .GroupBy(allocation => allocation.BillItemId!.Value)
            .ToList();
        if (itemGroups.Count == 0)
            return;

        var ids = itemGroups.Select(group => group.Key).ToArray();
        await using var command = new NpgsqlCommand(
            """
            SELECT bill_item_id, quantity
            FROM billing.bill_items
            WHERE bill_id = @bill_id AND bill_item_id = ANY(@item_ids)
            FOR UPDATE;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("bill_id", billId);
        command.Parameters.AddWithValue("item_ids", ids);
        var quantities = new Dictionary<Guid, decimal>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                quantities.Add(reader.GetGuid(0), reader.GetDecimal(1));
        }

        foreach (var group in itemGroups)
        {
            if (!quantities.TryGetValue(group.Key, out var available))
                throw new InvalidOperationException($"Bill item {group.Key} does not belong to bill {billId}.");
            if (group.Sum(allocation => allocation.AllocatedQuantity!.Value) > available)
                throw new InvalidOperationException($"Allocated quantity for bill item {group.Key} exceeds {available}.");
        }
    }

    private static async Task ValidateSeatOwnersAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        LockedBill bill,
        IReadOnlyList<OperationalAllocationOwner> owners,
        CancellationToken cancellationToken)
    {
        var seatIds = owners
            .Where(owner => owner.Kind == AllocationOwnerKind.Seat)
            .Select(owner => owner.Id)
            .Distinct()
            .ToArray();
        if (seatIds.Length == 0)
            return;
        if (!bill.TableId.HasValue)
            throw new InvalidOperationException("Seat owners require a table-bound bill.");

        await using var command = new NpgsqlCommand(
            """
            SELECT seat_id
            FROM table_mgmt.table_seats
            WHERE table_id = @table_id AND seat_id = ANY(@seat_ids)
            FOR SHARE;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("table_id", bill.TableId.Value);
        command.Parameters.AddWithValue("seat_ids", seatIds);
        var found = new HashSet<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            found.Add(reader.GetGuid(0));

        var unknown = seatIds.FirstOrDefault(id => !found.Contains(id));
        if (unknown != Guid.Empty)
            throw new InvalidOperationException($"Seat {unknown} does not belong to bill table {bill.TableId.Value}.");
    }

    private static async Task DeleteAllocationsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid billId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            $"DELETE FROM {AllocationsTable} WHERE bill_id = @bill_id;",
            connection,
            transaction);
        command.Parameters.AddWithValue("bill_id", billId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> IncrementBillRowVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid billId,
        long expectedRowVersion,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            UPDATE billing.bills
            SET row_version = row_version + 1,
                updated_at = now()
            WHERE bill_id = @bill_id AND row_version = @expected_row_version
            RETURNING row_version;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("bill_id", billId);
        command.Parameters.AddWithValue("expected_row_version", expectedRowVersion);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null)
            throw new SplitDesignConcurrencyException("bill", billId, expectedRowVersion, null);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    private static async Task InsertAllocationsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid billId,
        IReadOnlyList<BillAllocation> allocations,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO billing.bill_allocations (
                bill_allocation_id, bill_id, bill_item_id, owner_type,
                owner_reference, allocated_quantity, allocated_amount, tax_amount,
                created_at, created_by, row_version)
            VALUES (
                @bill_allocation_id, @bill_id, @bill_item_id, @owner_type,
                @owner_reference, @allocated_quantity, @allocated_amount, @tax_amount,
                @created_at, @created_by, 1);
            """;
        foreach (var allocation in allocations)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("bill_allocation_id", allocation.Id);
            command.Parameters.AddWithValue("bill_id", billId);
            command.Parameters.AddWithValue("bill_item_id", (object?)allocation.BillItemId ?? DBNull.Value);
            command.Parameters.AddWithValue("owner_type", allocation.OwnerType.ToString());
            command.Parameters.AddWithValue("owner_reference", allocation.OwnerReference);
            command.Parameters.AddWithValue("allocated_quantity", (object?)allocation.AllocatedQuantity ?? DBNull.Value);
            command.Parameters.AddWithValue("allocated_amount", allocation.AllocatedAmount);
            command.Parameters.AddWithValue("tax_amount", allocation.TaxAmount);
            command.Parameters.AddWithValue("created_at", allocation.CreatedAt);
            command.Parameters.AddWithValue("created_by", (object?)allocation.CreatedBy ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private sealed record LockedBill(
        Guid Id,
        BillState Status,
        Guid? TableId,
        decimal PayableAmount,
        decimal TaxTotal,
        long RowVersion);
}
