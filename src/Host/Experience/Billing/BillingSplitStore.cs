using ALKAROS.Billing.Adjustments;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Billing.SplitDesign;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Settings.GarsonFeatureToggles;
using ALKAROS.Settings.TypedSettings;
using Npgsql;

namespace ALKAROS.Host.Experience.Billing;

public sealed class BillingSplitStore
{
    private readonly IBillRepository _bills;
    private readonly ISplitDesignRepository _splitDesigns;
    private readonly IOrderRepository? _orders;
    private readonly NpgsqlDataSource? _dataSource;
    private readonly IBillAdjustmentRepository? _adjustments;
    private readonly ISettingsService? _settings;

    public BillingSplitStore(
        IBillRepository bills,
        ISplitDesignRepository splitDesigns,
        IOrderRepository? orders = null,
        NpgsqlDataSource? dataSource = null,
        IBillAdjustmentRepository? adjustments = null,
        ISettingsService? settings = null)
    {
        _bills = bills ?? throw new ArgumentNullException(nameof(bills));
        _splitDesigns = splitDesigns ?? throw new ArgumentNullException(nameof(splitDesigns));
        _orders = orders;
        _dataSource = dataSource;
        _adjustments = adjustments;
        _settings = settings;
    }

    /// <summary>
    /// V1-RMD-103 (B1): applies a bill-level discount. Domain-complete since
    /// V1-BIL-003 (AdjustmentCalculator, BillAdjustment, migration 021) but
    /// found by an independent audit (2026-09-05) to have zero DI
    /// registration, zero callers, and no HTTP endpoint — a discount could
    /// not be entered anywhere. Validated against
    /// <see cref="AdjustmentCalculator"/> before persisting (check-before-
    /// write), not after.
    /// </summary>
    public async Task<(BillAdjustment Adjustment, AdjustedBillSummary Summary)> ApplyDiscountAsync(
        Guid billId,
        ApplyBillDiscountRequestV1 request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (_adjustments == null)
            throw new InvalidOperationException("Bill adjustment repository is not configured.");
        if (_dataSource == null)
            throw new InvalidOperationException("Data source is not configured.");
        ArgumentNullException.ThrowIfNull(request);

        var bill = await _bills.GetByIdAsync(billId, cancellationToken)
            ?? throw new BillingSplitNotFoundException($"Bill {billId} was not found.");

        // Found by an independent audit (2026-09-06): nothing stopped a
        // discount from being applied to a Paid/PartiallyPaid/Cancelled
        // bill — money already collected could be discounted after the
        // fact. Same mutable-state set SplitDesign already enforces (see
        // BillingSplitDesignDto.Map's `mutable` check).
        if (!IsDiscountable(bill.Status))
            throw new BillDiscountUnsupportedBillStateException(billId, bill.Status.ToString());

        if (!DiscountReasonCatalog.IsValid(request.ReasonCode))
            throw new ArgumentException(
                $"Reason '{request.ReasonCode}' is not a valid discount catalog reason.", nameof(request));

        // Found by an independent audit (2026-09-06): a bare read-validate-
        // write here let two concurrent discount requests both read the
        // same (empty) adjustment set and both pass validation, letting
        // total discounts exceed the payable amount. A FOR UPDATE lock on
        // the bill row serializes the read-validate-write window across
        // concurrent callers for the same bill; AddAsync still commits on
        // its own connection, but by the time this lock releases that
        // insert is already visible to the next caller's read.
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var lockTransaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var lockCommand = new NpgsqlCommand(
            "SELECT bill_id FROM billing.bills WHERE bill_id = @bill_id FOR UPDATE;", connection, lockTransaction))
        {
            lockCommand.Parameters.AddWithValue("bill_id", billId);
            await lockCommand.ExecuteScalarAsync(cancellationToken);
        }

        var existingAdjustments = await _adjustments.GetByBillIdAsync(billId, cancellationToken);

        // V1-RMD-112: a retried request (network timeout, double-submit)
        // with the same IdempotencyKey must not append a second discount
        // line. Unlike Order items, an adjustment row has no natural
        // row-version guard to fall back on — /comp and /void-sent are
        // "accidentally" safe on retry only because their commands carry
        // an ExpectedRowVersion, which this endpoint's request never did.
        // The FOR UPDATE lock already held above serializes this check
        // against a concurrent identical retry for the same bill.
        var replay = existingAdjustments.FirstOrDefault(
            a => string.Equals(a.IdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal));
        if (replay is not null)
        {
            await lockTransaction.CommitAsync(cancellationToken);
            return (replay, AdjustmentCalculator.Calculate(bill, existingAdjustments));
        }

