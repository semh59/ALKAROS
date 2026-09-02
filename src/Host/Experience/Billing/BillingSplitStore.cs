using ALKAROS.Billing.BillFoundation;
using ALKAROS.Billing.SplitDesign;
using ALKAROS.Orders.OrderAggregate;
using Npgsql;

namespace ALKAROS.Host.Experience.Billing;

public sealed class BillingSplitStore
{
    private readonly IBillRepository _bills;
    private readonly ISplitDesignRepository _splitDesigns;
    private readonly IOrderRepository? _orders;
    private readonly NpgsqlDataSource? _dataSource;

    public BillingSplitStore(IBillRepository bills, ISplitDesignRepository splitDesigns, IOrderRepository? orders = null, NpgsqlDataSource? dataSource = null)
    {
        _bills = bills ?? throw new ArgumentNullException(nameof(bills));
        _splitDesigns = splitDesigns ?? throw new ArgumentNullException(nameof(splitDesigns));
        _orders = orders;
        _dataSource = dataSource;
    }

    public async Task<BillSplitDesignDto> CreateBillFromOrderAsync(
        Guid orderId,
        bool canMutate,
        CancellationToken cancellationToken = default)
    {
        if (_orders == null)
            throw new InvalidOperationException("Order repository is not configured.");

        var existingBills = await _bills.GetByOrderIdAsync(orderId, cancellationToken);
        var activeBill = existingBills.FirstOrDefault(b => b.Status != BillState.Cancelled);
        if (activeBill != null)
        {
            var existingAllocations = await _splitDesigns.GetAllocationsByBillIdAsync(activeBill.Id, cancellationToken);
            return Map(activeBill, existingAllocations, canMutate);
        }

        var order = await _orders.GetByIdAsync(orderId, cancellationToken)
            ?? throw new BillingSplitNotFoundException($"Order {orderId} was not found.");

        if (order.Status is OrderState.Cancelled or OrderState.Rejected)
            throw new InvalidOperationException($"Cannot create a bill from an order in '{order.Status}' state.");

        var billId = Guid.NewGuid();
        var billNumber = $"BILL-{order.OrderNumber}";
        var bill = Bill.FromOrder(billId, billNumber, order);

        try
        {
            await _bills.AddAsync(bill, cancellationToken);
        }
        catch (Exception)
        {
            // If another request concurrently inserted a bill for this order/number, recover gracefully
            var retryBills = await _bills.GetByOrderIdAsync(orderId, cancellationToken);
            var retryActive = retryBills.FirstOrDefault(b => b.Status != BillState.Cancelled);
            if (retryActive != null)
            {
                var retryAllocations = await _splitDesigns.GetAllocationsByBillIdAsync(retryActive.Id, cancellationToken);
                return Map(retryActive, retryAllocations, canMutate);
            }
            throw;
        }

        if (_dataSource != null && order.TableId.HasValue && order.TableId.Value != Guid.Empty)
        {
            try
            {
                await using var cmd = _dataSource.CreateCommand(
                    """
                    UPDATE table_mgmt.tables
                    SET current_bill_id = @bill_id,
                        row_version = row_version + 1
                    WHERE table_id = @table_id;
                    """);
                cmd.Parameters.AddWithValue("bill_id", billId);
                cmd.Parameters.AddWithValue("table_id", order.TableId.Value);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch
            {
                // Non-fatal table pointer sync
            }
        }

        return Map(bill, [], canMutate);
    }

    public async Task<BillSplitDesignDto> GetAsync(
        Guid billId,
        bool canMutate,
        CancellationToken cancellationToken = default)
    {
        var bill = await GetBillAsync(billId, cancellationToken);
        var allocations = await _splitDesigns.GetAllocationsByBillIdAsync(billId, cancellationToken);
        return Map(bill, allocations, canMutate);
    }

    public async Task<BillSplitDesignDto> SaveEqualAsync(
        Guid billId,
        SaveEqualSplitRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var bill = await GetBillAsync(billId, cancellationToken);
        var owners = RequiredOwners(request.Owners, SplitMode.EqualByPerson, requireUnique: true);
        var allocations = SplitEngine.CreateEqualSplit(bill, owners.Count, owners, actorId);
        return await ReplaceAsync(bill, request.ExpectedBillRowVersion, request.ExpectedAllocations, allocations, cancellationToken);
    }

    public async Task<BillSplitDesignDto> SaveAmountsAsync(
        Guid billId,
        SaveAmountSplitRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var bill = await GetBillAsync(billId, cancellationToken);
        var targets = request.Targets ?? throw new ArgumentException("Amount targets are required.", nameof(request));
        var ownerReferences = targets
            .Select(target => OwnerReference(target.Owner, SplitMode.ByAmount))
            .ToList();
        EnsureUnique(ownerReferences, nameof(request));
        var allocations = SplitEngine.CreateAmountSplit(
            bill,
            targets.Select((target, index) => (ownerReferences[index], target.Amount)).ToList(),
            actorId);
        return await ReplaceAsync(bill, request.ExpectedBillRowVersion, request.ExpectedAllocations, allocations, cancellationToken);
    }

    public async Task<BillSplitDesignDto> SaveItemsAsync(
        Guid billId,
        SaveItemSplitRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var bill = await GetBillAsync(billId, cancellationToken);
        var targets = request.Targets ?? throw new ArgumentException("Item targets are required.", nameof(request));
        var allocations = SplitEngine.CreateItemSplit(
            bill,
            targets.Select(target => new ItemSplitTarget(
                target.BillItemId,
                OwnerReference(target.Owner, SplitMode.ByItem),
                target.Quantity)).ToList(),
            actorId);
        return await ReplaceAsync(bill, request.ExpectedBillRowVersion, request.ExpectedAllocations, allocations, cancellationToken);
    }

    public async Task<BillSplitDesignDto> SaveCustomAsync(
        Guid billId,
        SaveCustomSplitRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var bill = await GetBillAsync(billId, cancellationToken);
        var targets = request.Targets ?? throw new ArgumentException("Custom targets are required.", nameof(request));
        var allocations = SplitEngine.CreateCustomSplit(
            bill,
            targets.Select(target => new CustomSplitTarget(
                OwnerReference(target.Owner, SplitMode.Custom),
                target.Amount,
                target.BillItemId,
                target.Quantity)).ToList(),
            actorId);
        return await ReplaceAsync(bill, request.ExpectedBillRowVersion, request.ExpectedAllocations, allocations, cancellationToken);
    }

    public async Task<BillSplitDesignDto> ClearAsync(
        Guid billId,
        ClearSplitDesignRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var bill = await GetBillAsync(billId, cancellationToken);
        return await ReplaceAsync(
            bill,
            request.ExpectedBillRowVersion,
            request.ExpectedAllocations,
            [],
            cancellationToken);
    }

    private async Task<BillSplitDesignDto> ReplaceAsync(
        Bill bill,
        long? expectedBillRowVersion,
        IReadOnlyList<AllocationVersionRequest>? expectedAllocations,
        IReadOnlyList<BillAllocation> allocations,
        CancellationToken cancellationToken)
    {
        var billVersion = RequiredVersion(expectedBillRowVersion, nameof(expectedBillRowVersion));
        var versions = (expectedAllocations
                ?? throw new ArgumentException("Expected allocation versions are required.", nameof(expectedAllocations)))
            .Select(version => new AllocationVersion(
                version.AllocationId,
                RequiredVersion(version.RowVersion, nameof(version.RowVersion))))
            .ToList();
        var saved = await _splitDesigns.ReplaceOperationalSplitDesignAsync(
            bill.Id,
            billVersion,
            versions,
            allocations,
            cancellationToken);
        return Map(bill, saved.Allocations, canMutate: true, saved.BillRowVersion);
    }

    /// <summary>
    /// The owners a bill can be split between: the persistent seats of the
    /// table behind the bill, plus any person owners that already hold an
    /// allocation. The client used to synthesise placeholder GUIDs
    /// (<c>00000000-0000-0000-0000-00000000000N</c>) for this list; those cannot
    /// be reconciled with the seat-based allocation the server and V1-GOV-017
    /// expect (deep-analysis finding F-2).
    /// </summary>
    public async Task<IReadOnlyList<BillSplitOwnerOptionDto>> GetOwnerOptionsAsync(
        Guid billId,
        CancellationToken cancellationToken = default)
    {
        var bill = await GetBillAsync(billId, cancellationToken);
        var options = new List<BillSplitOwnerOptionDto>();

        if (_dataSource is not null && bill.TableId.HasValue && bill.TableId.Value != Guid.Empty)
        {
            await using var command = _dataSource.CreateCommand(
                """
                SELECT seat_id, seat_number, label
                FROM table_mgmt.table_seats
                WHERE table_id = @table_id
                ORDER BY seat_number;
                """);
            command.Parameters.AddWithValue("table_id", bill.TableId.Value);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                options.Add(new BillSplitOwnerOptionDto(
                    "Seat",
                    reader.GetGuid(0),
                    $"Sandalye {reader.GetInt32(1)}",
                    reader.GetString(2)));
            }
        }

        var allocations = await _splitDesigns.GetAllocationsByBillIdAsync(billId, cancellationToken);
        var seenPersons = new HashSet<Guid>();
        var personIndex = 0;
        foreach (var allocation in allocations)
        {
            if (!OperationalOwnerReference.TryParse(allocation.OwnerReference, out var owner)
                || owner!.Kind != AllocationOwnerKind.Person
                || !seenPersons.Add(owner.Id))
            {
                continue;
            }

            personIndex++;
            options.Add(new BillSplitOwnerOptionDto("Person", owner.Id, $"{personIndex}. Kişi", null));
        }

        return options;
    }

