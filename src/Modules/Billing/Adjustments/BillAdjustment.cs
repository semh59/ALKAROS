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
        long rowVersion = 1)
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
        IsDeduction = isDeduction ?? (adjustmentType is AdjustmentType.DiscountPercentage or AdjustmentType.DiscountAmount);
        Reason = reason;
        AuthorizedBy = authorizedBy;
        Notes = notes;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        CreatedBy = createdBy;
        RowVersion = rowVersion;
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
        Guid? createdBy = null)
    {
        if (rate <= 0 || rate > 100)
            throw new ArgumentException("Discount percentage rate must be between 0 and 100.", nameof(rate));
        if (baseGrossAmount <= 0)
            throw new ArgumentException("Base gross amount must be positive to apply discount.", nameof(baseGrossAmount));

        var discountGross = BillMath.RoundCurrency(baseGrossAmount * (rate / 100m));
        var discountNet = taxRate > 0
            ? BillMath.RoundCurrency(discountGross / (1m + (taxRate / 100m)))
            : discountGross;
        var discountTax = BillMath.RoundCurrency(discountGross - discountNet);

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
            createdBy: createdBy);
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
        Guid? createdBy = null)
    {
        if (discountAmount <= 0)
            throw new ArgumentException("Discount amount must be positive.", nameof(discountAmount));

        var discountGross = BillMath.RoundCurrency(discountAmount);
        var discountNet = taxRate > 0
            ? BillMath.RoundCurrency(discountGross / (1m + (taxRate / 100m)))
            : discountGross;
        var discountTax = BillMath.RoundCurrency(discountGross - discountNet);

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
            createdBy: createdBy);
    }

    /// <summary>
    /// Creates a service fee or kuver adjustment.
    /// </summary>
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
        var feeNet = taxRate > 0
            ? BillMath.RoundCurrency(feeGross / (1m + (taxRate / 100m)))
            : feeGross;
        var feeTax = BillMath.RoundCurrency(feeGross - feeNet);

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
    /// Creates a tip adjustment (VAT exempt / 0% tax per V0-CMP-004).
    /// </summary>
    public static BillAdjustment CreateTip(
        Guid id,
        Guid billId,
        decimal amount,
        string reason,
        Guid authorizedBy,
        string? notes = null,
        Guid? createdBy = null)
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
            createdBy: createdBy);
    }
}