        // A bill's items can carry different tax rates; the discount is
        // split net/tax using the bill's own effective (weighted-average)
        // rate rather than an arbitrary single item's rate.
        var netBase = bill.PayableAmount - bill.TaxTotal;
        var effectiveTaxRate = netBase > 0 ? BillMath.RoundCurrency(bill.TaxTotal / netBase * 100m) : 0m;

        var adjustmentId = Guid.NewGuid();
        var adjustment = request.CalculationType switch
        {
            "Percentage" => BillAdjustment.CreateDiscountPercentage(
                adjustmentId, billId, request.Value, bill.PayableAmount, effectiveTaxRate,
                request.ReasonCode, actorId, notes: request.Notes, createdBy: actorId,
                idempotencyKey: request.IdempotencyKey),
            "FixedAmount" => BillAdjustment.CreateDiscountAmount(
                adjustmentId, billId, request.Value, effectiveTaxRate,
                request.ReasonCode, actorId, notes: request.Notes, createdBy: actorId,
                idempotencyKey: request.IdempotencyKey),
            _ => throw new ArgumentException(
                $"Unknown discount calculation type '{request.CalculationType}'.", nameof(request)),
        };

        // Validate against the full candidate set BEFORE writing anything —
        // AdjustmentCalculator throws if the total discount would exceed the
        // bill's payable amount.
        var summary = AdjustmentCalculator.Calculate(bill, [.. existingAdjustments, adjustment]);

        // Must use the lock's own connection/transaction: the insert's FK
        // reference to billing.bills otherwise waits on the very lock this
        // method holds, from a second, uncommitted connection — a
        // self-deadlock (found while testing this fix).
        await _adjustments.AddAsync(adjustment, connection, lockTransaction, cancellationToken);
        await lockTransaction.CommitAsync(cancellationToken);

