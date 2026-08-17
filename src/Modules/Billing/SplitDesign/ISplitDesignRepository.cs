namespace ALKAROS.Billing.SplitDesign;

/// <summary>
/// Persistence contract for Bill split design allocations (billing.bill_allocations).
/// </summary>
public interface ISplitDesignRepository
{
    /// <summary>
    /// Loads all split allocations defined for a Bill.
    /// </summary>
    Task<IReadOnlyList<BillAllocation>> GetAllocationsByBillIdAsync(Guid billId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically replaces all split allocations for a Bill in a single transaction.
    /// </summary>
    Task SaveSplitDesignAsync(Guid billId, IReadOnlyList<BillAllocation> allocations, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all split allocations for a Bill.
    /// </summary>
    Task DeleteSplitDesignAsync(Guid billId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the sum of allocated amounts for a Bill.
    /// </summary>
    Task<decimal> GetTotalAllocatedAmountAsync(Guid billId, CancellationToken cancellationToken = default);
}
