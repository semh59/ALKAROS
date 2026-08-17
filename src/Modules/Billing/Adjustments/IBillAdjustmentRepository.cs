namespace ALKAROS.Billing.Adjustments;

/// <summary>
/// Persistence repository interface for bill adjustments (billing.bill_adjustments).
/// </summary>
public interface IBillAdjustmentRepository
{
    /// <summary>
    /// Loads all adjustments for a Bill.
    /// </summary>
    Task<IReadOnlyList<BillAdjustment>> GetByBillIdAsync(Guid billId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new adjustment line to a Bill.
    /// </summary>
    Task AddAsync(BillAdjustment adjustment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an adjustment line by its ID.
    /// </summary>
    Task RemoveAsync(Guid adjustmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the sum of discount deductions for a Bill.
    /// </summary>
    Task<decimal> GetTotalDiscountAmountAsync(Guid billId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the sum of fees/tips additions for a Bill.
    /// </summary>
    Task<decimal> GetTotalFeeAmountAsync(Guid billId, CancellationToken cancellationToken = default);
}
