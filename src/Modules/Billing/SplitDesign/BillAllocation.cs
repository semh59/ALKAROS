using ALKAROS.Billing.BillFoundation;

namespace ALKAROS.Billing.SplitDesign;

/// <summary>
/// A persistent allocation record representing an ownership segment of a Bill (PDF:III.7.3).
/// Supports equal, item, amount, or customer account allocations without payment execution.
/// </summary>
public sealed class BillAllocation
{
    public BillAllocation(
        Guid id,
        Guid billId,
        AllocationOwnerType ownerType,
        string ownerReference,
        decimal allocatedAmount,
        decimal taxAmount = 0m,
        Guid? billItemId = null,
        decimal? allocatedQuantity = null,
        DateTimeOffset? createdAt = null,
        Guid? createdBy = null,
        long rowVersion = 1)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Bill allocation id cannot be empty.", nameof(id));
        if (billId == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(billId));
        if (billItemId == Guid.Empty)
            throw new ArgumentException("Bill item id cannot be empty GUID when specified.", nameof(billItemId));
        if (string.IsNullOrWhiteSpace(ownerReference))
            throw new ArgumentException("Owner reference cannot be empty.", nameof(ownerReference));
        if (allocatedAmount <= 0)
            throw new ArgumentException("Allocated amount must be positive.", nameof(allocatedAmount));
        if (taxAmount < 0)
            throw new ArgumentException("Tax amount cannot be negative.", nameof(taxAmount));
        if (allocatedQuantity.HasValue && allocatedQuantity.Value <= 0)
            throw new ArgumentException("Allocated quantity must be positive when specified.", nameof(allocatedQuantity));
        if (createdBy == Guid.Empty)
            throw new ArgumentException("Created by cannot be empty GUID when specified.", nameof(createdBy));

        Id = id;
        BillId = billId;
        BillItemId = billItemId;
        OwnerType = ownerType;
        OwnerReference = ownerReference;
        AllocatedQuantity = allocatedQuantity.HasValue ? BillMath.RoundQuantity(allocatedQuantity.Value) : null;
        AllocatedAmount = BillMath.RoundCurrency(allocatedAmount);
        TaxAmount = BillMath.RoundCurrency(taxAmount);
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        CreatedBy = createdBy;
        RowVersion = rowVersion;
    }

    public Guid Id { get; }

    public Guid BillId { get; }

    public Guid? BillItemId { get; }

    public AllocationOwnerType OwnerType { get; }

    public string OwnerReference { get; }

    public decimal? AllocatedQuantity { get; }

    public decimal AllocatedAmount { get; }

    public decimal TaxAmount { get; }

    public DateTimeOffset CreatedAt { get; }

    public Guid? CreatedBy { get; }

    public long RowVersion { get; }
}
