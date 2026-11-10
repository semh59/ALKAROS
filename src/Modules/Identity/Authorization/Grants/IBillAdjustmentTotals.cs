namespace ALKAROS.Identity.Authorization.Grants;

/// <summary>Where <see cref="BillAdjustmentTotalGate"/> reads what one bill has actually had taken off by comps and discounts, in lira.</summary>
public interface IBillAdjustmentTotals
{
    Task<decimal> GetAppliedAsync(Guid billId, CancellationToken cancellationToken = default);
}
