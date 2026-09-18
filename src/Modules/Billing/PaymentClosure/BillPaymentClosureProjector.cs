using ALKAROS.Billing.BillFoundation;
using ALKAROS.Payments.Allocations.Persistence;
using ALKAROS.Payments.PaymentAggregate;

namespace ALKAROS.Billing.PaymentClosure;

public sealed class BillPaymentClosureProjector : IBillPaymentClosureProjector
{
    private readonly IBillRepository _billRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentAllocationRepository _allocationRepository;

    public BillPaymentClosureProjector(
        IBillRepository billRepository,
        IPaymentRepository paymentRepository,
        IPaymentAllocationRepository allocationRepository)
    {
        _billRepository = billRepository ?? throw new ArgumentNullException(nameof(billRepository));
        _paymentRepository = paymentRepository ?? throw new ArgumentNullException(nameof(paymentRepository));
        _allocationRepository = allocationRepository ?? throw new ArgumentNullException(nameof(allocationRepository));
    }

    public async Task<BillPaymentClosureProjection> RebuildAsync(Guid billId, CancellationToken cancellationToken = default)
    {
        if (billId == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(billId));

        var bill = await _billRepository.GetByIdAsync(billId, cancellationToken)
            ?? throw new PaymentClosureBillNotFoundException(billId);

        // Two independent reads (Payments, then Allocations) rather than one
        // externally-supplied snapshot transaction: each of
        // IPaymentRepository/IPaymentAllocationRepository already reads its
        // own rows under its own RepeatableRead/committed-read semantics
        // (see their own GetByBillIdAsync), and a payment/allocation
        // arriving in the narrow window between these two calls only ever
        // makes this rebuild's answer MORE conservative (a newly-committed
        // allocation not yet reflected here cannot make PaymentSatisfied
        // wrongly true - it can only be seen a call later, and a
        // rebuild is by definition safe to call again).
        var payments = await _paymentRepository.GetByBillIdAsync(billId, cancellationToken);
        var allocations = await _allocationRepository.GetByBillIdAsync(billId, cancellationToken);

        return BillPaymentClosureCalculator.Compute(bill, payments, allocations);
    }
}
