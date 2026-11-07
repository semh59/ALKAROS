using System.Text.Json;
using ALKAROS.Audit.EventStore;
using ALKAROS.Billing.Adjustments;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Billing.SplitDesign;
using ALKAROS.Identity.Authorization.Grants;
using ALKAROS.Orders.ItemExceptions;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Settings.GarsonFeatureToggles;
using ALKAROS.Settings.TypedSettings;
using ALKAROS.Tables.TableRelease;
using Npgsql;

namespace ALKAROS.Host.Experience.Billing;

public sealed class BillingSplitStore
{
    private const int MaxMergedOrders = 20;

    private readonly IBillRepository _bills;
    private readonly ISplitDesignRepository _splitDesigns;
    private readonly IOrderRepository? _orders;
    private readonly NpgsqlDataSource? _dataSource;
    private readonly IBillAdjustmentRepository? _adjustments;
    private readonly ISettingsService? _settings;
    private readonly ITableReleasePort? _tables;

    public BillingSplitStore(
        IBillRepository bills,
        ISplitDesignRepository splitDesigns,
        IOrderRepository? orders = null,
        NpgsqlDataSource? dataSource = null,
        IBillAdjustmentRepository? adjustments = null,
        ISettingsService? settings = null,
        ITableReleasePort? tables = null)
    {
        _bills = bills ?? throw new ArgumentNullException(nameof(bills));
        _splitDesigns = splitDesigns ?? throw new ArgumentNullException(nameof(splitDesigns));
        _orders = orders;
        _dataSource = dataSource;
        _adjustments = adjustments;
        _settings = settings;
        _tables = tables;
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
        IAuditEventStore auditEvents,
        string correlationId,
        Guid? approverId = null,
        Guid? grantId = null,
        bool complimentary = false,
        CancellationToken cancellationToken = default)
    {
        if (_adjustments == null)
            throw new InvalidOperationException("Bill adjustment repository is not configured.");
        if (_dataSource == null)
            throw new InvalidOperationException("Data source is not configured.");
        ValidateDiscountRequest(request, complimentary);

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

        // Read only after the lock: a status or payable changed by a request that held it first must be seen.
        var bill = await _bills.GetByIdAsync(billId, cancellationToken)
            ?? throw new BillingSplitNotFoundException($"Bill {billId} was not found.");

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
            if (!SameDiscount(replay, request))
                throw new IdempotencyKeyReusedException(request.IdempotencyKey);
            await lockTransaction.CommitAsync(cancellationToken);
            return (replay, AdjustmentCalculator.Calculate(bill, existingAdjustments));
        }

        // A replay of an already-applied request is answered before the state check: the bill may have been paid since
        // the first attempt succeeded, and a retry must still learn that it was applied (audit 2026-09-06: nothing
        // else may be applied to a Paid/PartiallyPaid/Cancelled bill).
        if (!IsDiscountable(bill.Status))
            throw new BillDiscountUnsupportedBillStateException(billId, bill.Status.ToString());

        var adjustment = CreateDiscount(bill, request, approverId ?? actorId, actorId);

        // Validate against the full candidate set BEFORE writing anything —
        // AdjustmentCalculator throws if the total discount would exceed the
        // bill's payable amount.
        var summary = AdjustmentCalculator.Calculate(bill, [.. existingAdjustments, adjustment]);

        // V1-RMD-410 (V1-RMD-393 F-05): payments do not change the bill's status until it closes, so a partly paid
        // bill still looks discountable — and a discount could push the payable below what was already collected
        // (80 taken, 30 off -> 70 payable). The allocation path's own per-bill lock, taken here in this same
        // transaction, serializes against a concurrent tender (which reads adjustments under that lock too).
        await using (var allocationLock = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtext($1)::bigint);", connection, lockTransaction))
        {
            allocationLock.Parameters.AddWithValue($"payment-allocation:{billId:N}");
            await allocationLock.ExecuteNonQueryAsync(cancellationToken);
        }
        var collected = await ReadCollectedAsync(connection, lockTransaction, billId, cancellationToken);
        if (summary.AdjustedPayableAmount < collected)
            throw new BillDiscountBelowCollectedException(billId, summary.AdjustedPayableAmount, collected);

