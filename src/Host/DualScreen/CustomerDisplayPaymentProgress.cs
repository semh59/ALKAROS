using ALKAROS.Billing.BillFoundation;
using ALKAROS.Billing.PaymentClosure;

namespace ALKAROS.Host.DualScreen;

/// <summary>
/// Adds what the till has already taken to a display snapshot. The snapshot itself is built from the order alone, so
/// without this the guest keeps seeing the full total after a partial payment.
/// </summary>
public static class CustomerDisplayPaymentProgress
{
    private const string ActiveState = "Active";

    public const string PartiallyPaidMessage = "Ödemenizin bir bölümü alındı; kalan tutar ödenecek.";

    public static async Task<CustomerDisplaySnapshotDto> AttachAsync(
        CustomerDisplaySnapshotDto snapshot,
        IBillRepository bills,
        IBillPaymentClosureProjector projector,
        CancellationToken cancellationToken)
    {
        if (snapshot.OrderId is not { } orderId || snapshot.State != ActiveState)
            return snapshot;

        var projections = new List<BillPaymentClosureProjection>();
        foreach (var bill in await bills.GetByOrderIdAsync(orderId, cancellationToken))
        {
            if (bill.Status != BillState.Cancelled)
                projections.Add(await projector.RebuildAsync(bill.Id, cancellationToken));
        }

        return Apply(snapshot, projections);
    }

    public static CustomerDisplaySnapshotDto Apply(
        CustomerDisplaySnapshotDto snapshot, IReadOnlyCollection<BillPaymentClosureProjection> projections)
    {
        if (snapshot.State != ActiveState || projections.Count == 0)
            return snapshot;

        var paid = projections.Sum(p => p.AllocatedTotal);
        var remaining = projections.Sum(p => Math.Max(0m, p.PayableAmount - p.AllocatedTotal));
        return snapshot with
        {
            PaidTotal = paid,
            RemainingTotal = remaining,
            Message = paid > 0m && remaining > 0m ? PartiallyPaidMessage : snapshot.Message,
        };
    }
}
