using ALKAROS.Billing.BillFoundation;

namespace ALKAROS.Billing.Adjustments;

/// <summary>
/// A persistent adjustment line attached to a Bill or BillItem (PDF:III.7, V0-DOM-006).
/// Captures approved discounts, service fees, kuver, and tips with mandatory reason and manager authorization.
/// </summary>
public sealed class BillAdjustment
{
    public BillAdjustment(
        Guid id,
        Guid billId,
        AdjustmentType adjustmentType,
        AdjustmentCalculationType calculationType,
        decimal amount,
        decimal netAmount,
        decimal grossAmount,
        string reason,
        Guid authorizedBy,
        decimal? rate = null,
        decimal taxRate = 0m,
        decimal taxAmount = 0m,
        bool? isDeduction = null,
        Guid? billItemId = null,
        string? notes = null,
        DateTimeOffset? createdAt = null,
        Guid? createdBy = null,
        long rowVersion = 1,
        string? idempotencyKey = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Adjustment id cannot be empty.", nameof(id));
        if (billId == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(billId));
        if (billItemId == Guid.Empty)
            throw new ArgumentException("Bill item id cannot be empty GUID when specified.", nameof(billItemId));
        if (amount <= 0)
            throw new ArgumentException("Adjustment amount must be positive.", nameof(amount));
        if (netAmount < 0)
            throw new ArgumentException("Net amount cannot be negative.", nameof(netAmount));
        if (grossAmount < 0)
            throw new ArgumentException("Gross amount cannot be negative.", nameof(grossAmount));
        if (taxRate < 0)
            throw new ArgumentException("Tax rate cannot be negative.", nameof(taxRate));
        if (taxAmount < 0)
            throw new ArgumentException("Tax amount cannot be negative.", nameof(taxAmount));
        if (rate.HasValue && (rate.Value <= 0 || rate.Value > 100))
            throw new ArgumentException("Rate must be between 0 and 100 percent when specified.", nameof(rate));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is mandatory for every adjustment (V0-DOM-006).", nameof(reason));
        if (authorizedBy == Guid.Empty)
            throw new ArgumentException("AuthorizedBy manager ID is mandatory for every adjustment (V0-DOM-006).", nameof(authorizedBy));
        var expectedDeduction = adjustmentType is AdjustmentType.DiscountPercentage or AdjustmentType.DiscountAmount;
        if (isDeduction.HasValue && isDeduction.Value != expectedDeduction)
        {
            throw new ArgumentException(
                $"Adjustment type '{adjustmentType}' cannot have isDeduction={isDeduction.Value}. Expected isDeduction={expectedDeduction}.",
                nameof(isDeduction));
        }

        Id = id;
        BillId = billId;
        BillItemId = billItemId;
        AdjustmentType = adjustmentType;
        CalculationType = calculationType;
        Rate = rate;
        Amount = BillMath.RoundCurrency(amount);
        TaxRate = taxRate;
        TaxAmount = BillMath.RoundCurrency(taxAmount);
        NetAmount = BillMath.RoundCurrency(netAmount);
        GrossAmount = BillMath.RoundCurrency(grossAmount);
        IsDeduction = expectedDeduction;
        Reason = reason;
        AuthorizedBy = authorizedBy;
        Notes = notes;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        CreatedBy = createdBy;
        RowVersion = rowVersion;
        IdempotencyKey = idempotencyKey;
    }

    public Guid Id { get; }

    public Guid BillId { get; }

    public Guid? BillItemId { get; }

    public AdjustmentType AdjustmentType { get; }

    public AdjustmentCalculationType CalculationType { get; }

    public decimal? Rate { get; }

    public decimal Amount { get; }

    public decimal TaxRate { get; }

    public decimal TaxAmount { get; }

    public decimal NetAmount { get; }

    public decimal GrossAmount { get; }

    public bool IsDeduction { get; }

    public string Reason { get; }

    public Guid AuthorizedBy { get; }

    public string? Notes { get; }

    public DateTimeOffset CreatedAt { get; }

    public Guid? CreatedBy { get; }

    public long RowVersion { get; }

    /// <summary>
    /// V1-RMD-112: the caller-supplied idempotency key that produced this
    /// adjustment, when the creating endpoint carries one (currently only
    /// the discount endpoint — <c>POST .../bills/{billId}/discount</c>).
    /// <see cref="BillAdjustment"/> is append-only (unlike Order items, a
    /// retry has no natural row-version guard to fall back on), so this is
    /// the only protection against a network retry inserting a second,
    /// identical adjustment line.
    /// </summary>
    public string? IdempotencyKey { get; }

