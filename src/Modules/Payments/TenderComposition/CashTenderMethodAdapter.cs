using ALKAROS.Cash.TenderHandler;

namespace ALKAROS.Payments.TenderRouting;

/// <summary>
/// Bridges the already-Done <c>ICashTenderHandler</c> (V13-CSH-003) into the
/// generic <see cref="ITenderHandler"/> shape so Cash can be registered in
/// the same <see cref="ITenderHandlerRegistry"/> as every other method
/// (V13-PAY-003's Goal: "tek fail-closed registry"). Pure structural
/// translation only — no tender business logic lives here; every real
/// invariant (session-open, sufficient tender, allocation, ledger posting)
/// still lives entirely inside <c>CashTenderHandler</c>, unmodified.
///
/// <see cref="TenderRequest"/>'s <c>BillId</c>/<c>CashSessionId</c>/
/// <c>IdempotencyKey</c> are required for Cash (checked here, not inside
/// <c>CashTenderHandler</c>) since the generic envelope makes them optional
/// for methods that don't need them. <see cref="TenderRequest.PaymentId"/>
/// itself is NOT the id of the Payment this call creates — see
/// <see cref="TenderRequest"/>'s own doc comment for why.
///
/// <see cref="TenderRequest"/> carries a single <c>Amount</c>, so this
/// bridge always tenders exactly the amount due (no change) — a caller
/// needing change-giving must call <c>ICashTenderHandler</c> directly with
/// its own richer <c>CashTenderRequest(AmountDue, TenderedAmount)</c> pair;
/// widening the generic envelope for this is out of this task's scope.
/// </summary>
public sealed class CashTenderMethodAdapter : ITenderHandler
{
    private readonly ICashTenderHandler _cashTenderHandler;

    public CashTenderMethodAdapter(ICashTenderHandler cashTenderHandler)
    {
        _cashTenderHandler = cashTenderHandler ?? throw new ArgumentNullException(nameof(cashTenderHandler));
    }

    public TenderMethod Method => TenderMethod.Cash;

    public async Task<TenderHandlerResult> HandleAsync(
        TenderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Method != TenderMethod.Cash)
            throw new ArgumentException(
                $"'{nameof(CashTenderMethodAdapter)}' only handles '{TenderMethod.Cash}' requests.",
                nameof(request));
        if (request.BillId is null || request.BillId == Guid.Empty)
            throw new ArgumentException("Bill id is required for a Cash tender request.", nameof(request));
        if (request.CashSessionId is null || request.CashSessionId == Guid.Empty)
            throw new ArgumentException("Cash session id is required for a Cash tender request.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw new ArgumentException("Idempotency key is required for a Cash tender request.", nameof(request));

        var cashResult = await _cashTenderHandler.HandleAsync(
            new CashTenderRequest(
                request.CashSessionId.Value,
                request.BillId.Value,
                AmountDue: request.Amount,
                TenderedAmount: request.Amount,
                IdempotencyKey: request.IdempotencyKey,
                RecordedBy: request.RecordedBy),
            cancellationToken).ConfigureAwait(false);

        // ICashTenderHandler has no Declined/RequiresReconciliation outcome
        // of its own — a cash tender either succeeds atomically or throws a
        // typed CashTenderException (propagated as-is, never swallowed into
        // a fabricated Declined/Unknown result).
        return new TenderApproved(cashResult.ApprovedAmount);
    }
}
