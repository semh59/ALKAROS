namespace ALKAROS.Billing.PaymentClosure;

/// <summary>Rebuilds a Bill's payment-closure projection (V13-ALC-002).</summary>
public interface IBillPaymentClosureProjector
{
    /// <summary>
    /// Loads the Bill and every Payment/PaymentAllocation recorded against
    /// it, then deterministically recomputes the projection. Throws
    /// <see cref="PaymentClosureBillNotFoundException"/> if the Bill does
    /// not exist.
    /// </summary>
    Task<BillPaymentClosureProjection> RebuildAsync(Guid billId, CancellationToken cancellationToken = default);
}