    /// <summary>
    /// Creates a percentage discount adjustment.
    /// </summary>
    public static BillAdjustment CreateDiscountPercentage(
        Guid id,
        Guid billId,
        decimal rate,
        decimal baseGrossAmount,
        decimal taxRate,
        string reason,
        Guid authorizedBy,
        Guid? billItemId = null,
        string? notes = null,
        Guid? createdBy = null,
        string? idempotencyKey = null)
    {
        if (rate <= 0 || rate > 100)
            throw new ArgumentException("Discount percentage rate must be between 0 and 100.", nameof(rate));
        if (baseGrossAmount <= 0)
            throw new ArgumentException("Base gross amount must be positive to apply discount.", nameof(baseGrossAmount));

        var discountGross = BillMath.RoundCurrency(baseGrossAmount * (rate / 100m));
        var discountTax = BillItem.TaxIncludedIn(discountGross, taxRate);
        var discountNet = discountGross - discountTax;

        return new BillAdjustment(
            id: id,
            billId: billId,
            adjustmentType: AdjustmentType.DiscountPercentage,
            calculationType: AdjustmentCalculationType.Percentage,
            amount: discountGross,
            netAmount: discountNet,
            grossAmount: discountGross,
            reason: reason,
            authorizedBy: authorizedBy,
            rate: rate,
            taxRate: taxRate,
            taxAmount: discountTax,
            isDeduction: true,
            billItemId: billItemId,
            notes: notes,
            createdBy: createdBy,
            idempotencyKey: idempotencyKey);
    }

    /// <summary>
    /// Creates a fixed amount discount adjustment.
    /// </summary>
    public static BillAdjustment CreateDiscountAmount(
        Guid id,
        Guid billId,
        decimal discountAmount,
        decimal taxRate,
        string reason,
        Guid authorizedBy,
        Guid? billItemId = null,
        string? notes = null,
        Guid? createdBy = null,
        string? idempotencyKey = null)
    {
        if (discountAmount <= 0)
            throw new ArgumentException("Discount amount must be positive.", nameof(discountAmount));

        var discountGross = BillMath.RoundCurrency(discountAmount);
        var discountTax = BillItem.TaxIncludedIn(discountGross, taxRate);
        var discountNet = discountGross - discountTax;

        return new BillAdjustment(
            id: id,
            billId: billId,
            adjustmentType: AdjustmentType.DiscountAmount,
            calculationType: AdjustmentCalculationType.FixedAmount,
            amount: discountGross,
            netAmount: discountNet,
            grossAmount: discountGross,
            reason: reason,
            authorizedBy: authorizedBy,
            taxRate: taxRate,
            taxAmount: discountTax,
            isDeduction: true,
            billItemId: billItemId,
            notes: notes,
            createdBy: createdBy,
            idempotencyKey: idempotencyKey);
    }

    /// <summary>
    /// Creates a service fee or kuver adjustment.
    /// </summary>
    /// <remarks>
    /// V1-WTR-020: domain-complete since V1-BIL-003 but, as of this comment,
    /// still never wired to any HTTP endpoint — and it must stay that way. A
    /// regulation effective 2026-01-30 (Resmi Gazete 33153) bans food/
    /// beverage establishments from adding a "servis, masa, kuver veya
    /// benzeri" charge under any name; violations are fined per adisyon
    /// (3.973 TRY, 2026). Wiring this factory up to any client-facing
    /// endpoint would make ALKAROS a tool for an illegal charge. Only
    /// <see cref="CreateTip"/> below (a genuinely voluntary, customer-
    /// initiated amount) may ever reach an endpoint.
    /// </remarks>
    public static BillAdjustment CreateServiceFee(
        Guid id,
        Guid billId,
        decimal amount,
        decimal taxRate,
        string reason,
        Guid authorizedBy,
        bool isKuver = false,
        string? notes = null,
        Guid? createdBy = null)
    {
        if (amount <= 0)
            throw new ArgumentException("Service fee / Kuver amount must be positive.", nameof(amount));

        var feeGross = BillMath.RoundCurrency(amount);
        var feeTax = BillItem.TaxIncludedIn(feeGross, taxRate);
        var feeNet = feeGross - feeTax;

        return new BillAdjustment(
            id: id,
            billId: billId,
            adjustmentType: isKuver ? AdjustmentType.Kuver : AdjustmentType.ServiceFee,
            calculationType: AdjustmentCalculationType.FixedAmount,
            amount: feeGross,
            netAmount: feeNet,
            grossAmount: feeGross,
            reason: reason,
            authorizedBy: authorizedBy,
            taxRate: taxRate,
            taxAmount: feeTax,
            isDeduction: false,
            notes: notes,
            createdBy: createdBy);
    }

    /// <summary>
    /// Creates a tip adjustment (VAT exempt / 0% tax per V0-CMP-004). The
    /// caller (V1-WTR-020's <c>POST .../bills/{billId}/tip</c>) must never
    /// pre-fill or suggest <paramref name="amount"/> — a voluntary tip is
    /// only lawful when the customer chose the figure themselves, per the
    /// same regulation <see cref="CreateServiceFee"/>'s own remark cites.
    /// </summary>
    public static BillAdjustment CreateTip(
        Guid id,
        Guid billId,
        decimal amount,
        string reason,
        Guid authorizedBy,
        string? notes = null,
        Guid? createdBy = null,
        string? idempotencyKey = null)
    {
        if (amount <= 0)
            throw new ArgumentException("Tip amount must be positive.", nameof(amount));

        var tipAmount = BillMath.RoundCurrency(amount);

        return new BillAdjustment(
            id: id,
            billId: billId,
            adjustmentType: AdjustmentType.Tip,
            calculationType: AdjustmentCalculationType.FixedAmount,
            amount: tipAmount,
            netAmount: tipAmount,
            grossAmount: tipAmount,
            reason: reason,
            authorizedBy: authorizedBy,
            taxRate: 0m,
            taxAmount: 0m,
            isDeduction: false,
            notes: notes,
            createdBy: createdBy,
            idempotencyKey: idempotencyKey);
    }
}
