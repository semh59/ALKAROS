namespace ALKAROS.Identity.Authorization.Grants;

/// <summary>Where <see cref="BillAdjustmentTotalGate"/> reads the per-bill total cap from, in lira.</summary>
public interface IBillAdjustmentTotalCap
{
    Task<decimal> GetAsync(CancellationToken cancellationToken = default);
}