        return (adjustment, summary);
    }

    private static bool IsDiscountable(BillState status) => status
        is BillState.Open or BillState.PartiallyAllocated or BillState.Allocated or BillState.Reopened;

    /// <summary>
    /// V1-WTR-020: records a voluntary tip (<see cref="BillAdjustment.CreateTip"/>).
    /// Same lock/idempotency/status-gate shape as <see cref="ApplyDiscountAsync"/>
    /// above — deliberately reusing <see cref="IsDiscountable"/>'s status set
    /// rather than a second copy of it, since "can this bill still take an
    /// adjustment line" is the same question for a tip as for a discount.
    /// No <see cref="AdjustmentCalculator"/> upper-bound check is needed here
    /// (unlike a discount, a tip can never make the adjusted total negative).
    /// </summary>
    public async Task<(BillAdjustment Adjustment, AdjustedBillSummary Summary)> ApplyTipAsync(
        Guid billId,
        ApplyBillTipRequestV1 request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (_adjustments == null)
            throw new InvalidOperationException("Bill adjustment repository is not configured.");
        if (_dataSource == null)
            throw new InvalidOperationException("Data source is not configured.");
        ArgumentNullException.ThrowIfNull(request);
        if (request.Amount <= 0)
            throw new ArgumentException("Tip amount must be positive.", nameof(request));
        // V1-SET-004: this deployment turned the voluntary-tip line off.
        // Refused outright, not silently zeroed — unlike the tolerant
        // optional-field handling elsewhere, an explicit tip call when the
        // feature is off can only mean a client still showing the old UI.
        if (_settings is not null
            && !await GarsonFeatureToggles.IsEnabledAsync(_settings, GarsonFeature.VoluntaryTip, cancellationToken))
            throw new GarsonFeatureDisabledException(GarsonFeature.VoluntaryTip);

        var bill = await _bills.GetByIdAsync(billId, cancellationToken)
            ?? throw new BillingSplitNotFoundException($"Bill {billId} was not found.");
        if (!IsDiscountable(bill.Status))
            throw new BillDiscountUnsupportedBillStateException(billId, bill.Status.ToString());

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var lockTransaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var lockCommand = new NpgsqlCommand(
            "SELECT bill_id FROM billing.bills WHERE bill_id = @bill_id FOR UPDATE;", connection, lockTransaction))
        {
            lockCommand.Parameters.AddWithValue("bill_id", billId);
            await lockCommand.ExecuteScalarAsync(cancellationToken);
        }

        var existingAdjustments = await _adjustments.GetByBillIdAsync(billId, cancellationToken);

        var replay = existingAdjustments.FirstOrDefault(
            a => string.Equals(a.IdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal));
        if (replay is not null)
        {
            await lockTransaction.CommitAsync(cancellationToken);
            return (replay, AdjustmentCalculator.Calculate(bill, existingAdjustments));
        }

        // A fixed code, not free text - matches how DiscountReasonCatalog's
        // codes work: the server stores a code, a future client translates
        // it (UI_STYLE_GUIDE's translation-dictionary rule). A tip needs no
        // business-reason catalog since it is never discretionary, but the
        // stored value still stays in the same "code, not prose" shape.
        var adjustment = BillAdjustment.CreateTip(
            Guid.NewGuid(),
            billId,
            request.Amount,
            reason: "VOLUNTARY_TIP",
            authorizedBy: actorId,
            notes: request.Notes,
            createdBy: actorId,
            idempotencyKey: request.IdempotencyKey);

        var summary = AdjustmentCalculator.Calculate(bill, [.. existingAdjustments, adjustment]);

        await _adjustments.AddAsync(adjustment, connection, lockTransaction, cancellationToken);
        await lockTransaction.CommitAsync(cancellationToken);

        return (adjustment, summary);
    }

    public async Task<(IReadOnlyList<BillAdjustment> Adjustments, AdjustedBillSummary Summary)> GetAdjustmentsAsync(
        Guid billId,
        CancellationToken cancellationToken = default)
    {
        if (_adjustments == null)
            throw new InvalidOperationException("Bill adjustment repository is not configured.");

        var bill = await _bills.GetByIdAsync(billId, cancellationToken)
            ?? throw new BillingSplitNotFoundException($"Bill {billId} was not found.");
        var adjustments = await _adjustments.GetByBillIdAsync(billId, cancellationToken);
        var summary = AdjustmentCalculator.Calculate(bill, adjustments);
        return (adjustments, summary);
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
        // Narrowed from a bare catch (Exception) — found by an independent
        // audit (2026-09-05, H1): this used to swallow ANY exception
        // (validation bugs, connection faults, a defect in AddAsync) on a
        // financial write path and silently hand back whatever bill already
        // existed for the order as if the request had succeeded. bill_number
        // is deterministic per order ($"BILL-{order.OrderNumber}"), so the
        // only expected concurrent failure here is two requests racing to
        // insert the same bill_number for the same order.
        catch (PostgresException ex)
            when (ex.SqlState == PostgresErrorCodes.UniqueViolation && ex.ConstraintName == "bills_bill_number_key")
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
            catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException)
            {
                // table_mgmt.tables.current_bill_id is a soft cache; a failed
                // write here does not fail the bill creation and is repaired by
                // PostgresTablePointerProjector's drift detection. Only expected
                // transport/state faults are swallowed; nothing else.
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
        var adjustment = await LoadAdjustmentAsync(bill, cancellationToken);
        var allocations = SplitEngine.CreateEqualSplit(bill, owners.Count, owners, actorId, adjustment);
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
        var adjustment = await LoadAdjustmentAsync(bill, cancellationToken);
        var allocations = SplitEngine.CreateAmountSplit(
            bill,
            targets.Select((target, index) => (ownerReferences[index], target.Amount)).ToList(),
            actorId,
            adjustment);
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
        var adjustment = await LoadAdjustmentAsync(bill, cancellationToken);
        var allocations = SplitEngine.CreateCustomSplit(
            bill,
            targets.Select(target => new CustomSplitTarget(
                OwnerReference(target.Owner, SplitMode.Custom),
                target.Amount,
                target.BillItemId,
                target.Quantity)).ToList(),
            actorId,
            adjustment);
        return await ReplaceAsync(bill, request.ExpectedBillRowVersion, request.ExpectedAllocations, allocations, cancellationToken);
    }

    /// <summary>
    /// V1-RMD-298 (independent 2026-09-26 audit, finding K1): loads the discount/tip-adjusted payable/tax
    /// totals for a split calculation, or <c>null</c> when no adjustment repository is configured (this
    /// store's own established optional-dependency pattern — see <see cref="_adjustments"/>) so the split
    /// engine falls back to the Bill's own unadjusted totals rather than throwing.
    /// </summary>
    private async Task<AdjustedBillSummary?> LoadAdjustmentAsync(Bill bill, CancellationToken cancellationToken)
    {
        if (_adjustments is null)
            return null;

        var adjustments = await _adjustments.GetByBillIdAsync(bill.Id, cancellationToken);
        return AdjustmentCalculator.Calculate(bill, adjustments);
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

public sealed class BillDiscountUnsupportedBillStateException : Exception
{
    public BillDiscountUnsupportedBillStateException(Guid billId, string state)
        : base($"Bill {billId} is in state '{state}' and does not accept new discounts.")
    {
        BillId = billId;
        State = state;
    }

    public Guid BillId { get; }
    public string State { get; }
}