        // Must use the lock's own connection/transaction: the insert's FK
        // reference to billing.bills otherwise waits on the very lock this
        // method holds, from a second, uncommitted connection — a
        // self-deadlock (found while testing this fix).
        await _adjustments.AddAsync(adjustment, connection, lockTransaction, cancellationToken);
        await BumpBillRowVersionAsync(connection, lockTransaction, billId, cancellationToken);
        await auditEvents.AppendAsync(
            new AuditEvent(
                id: Guid.NewGuid(),
                eventName: complimentary ? "bill.comp.applied" : "bill.discount.applied",
                aggregateType: "Bill",
                aggregateId: billId,
                actorType: "User",
                correlationId: correlationId,
                actorId: actorId,
                reason: request.ReasonCode,
                beforeStateJson: JsonSerializer.Serialize(new { payableAmount = summary.OriginalPayableAmount }),
                afterStateJson: JsonSerializer.Serialize(new
                {
                    payableAmount = summary.AdjustedPayableAmount,
                    discountId = adjustment.Id,
                    discountValue = request.Value,
                    approverUserId = approverId,
                    grantId,
                })),
            connection, lockTransaction, cancellationToken);
        await lockTransaction.CommitAsync(cancellationToken);

        return (adjustment, summary);
    }

    /// <summary>A bill's items can carry different tax rates; the discount is split net/tax in the bill's own weighted-average rate.</summary>
    private static BillAdjustment CreateDiscount(Bill bill, ApplyBillDiscountRequestV1 request, Guid authorizedBy, Guid actorId)
        => request.CalculationType switch
        {
            "Percentage" => BillAdjustment.CreateDiscountPercentage(
                Guid.NewGuid(), bill.Id, request.Value, bill.PayableAmount, bill.EffectiveTaxRatePercent,
                request.ReasonCode, authorizedBy, notes: request.Notes, createdBy: actorId,
                idempotencyKey: request.IdempotencyKey),
            "FixedAmount" => BillAdjustment.CreateDiscountAmount(
                Guid.NewGuid(), bill.Id, request.Value, bill.EffectiveTaxRatePercent,
                request.ReasonCode, authorizedBy, notes: request.Notes, createdBy: actorId,
                idempotencyKey: request.IdempotencyKey),
            _ => throw new ArgumentException(
                $"Unknown discount calculation type '{request.CalculationType}'.", nameof(request)),
        };

    private static async Task<decimal> ReadCollectedAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid billId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT COALESCE(SUM(amount), 0) FROM payments.payment_allocations WHERE bill_id = @bill_id;",
            connection, transaction);
        command.Parameters.AddWithValue("bill_id", billId);
        return (decimal)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static bool IsDiscountable(BillState status) => status
        is BillState.Open or BillState.PartiallyAllocated or BillState.Allocated or BillState.Reopened;

    /// <summary>Above anything a bill can hold and well inside NUMERIC(18,2); keeps a typo from reaching the database.</summary>
    public const decimal MaxAdjustmentAmount = 1_000_000m;

    public const int MaxNotesLength = 500;

    private const int MaxIdempotencyKeyLength = 128;

    private static void ValidateKeyAndNotes(string? idempotencyKey, string? notes)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > MaxIdempotencyKeyLength)
            throw new BillingValidationException(
                "IDEMPOTENCY_KEY_INVALID", "An idempotency key of at most 128 characters is required.",
                "İşlem anahtarı boş olamaz ve 128 karakteri aşamaz.");
        if (notes is { Length: > MaxNotesLength })
            throw new BillingValidationException(
                "NOTES_TOO_LONG", $"Notes may be at most {MaxNotesLength} characters.",
                $"Not en çok {MaxNotesLength} karakter olabilir.");
    }

    /// <summary>Everything that can be judged from the request alone, shared by the grant request and the apply path.</summary>
    public static void ValidateDiscountRequest(ApplyBillDiscountRequestV1 request, bool complimentary = false)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateKeyAndNotes(request.IdempotencyKey, request.Notes);
        if (!(complimentary
                ? ComplimentaryReasonCatalog.AllReasons.Any(reason => string.Equals(reason, request.ReasonCode, StringComparison.Ordinal))
                : DiscountReasonCatalog.IsValid(request.ReasonCode)))
            throw new BillingValidationException(
                "DISCOUNT_REASON_INVALID", $"Reason '{request.ReasonCode}' is not a valid {(complimentary ? "complimentary" : "discount")} catalog reason.",
                complimentary ? "İkram gerekçesi geçerli değil." : "İndirim gerekçesi geçerli değil.");
        var upper = request.CalculationType switch
        {
            "Percentage" => 100m,
            "FixedAmount" => MaxAdjustmentAmount,
            _ => throw new BillingValidationException(
                "DISCOUNT_TYPE_INVALID", $"Unknown discount calculation type '{request.CalculationType}'.",
                "İndirim türü yüzde ya da tutar olmalı."),
        };
        if (request.Value <= 0m || request.Value > upper)
            throw new BillingValidationException(
                "DISCOUNT_VALUE_INVALID", $"Discount value must be above 0 and at most {upper}.",
                request.CalculationType == "Percentage"
                    ? "İndirim yüzdesi 0'dan büyük, en çok 100 olmalı."
                    : $"İndirim tutarı 0'dan büyük, en çok {MaxAdjustmentAmount:N0} olmalı.");
    }

    /// <summary>
    /// Refuses a discount or comp the bill would refuse anyway (missing or closed bill, more than the payable, below what was
    /// already collected) before any grant is opened for it; a retry of an already applied request passes.
    /// </summary>
    public async Task EnsureDiscountRequestableAsync(
        Guid billId, ApplyBillDiscountRequestV1 request, Guid actorId, bool complimentary = false,
        CancellationToken cancellationToken = default)
    {
        ValidateDiscountRequest(request, complimentary);
        var bill = await _bills.GetByIdAsync(billId, cancellationToken)
            ?? throw new BillingSplitNotFoundException($"Bill {billId} was not found.");
        var existing = await _adjustments!.GetByBillIdAsync(billId, cancellationToken);

        // A retry of a discount that was already applied is answered by ApplyDiscountAsync, even after the bill was paid.
        if (existing.Any(a => string.Equals(a.IdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal)))
            return;
        if (!IsDiscountable(bill.Status))
            throw new BillDiscountUnsupportedBillStateException(billId, bill.Status.ToString());

        // A request the bill would refuse anyway must not spend one of the requester's approvals first.
        var summary = AdjustmentCalculator.Calculate(bill, [.. existing, CreateDiscount(bill, request, actorId, actorId)]);
        await using var connection = await _dataSource!.OpenConnectionAsync(cancellationToken);
        var collected = await ReadCollectedAsync(connection, null, billId, cancellationToken);
        if (summary.AdjustedPayableAmount < collected)
            throw new BillDiscountBelowCollectedException(billId, summary.AdjustedPayableAmount, collected);
    }

    private static bool SameDiscount(BillAdjustment stored, ApplyBillDiscountRequestV1 request)
        => request.CalculationType switch
        {
            "Percentage" => stored.AdjustmentType == AdjustmentType.DiscountPercentage && stored.Rate == request.Value,
            _ => stored.AdjustmentType == AdjustmentType.DiscountAmount
                 && stored.Amount == BillMath.RoundCurrency(request.Value),
        } && stored.Reason == request.ReasonCode && stored.Notes == request.Notes;

    /// <summary>A discount or tip changes what the bill is worth, so a split saved against the old version must be refused.</summary>
    private static async Task BumpBillRowVersionAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid billId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "UPDATE billing.bills SET row_version = row_version + 1, updated_at = now() WHERE bill_id = @bill_id;",
            connection, transaction);
        command.Parameters.AddWithValue("bill_id", billId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

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
        IAuditEventStore auditEvents,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (_adjustments == null)
            throw new InvalidOperationException("Bill adjustment repository is not configured.");
        if (_dataSource == null)
            throw new InvalidOperationException("Data source is not configured.");
        ArgumentNullException.ThrowIfNull(request);
        ValidateKeyAndNotes(request.IdempotencyKey, request.Notes);
        if (request.Amount <= 0 || request.Amount > MaxAdjustmentAmount)
            throw new BillingValidationException(
                "TIP_AMOUNT_INVALID", $"Tip amount must be above 0 and at most {MaxAdjustmentAmount}.",
                $"Bahşiş tutarı 0'dan büyük, en çok {MaxAdjustmentAmount:N0} olmalı.");
        // V1-SET-004: this deployment turned the voluntary-tip line off.
        // Refused outright, not silently zeroed — unlike the tolerant
        // optional-field handling elsewhere, an explicit tip call when the
        // feature is off can only mean a client still showing the old UI.
        if (_settings is not null
            && !await GarsonFeatureToggles.IsEnabledAsync(_settings, GarsonFeature.VoluntaryTip, cancellationToken))
            throw new GarsonFeatureDisabledException(GarsonFeature.VoluntaryTip);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var lockTransaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var lockCommand = new NpgsqlCommand(
            "SELECT bill_id FROM billing.bills WHERE bill_id = @bill_id FOR UPDATE;", connection, lockTransaction))
        {
            lockCommand.Parameters.AddWithValue("bill_id", billId);
            await lockCommand.ExecuteScalarAsync(cancellationToken);
        }
        // Same per-bill lock a tender takes, so a payment cannot slip in between the read and the insert.
        await using (var allocationLock = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtext($1)::bigint);", connection, lockTransaction))
        {
            allocationLock.Parameters.AddWithValue($"payment-allocation:{billId:N}");
            await allocationLock.ExecuteNonQueryAsync(cancellationToken);
        }

        var bill = await _bills.GetByIdAsync(billId, cancellationToken)
            ?? throw new BillingSplitNotFoundException($"Bill {billId} was not found.");

        var existingAdjustments = await _adjustments.GetByBillIdAsync(billId, cancellationToken);

        var replay = existingAdjustments.FirstOrDefault(
            a => string.Equals(a.IdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal));
        if (replay is not null)
        {
            if (replay.AdjustmentType != AdjustmentType.Tip
                || replay.Amount != BillMath.RoundCurrency(request.Amount) || replay.Notes != request.Notes)
                throw new IdempotencyKeyReusedException(request.IdempotencyKey);
            await lockTransaction.CommitAsync(cancellationToken);
            return (replay, AdjustmentCalculator.Calculate(bill, existingAdjustments));
        }

        // A replay of an already-applied request is answered before the state check: the bill may have been paid since
        // the first attempt succeeded, and a retry must still learn that it was applied (audit 2026-09-06: nothing
        // else may be applied to a Paid/PartiallyPaid/Cancelled bill).
        if (!IsDiscountable(bill.Status))
            throw new BillDiscountUnsupportedBillStateException(billId, bill.Status.ToString());

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
        await BumpBillRowVersionAsync(connection, lockTransaction, billId, cancellationToken);
        await auditEvents.AppendAsync(
            new AuditEvent(
                id: Guid.NewGuid(),
                eventName: "bill.tip.applied",
                aggregateType: "Bill",
                aggregateId: billId,
                actorType: "User",
                correlationId: correlationId,
                actorId: actorId,
                reason: "VOLUNTARY_TIP",
                beforeStateJson: JsonSerializer.Serialize(new { payableAmount = summary.OriginalPayableAmount }),
                afterStateJson: JsonSerializer.Serialize(new
                {
                    payableAmount = summary.AdjustedPayableAmount,
                    tipId = adjustment.Id,
                    tipAmount = request.Amount,
                })),
            connection, lockTransaction, cancellationToken);
        await lockTransaction.CommitAsync(cancellationToken);

        return (adjustment, summary);
    }

    /// <summary>
    /// Takes a wrongly entered discount or tip off the bill. The row is deleted (every reader of the adjustments
    /// stays consistent without a filter), and the audit event keeps the removed record in full.
    /// </summary>
    public async Task<AdjustedBillSummary> UndoAdjustmentAsync(
        Guid billId,
        Guid adjustmentId,
        Guid actorId,
        IAuditEventStore auditEvents,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (_adjustments == null)
            throw new InvalidOperationException("Bill adjustment repository is not configured.");
        if (_dataSource == null)
            throw new InvalidOperationException("Data source is not configured.");

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var lockCommand = new NpgsqlCommand(
            "SELECT bill_id FROM billing.bills WHERE bill_id = @bill_id FOR UPDATE;", connection, transaction))
        {
            lockCommand.Parameters.AddWithValue("bill_id", billId);
            await lockCommand.ExecuteScalarAsync(cancellationToken);
        }
        // The per-bill lock a tender and an apply take: a payment cannot land between the collected-amount read and the delete.
        await using (var allocationLock = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtext($1)::bigint);", connection, transaction))
        {
            allocationLock.Parameters.AddWithValue($"payment-allocation:{billId:N}");
            await allocationLock.ExecuteNonQueryAsync(cancellationToken);
        }

        var bill = await _bills.GetByIdAsync(billId, cancellationToken)
            ?? throw new BillingSplitNotFoundException($"Bill {billId} was not found.");
        var existing = await _adjustments.GetByBillIdAsync(billId, cancellationToken);
        var target = existing.FirstOrDefault(a => a.Id == adjustmentId)
            ?? throw new BillingSplitNotFoundException($"Adjustment {adjustmentId} was not found on bill {billId}.", "düzeltme");

        if (!IsDiscountable(bill.Status))
            throw new AdjustmentUndoBillStateException(billId, bill.Status.ToString());
        if (target.AdjustmentType is not (AdjustmentType.DiscountPercentage or AdjustmentType.DiscountAmount or AdjustmentType.Tip))
            throw new BillingValidationException(
                "ADJUSTMENT_NOT_UNDOABLE", $"Adjustment type {target.AdjustmentType} cannot be undone.",
                "Yalnızca indirim ve bahşiş geri alınabilir.");

        var before = AdjustmentCalculator.Calculate(bill, existing);
        var summary = AdjustmentCalculator.Calculate(bill, existing.Where(a => a.Id != adjustmentId).ToList());
        decimal collected;
        await using (var collectedCommand = new NpgsqlCommand(
            "SELECT COALESCE(SUM(amount), 0) FROM payments.payment_allocations WHERE bill_id = @bill_id;",
            connection, transaction))
        {
            collectedCommand.Parameters.AddWithValue("bill_id", billId);
            collected = (decimal)(await collectedCommand.ExecuteScalarAsync(cancellationToken))!;
        }
        if (summary.AdjustedPayableAmount < collected)
            throw new AdjustmentUndoBelowCollectedException(billId, summary.AdjustedPayableAmount, collected);

        if (!await _adjustments.DeleteAsync(billId, adjustmentId, connection, transaction, cancellationToken))
            throw new BillingSplitNotFoundException($"Adjustment {adjustmentId} was not found on bill {billId}.", "düzeltme");
        await BumpBillRowVersionAsync(connection, transaction, billId, cancellationToken);
        await auditEvents.AppendAsync(
            new AuditEvent(
                id: Guid.NewGuid(),
                eventName: "bill.adjustment.undone",
                aggregateType: "Bill",
                aggregateId: billId,
                actorType: "User",
                correlationId: correlationId,
                actorId: actorId,
                reason: target.Reason,
                beforeStateJson: JsonSerializer.Serialize(new
                {
                    payableAmount = before.AdjustedPayableAmount,
                    adjustmentId = target.Id,
                    adjustmentType = target.AdjustmentType.ToString(),
                    calculationType = target.CalculationType.ToString(),
                    amount = target.Amount,
                    reason = target.Reason,
                    notes = target.Notes,
                    authorizedBy = target.AuthorizedBy,
                    createdBy = target.CreatedBy,
                    createdAt = target.CreatedAt,
                }),
                afterStateJson: JsonSerializer.Serialize(new { payableAmount = summary.AdjustedPayableAmount })),
            connection, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return summary;
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
            return Map(activeBill, existingAllocations, canMutate, adjustment: await LoadAdjustmentAsync(activeBill, cancellationToken));
        }

        var order = await _orders.GetByIdAsync(orderId, cancellationToken)
            ?? throw new BillingSplitNotFoundException($"Order {orderId} was not found.", "sipariş");

        if (order.Status is OrderState.Cancelled or OrderState.Rejected or OrderState.Draft)
            throw new UnsupportedOrderStateException($"Cannot create a bill from an order in '{order.Status}' state.");

        var billId = Guid.NewGuid();
        var billNumber = $"BILL-{order.OrderNumber}";
        var bill = Bill.FromOrder(billId, billNumber, order);

        try
        {
            // The bill and the table's pointer to it commit together: a bill the table does not point at is invisible
            // to the table screens, a pointer to a bill that was not saved is a ghost.
            if (_dataSource is not null && _tables is not null && order.TableId is { } tableId && tableId != Guid.Empty)
            {
                await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                await _bills.AddAsync(bill, connection, transaction, cancellationToken);
                await _tables.AttachBillPointerAsync(tableId, billId, connection, transaction, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                await _bills.AddAsync(bill, cancellationToken);
            }
        }
        // Narrowed from a bare catch (Exception) — found by an independent
        // audit (2026-09-05, H1): this used to swallow ANY exception
        // (validation bugs, connection faults, a defect in AddAsync) on a
        // financial write path and silently hand back whatever bill already
        // existed for the order as if the request had succeeded. bill_number
        // is deterministic per order ($"BILL-{order.OrderNumber}"), so the
        // expected concurrent failures here are two requests racing to
        // insert the same bill_number for the same order, or a merged bill taking the order items first.
        catch (PostgresException ex)
            when (ex.SqlState == PostgresErrorCodes.UniqueViolation
                  && ex.ConstraintName is "bills_bill_number_key" or "bill_items_order_item_id_key")
        {
            // If another request concurrently inserted a bill for this order/number, or a merged bill took its items, recover gracefully
            var retryBills = await _bills.GetByOrderIdAsync(orderId, cancellationToken);
            var retryActive = retryBills.FirstOrDefault(b => b.Status != BillState.Cancelled);
            if (retryActive != null)
            {
                var retryAllocations = await _splitDesigns.GetAllocationsByBillIdAsync(retryActive.Id, cancellationToken);
                return Map(retryActive, retryAllocations, canMutate, adjustment: await LoadAdjustmentAsync(retryActive, cancellationToken));
            }
            throw;
        }

        return Map(bill, [], canMutate);
    }

    /// <summary>
    /// Bills several open orders of one table on one bill (the bill carries no order id; its orders are the ones its items come
    /// from). The bill number is derived from the idempotency key, so the same key and orders return the same bill.
    /// </summary>
    public async Task<BillSplitDesignDto> CreateMergedBillAsync(
        Guid terminalId,
        IReadOnlyList<Guid> orderIds,
        string? idempotencyKey,
        bool canMutate,
        CancellationToken cancellationToken = default)
    {
        if (_orders == null || _dataSource == null)
            throw new InvalidOperationException("Order repository and data source are required to merge orders.");

        var ids = (orderIds ?? []).Distinct().Order().ToList();
        if (ids.Count < 2)
            throw new BillingValidationException(
                "MERGE_NEEDS_TWO_ORDERS", "A merge needs at least two distinct orders.", "Birleştirmek için en az iki farklı sipariş seçin.");
        if (ids.Count > MaxMergedOrders)
            throw new BillingValidationException(
                "MERGE_TOO_MANY_ORDERS", $"At most {MaxMergedOrders} orders can be merged.", $"En fazla {MaxMergedOrders} sipariş birleştirilebilir.");
        var key = idempotencyKey?.Trim();
        if (string.IsNullOrEmpty(key) || key.Length > 200)
            throw new BillingValidationException(
                "IDEMPOTENCY_KEY_REQUIRED", "An idempotency key of 1 to 200 characters is required.", "İşlem anahtarı gerekli.");

        var billNumber = "BILL-M-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{terminalId:N}:{key}")))[..24];
        if (await _bills.GetByBillNumberAsync(billNumber, cancellationToken) is { } replay)
            return await ReplayMergedBillAsync(replay, ids, canMutate, cancellationToken);

        var orders = new List<Order>();
        foreach (var id in ids)
            orders.Add(await _orders.GetByIdAsync(id, cancellationToken)
                ?? throw new BillingSplitNotFoundException($"Order {id} was not found.", "sipariş"));
        foreach (var order in orders)
        {
            if (order.Status is not (OrderState.Submitted or OrderState.Accepted or OrderState.Preparing or OrderState.Ready or OrderState.Served))
                throw new UnsupportedOrderStateException($"Cannot merge an order in '{order.Status}' state.");
            if (order.Source is not (OrderSource.Cashier or OrderSource.Waiter))
                throw new BillingValidationException(
                    "MERGE_ORDER_SOURCE", $"Order {order.Id} comes from {order.Source}.", "Yalnız kasa veya garson siparişleri birleştirilebilir.");
        }

        if (orders[0].TableId is not { } tableId || tableId == Guid.Empty || orders.Any(o => o.TableId != tableId))
            throw new BillingValidationException(
                "MERGE_DIFFERENT_TABLES", "The orders are not on one table.", "Yalnız aynı masadaki siparişler birleştirilebilir.");

        foreach (var id in ids)
            if ((await _bills.GetByOrderIdAsync(id, cancellationToken)).Any(b => b.Status != BillState.Cancelled))
                throw new BillMergeConflictException(
                    "MERGE_ORDER_ALREADY_BILLED", $"Order {id} already has a bill.",
                    "Seçilen siparişlerden birinin hesabı zaten açık; önce o hesabı iptal edin.");

        Bill bill;
        try
        {
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await LockAndCheckOrdersAsync(ids, tableId, connection, transaction, cancellationToken);

            // Read again under the lock: an item added or cancelled while the request was running must be on the bill or off it.
            var locked = new List<Order>();
            foreach (var id in ids)
                locked.Add(await PostgresOrderRepository.GetByIdAsync(id, connection, transaction, cancellationToken)
                    ?? throw new BillingSplitNotFoundException($"Order {id} was not found.", "sipariş"));
            foreach (var order in locked)
                if (!order.Items.Any(IsBillable))
                    throw new BillMergeConflictException(
                        "MERGE_ORDER_EMPTY", $"Order {order.Id} has no item to bill.",
                        "Seçilen siparişlerden birinde hesaplanacak ürün yok.");

            bill = BillSourceOperations.CreateMergedBill(
                Guid.NewGuid(), billNumber, locked.SelectMany(o => o.Items).Where(IsBillable).ToList(), tableId);
            await _bills.AddAsync(bill, connection, transaction, cancellationToken);
            if (_tables is not null)
                await _tables.AttachBillPointerAsync(tableId, bill.Id, connection, transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException ex)
            when (ex.SqlState == PostgresErrorCodes.UniqueViolation
                  && ex.ConstraintName is "bills_bill_number_key" or "bill_items_order_item_id_key")
        {
            // Another request took the key or the items first: a replay of the same merge gets its bill, anything else is a conflict.
            if (await _bills.GetByBillNumberAsync(billNumber, cancellationToken) is { } raced)
                return await ReplayMergedBillAsync(raced, ids, canMutate, cancellationToken);
            throw new BillMergeConflictException(
                "MERGE_ORDER_ALREADY_BILLED", "An order was billed by another request.",
                "Seçilen siparişlerden birinin hesabı az önce açıldı; ekranı yenileyin.");
        }

        return Map(bill, [], canMutate);
    }

    /// <summary>The same lines a bill opened from one order takes: not cancelled, and not a round still waiting to be sent.</summary>
    private static bool IsBillable(OrderItem item) => item.Status is not (OrderItemState.Cancelled or OrderItemState.Draft);

    /// <summary>Locks the orders and checks them once more: one cancelled, moved or sent to the till after they were read must not be billed.</summary>
    private static async Task LockAndCheckOrdersAsync(
        List<Guid> ids, Guid tableId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT o.status, o.table_id, EXISTS (SELECT 1 FROM table_mgmt.table_sent_checks sc WHERE sc.order_id = o.order_id)
            FROM orders.orders o WHERE o.order_id = ANY(@ids) ORDER BY o.order_id FOR UPDATE OF o;
            """, connection, transaction);
        command.Parameters.AddWithValue("ids", ids.ToArray());
        var rows = 0;
        var sent = 0;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows++;
            if (reader.GetString(0) is not ("Submitted" or "Accepted" or "Preparing" or "Ready" or "Served"))
                throw new UnsupportedOrderStateException("An order was closed while the merge was running.");
            if (reader.IsDBNull(1) || reader.GetGuid(1) != tableId)
                throw new BillingValidationException(
                    "MERGE_DIFFERENT_TABLES", "The orders are not on one table.", "Yalnız aynı masadaki siparişler birleştirilebilir.");
            if (reader.GetBoolean(2))
                sent++;
        }

        if (rows != ids.Count)
            throw new BillingSplitNotFoundException("An order disappeared while the merge was running.", "sipariş");
        if (sent != 0 && sent != ids.Count)
            throw new BillMergeConflictException(
                "MERGE_MIXED_SENT", "Some of the orders went to the till and some did not.",
                "Seçilen siparişlerin bir kısmı kasaya gönderilmiş, bir kısmı masada; hepsi aynı yerde olmalı.");
    }

    private async Task<BillSplitDesignDto> ReplayMergedBillAsync(
        Bill existing, IReadOnlyList<Guid> orderIds, bool canMutate, CancellationToken cancellationToken)
    {
        var billed = new List<Guid>();
        await using (var command = _dataSource!.CreateCommand(BillOrderLink.OrderIdsOfBill))
        {
            command.Parameters.AddWithValue("bill_id", existing.Id);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                billed.Add(reader.GetGuid(0));
        }

        if (existing.Status == BillState.Cancelled || !billed.ToHashSet().SetEquals(orderIds))
            throw new BillMergeConflictException(
                "IDEMPOTENCY_KEY_REUSED", "The idempotency key belongs to another or a cancelled merge.",
                "Bu işlem anahtarı başka bir birleştirme için veya iptal edilmiş bir birleştirme için kullanılmış; yeni işlem anahtarıyla deneyin.");
        var allocations = await _splitDesigns.GetAllocationsByBillIdAsync(existing.Id, cancellationToken);
        return Map(existing, allocations, canMutate, adjustment: await LoadAdjustmentAsync(existing, cancellationToken));
    }

    public async Task<BillSplitDesignDto> GetAsync(
        Guid billId,
        bool canMutate,
        CancellationToken cancellationToken = default)
    {
        var bill = await GetBillAsync(billId, cancellationToken);
        var allocations = await _splitDesigns.GetAllocationsByBillIdAsync(billId, cancellationToken);
        return Map(bill, allocations, canMutate, adjustment: await LoadAdjustmentAsync(bill, cancellationToken));
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
        // A discount is spread over the items (AdjustmentCalculator.DiscountedLineAmounts); a fee or a tip belongs to
        // no item, so such a bill is split by amount, person or custom split instead, unless the adjustments cancel
        // out and the items' own prices still add up to what is owed.
        var adjustment = await LoadAdjustmentAsync(bill, cancellationToken);
        if (adjustment is not null && (adjustment.TotalFees != 0m || adjustment.TotalTips != 0m))
        {
            if (adjustment.AdjustedPayableAmount != bill.PayableAmount || adjustment.AdjustedTaxTotal != bill.TaxTotal)
                throw new SplitItemsOnAdjustedBillException(billId);
            adjustment = null;
        }

        var allocations = SplitEngine.CreateItemSplit(
            bill,
            targets.Select(target => new ItemSplitTarget(
                target.BillItemId,
                OwnerReference(target.Owner, SplitMode.ByItem),
                target.Quantity)).ToList(),
            actorId,
            adjustment);
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
        return Map(
            bill,
            saved.Allocations,
            canMutate: true,
            saved.BillRowVersion,
            await LoadAdjustmentAsync(bill, cancellationToken));
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
        long? billRowVersion = null,
        AdjustedBillSummary? adjustment = null)
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
            // V1-RMD-413: the totals a design must add up to are the adjusted ones (see SplitEngine).
            adjustment?.AdjustedPayableAmount ?? bill.PayableAmount,
            adjustment?.AdjustedTaxTotal ?? bill.TaxTotal,
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
    public BillingSplitNotFoundException(string message, string resource = "hesap") : base(message)
    {
        Resource = resource;
    }

    /// <summary>What was not found, in the words the screen shows (bill, order, refund request).</summary>
    public string Resource { get; }
}

/// <summary>The order is in a state that cannot be billed (cancelled, rejected): a conflict, not a malformed request.</summary>
public sealed class UnsupportedOrderStateException(string message) : InvalidOperationException(message);

/// <summary>A merge refused because of the current state of the orders or the key: a conflict the screen explains by <see cref="Code"/>.</summary>
public sealed class BillMergeConflictException(string code, string message, string userMessage) : InvalidOperationException(message)
{
    public string Code { get; } = code;

    public string UserMessage { get; } = userMessage;
}

/// <summary>A request the server refuses before doing anything; <see cref="Code"/> and <see cref="UserMessage"/> reach the screen.</summary>
public sealed class BillingValidationException(string code, string message, string userMessage) : ArgumentException(message)
{
    public string Code { get; } = code;

    public string UserMessage { get; } = userMessage;
}

/// <summary>A discount or complimentary request the approval policy refused.</summary>
public sealed class GrantDeniedException(bool complimentary = false) : Exception("The request was denied.")
{
    public bool Complimentary { get; } = complimentary;
}

/// <summary>
/// An item split was requested on a bill with a fee or a tip, which belong to no item.
/// </summary>
public sealed class SplitItemsOnAdjustedBillException : Exception
{
    public SplitItemsOnAdjustedBillException(Guid billId)
        : base($"Bill {billId} carries a fee or a tip; an item split cannot account for it.")
    {
        BillId = billId;
    }

    public Guid BillId { get; }
}

/// <summary>
/// V1-RMD-410 (V1-RMD-393 F-05): the discount would leave the bill's payable below the amount already collected.
/// </summary>
public sealed class BillDiscountBelowCollectedException : Exception
{
    public BillDiscountBelowCollectedException(Guid billId, decimal adjustedPayable, decimal collected)
        : base($"Bill {billId}: a discount leaving {adjustedPayable} payable is below the {collected} already collected.")
    {
        BillId = billId;
        AdjustedPayable = adjustedPayable;
        Collected = collected;
    }

    public Guid BillId { get; }
    public decimal AdjustedPayable { get; }
    public decimal Collected { get; }
}

/// <summary>Taking an adjustment off would leave the payable below what was already collected.</summary>
public sealed class AdjustmentUndoBelowCollectedException(Guid billId, decimal adjustedPayable, decimal collected)
    : Exception($"Bill {billId}: undoing the adjustment leaves {adjustedPayable} payable, below the {collected} already collected.");

/// <summary>The bill is paid, cancelled or otherwise past the point where its adjustments may change.</summary>
public sealed class AdjustmentUndoBillStateException(Guid billId, string status)
    : Exception($"Bill {billId} is {status}; its adjustments can no longer be undone.");

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
