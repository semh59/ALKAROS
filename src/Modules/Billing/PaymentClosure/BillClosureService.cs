using ALKAROS.Billing.BillFoundation;

namespace ALKAROS.Billing.PaymentClosure;

public sealed class BillClosureService : IBillClosureService
{
    private const int MaxAttempts = 3;

    private readonly IBillRepository _bills;
    private readonly IBillPaymentClosureProjector _projector;

    public BillClosureService(IBillRepository bills, IBillPaymentClosureProjector projector)
    {
        _bills = bills ?? throw new ArgumentNullException(nameof(bills));
        _projector = projector ?? throw new ArgumentNullException(nameof(projector));
    }

    public async Task<BillClosureResult> TryCloseAsync(Guid billId, CancellationToken cancellationToken = default)
    {
        if (billId == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(billId));

        for (var attempt = 1; ; attempt++)
        {
            var bill = await _bills.GetByIdAsync(billId, cancellationToken)
                ?? throw new PaymentClosureBillNotFoundException(billId);

            if (bill.Status == BillState.Paid)
                return new BillClosureResult(BillClosureOutcome.AlreadyClosed, []);
            if (bill.Status == BillState.Cancelled)
                return new BillClosureResult(BillClosureOutcome.NotClosable, []);

            var projection = await _projector.RebuildAsync(billId, cancellationToken);
            if (!projection.PaymentSatisfied || projection.Blockers.Count > 0)
                return new BillClosureResult(BillClosureOutcome.NotSatisfied, projection.Blockers);

            var closing = bill.WithPaymentTotals(projection.AllocatedTotal, projection.PaidTotal, projection.ChangeTotal);
            if (closing.Status is BillState.Open or BillState.PartiallyAllocated or BillState.Reopened)
                closing = closing.TransitionTo(BillState.Allocated);
            closing = closing.TransitionTo(BillState.Paid);

            try
            {
                await _bills.SaveAsync(closing, bill.RowVersion, cancellationToken);
                return new BillClosureResult(BillClosureOutcome.Closed, []);
            }
            catch (InvalidOperationException) when (attempt < MaxAttempts)
            {
                // Optimistic concurrency: someone changed the bill (a split edit, another closer).
                // Reload and re-evaluate; the second look usually finds it already Paid.
            }
        }
    }
}
