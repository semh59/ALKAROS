using ALKAROS.Billing.Adjustments;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Billing.PaymentClosure;
using ALKAROS.Payments.Allocations.Persistence;
using ALKAROS.Payments.PaymentAggregate;

namespace ALKAROS.Host.DualScreen;

/// <summary>A live bill with the adjustments and payments the display statement is built from.</summary>
public sealed record BillStatement(Bill Bill, IReadOnlyList<BillAdjustment> Adjustments, BillPaymentClosureProjection Projection);

/// <summary>
/// Rebuilds a display snapshot from the order's bills. The snapshot itself is built from the order alone, so without
/// this the guest sees neither a bill discount nor a tip, and keeps seeing the full total after a partial payment.
/// </summary>
public static class CustomerDisplayPaymentProgress
{
    private const string ActiveState = "Active";
    private const string CompletedState = "Completed";
    private const string IdleState = "Idle";

    public const string PartiallyPaidMessage = "Ödemenizin bir bölümü alındı; kalan tutar ödenecek.";

    public const string BillCancelledMessage = "Hesap iptal edildi.";

    public static async Task<CustomerDisplaySnapshotDto> AttachAsync(
        CustomerDisplaySnapshotDto snapshot,
        IBillRepository bills,
        IBillAdjustmentRepository adjustments,
        IPaymentRepository payments,
        IPaymentAllocationRepository allocations,
        CancellationToken cancellationToken)
    {
        if (snapshot.OrderId is not { } orderId || snapshot.State is not (ActiveState or CompletedState))
            return snapshot;

        var live = new List<BillStatement>();
        var anyCancelled = false;
        foreach (var bill in await bills.GetByOrderIdAsync(orderId, cancellationToken))
        {
            if (bill.Status == BillState.Cancelled)
            {
                anyCancelled = true;
                continue;
            }

            // One read of the adjustments feeds both the statement and the payment projection, so they cannot disagree.
            var billAdjustments = await adjustments.GetByBillIdAsync(bill.Id, cancellationToken);
            var projection = BillPaymentClosureCalculator.Compute(
                bill,
                await payments.GetByBillIdAsync(bill.Id, cancellationToken),
                await allocations.GetByBillIdAsync(bill.Id, cancellationToken),
                billAdjustments);
            live.Add(new BillStatement(bill, billAdjustments, projection));
        }

        return Apply(snapshot, live, anyCancelled);
    }

    /// <param name="live">The order's bills that are not cancelled.</param>
    /// <param name="anyBillCancelled">
    /// Whether the order also has a cancelled bill. An order that is no longer being edited and has only cancelled
    /// bills has nothing left to pay, so the display returns to idle instead of keeping "order received" up.
    /// </param>
    public static CustomerDisplaySnapshotDto Apply(
        CustomerDisplaySnapshotDto snapshot, IReadOnlyCollection<BillStatement> live, bool anyBillCancelled = false)
    {
        if (snapshot.State is not (ActiveState or CompletedState))
            return snapshot;

        if (live.Count == 0)
            return anyBillCancelled && snapshot.State == ActiveState && !snapshot.Editable ? Cleared(snapshot) : snapshot;

        var summaries = live.Select(s => AdjustmentCalculator.Calculate(s.Bill, s.Adjustments)).ToList();
        var lines = live
            .SelectMany(s => s.Bill.Items)
            .Select(item => new CustomerDisplayLineDto(
                item.OrderItemId, item.ProductNameSnapshot, item.Quantity, item.UnitPrice, item.LineSubtotal, item.DiscountAmount))
            .ToList();
        var paid = live.Sum(s => s.Projection.AllocatedTotal);
        var remaining = live.Zip(summaries).Sum(pair => Math.Max(0m, pair.Second.AdjustedPayableAmount - pair.First.Projection.AllocatedTotal));
        return snapshot with
        {
            Lines = lines,
            Subtotal = lines.Sum(line => line.LineTotal),
            DiscountTotal = lines.Sum(line => line.DiscountAmount) + summaries.Sum(s => s.TotalDiscounts),
            TipTotal = summaries.Sum(s => s.TotalTips),
            FeeTotal = summaries.Sum(s => s.TotalFees),
            TaxTotal = summaries.Sum(s => s.AdjustedTaxTotal),
            Total = summaries.Sum(s => s.AdjustedPayableAmount),
            PaidTotal = paid,
            RemainingTotal = remaining,
            Message = snapshot.State == ActiveState && paid > 0m && remaining > 0m ? PartiallyPaidMessage : snapshot.Message,
        };
    }

    private static CustomerDisplaySnapshotDto Cleared(CustomerDisplaySnapshotDto snapshot) => snapshot with
    {
        State = IdleState,
        Lines = [],
        Subtotal = 0m,
        DiscountTotal = 0m,
        TipTotal = 0m,
        FeeTotal = 0m,
        TaxTotal = 0m,
        Total = 0m,
        PaidTotal = 0m,
        RemainingTotal = null,
        Message = BillCancelledMessage,
    };
}