    private async Task<Bill> GetBillAsync(Guid billId, CancellationToken cancellationToken)
    {
        if (billId == Guid.Empty)
            throw new ArgumentException("Bill ID cannot be empty.", nameof(billId));
        return await _bills.GetByIdAsync(billId, cancellationToken)
            ?? throw new BillingSplitNotFoundException($"Bill {billId} was not found.");
    }

    private static List<string> RequiredOwners(
        IReadOnlyList<SplitOwnerRequest>? owners,
        SplitMode mode,
        bool requireUnique)
    {
        if (owners is null)
            throw new ArgumentException("Owners are required.", nameof(owners));
        var values = owners.Select(owner => OwnerReference(owner, mode)).ToList();
        if (requireUnique)
            EnsureUnique(values, nameof(owners));
        return values;
    }

    private static string OwnerReference(SplitOwnerRequest owner, SplitMode mode)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (!Enum.TryParse<AllocationOwnerKind>(owner.Kind, true, out var kind) || !Enum.IsDefined(kind))
            throw new ArgumentException("Owner kind must be Person or Seat.", nameof(owner));
        return OperationalOwnerReference.Format(mode, kind, owner.OwnerId);
    }

    private static void EnsureUnique(List<string> values, string parameterName)
    {
        if (values.Distinct(StringComparer.Ordinal).Count() != values.Count)
            throw new ArgumentException("Owners must be unique for this split mode.", parameterName);
    }

    private static long RequiredVersion(long? value, string parameterName)
    {
        if (value is null or <= 0)
            throw new ArgumentOutOfRangeException(parameterName, "Expected row version must be positive.");
        return value.Value;
    }

    private static BillSplitDesignDto Map(
        Bill bill,
        IReadOnlyList<BillAllocation> allocations,
        bool canMutate,
        long? billRowVersion = null)
    {
        var parsed = allocations.Select(allocation =>
        {
            var recognized = OperationalOwnerReference.TryParse(allocation.OwnerReference, out var owner);
            return new BillSplitAllocationDto(
                allocation.Id,
                recognized ? owner!.Mode.ToString() : SplitMode.Custom.ToString(),
                recognized ? owner!.Kind.ToString() : "Legacy",
                recognized ? owner!.Id : null,
                recognized ? null : allocation.OwnerReference,
                allocation.BillItemId,
                allocation.AllocatedQuantity,
                allocation.AllocatedAmount,
                allocation.TaxAmount,
                allocation.RowVersion);
        }).ToList();

        var modes = parsed.Select(allocation => allocation.Mode).Distinct(StringComparer.Ordinal).ToList();
        var mode = parsed.Count == 0 ? "None" : modes.Count == 1 ? modes[0] : SplitMode.Custom.ToString();
        var mutable = bill.Status is BillState.Open
            or BillState.PartiallyAllocated
            or BillState.Allocated
            or BillState.Reopened;

        return new BillSplitDesignDto(
            bill.Id,
            bill.BillNumber,
            bill.Status.ToString(),
            bill.CurrencyCode,
            bill.PayableAmount,
            bill.TaxTotal,
            billRowVersion ?? bill.RowVersion,
            mode,
            "DesignOnly",
            canMutate && mutable ? ["SaveEqual", "SaveItems", "SaveAmounts", "SaveCustom", "Clear"] : [],
            bill.Items.Select(item => new BillSplitItemDto(
                item.Id,
                item.ProductNameSnapshot,
                item.Quantity,
                item.GrossAmount,
                item.TaxAmount,
                item.RowVersion)).ToList(),
            parsed);
    }
}

public sealed class BillingSplitNotFoundException : Exception
{
    public BillingSplitNotFoundException(string message) : base(message) { }
}
