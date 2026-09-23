namespace ALKAROS.Payments.TenderRouting;

/// <summary>
/// A request to tender a Payment through one of the canonical
/// <see cref="TenderRouting.TenderMethod"/> values. The envelope every
/// handler (Cash/BankCard/MealCard, later) and the router itself agree on.
///
/// <para>
/// <see cref="BillId"/>/<see cref="CashSessionId"/>/<see cref="IdempotencyKey"/>/
/// <see cref="RecordedBy"/> are optional fields added by V13-PAY-003's Cash
/// composition bridge (<c>ALKAROS.Payments.TenderComposition.CashTenderMethodAdapter</c>):
/// <c>ICashTenderHandler</c>'s own contract (V13-CSH-003, unmodified) needs
/// strictly more than the original three-field envelope carried, and that
/// contract mints its own new Payment id rather than tendering
/// <see cref="PaymentId"/> — so for a <see cref="TenderMethod.Cash"/>
/// request, <see cref="PaymentId"/> is only a non-empty correlation token
/// the caller supplies to satisfy <see cref="Validate"/>, never the id of
/// the Payment actually created (that id is only observable by calling
/// <c>ICashTenderHandler</c> directly, or by inspecting the resulting
/// <c>CashTenderResult</c> outside the generic <see cref="TenderHandlerResult"/>
/// contract, which this task's Owned surface does not extend). Other tender
/// methods (BankCard/MealCard) leave these four fields null.
/// </para>
///
/// <para>
/// <see cref="Note"/> is an optional free-text field added by V13-PAY-005's
/// EFT/Havale handler (a reference number is explicitly NOT required —
/// Semih's own product decision). It carries through unchanged as the
/// resulting <c>Payment</c> status-history entry's own <c>reason</c> text;
/// other tender methods leave it null.
/// </para>
/// </summary>
public sealed record TenderRequest(
    Guid PaymentId,
    TenderMethod Method,
    decimal Amount,
    Guid? BillId = null,
    Guid? CashSessionId = null,
    string? IdempotencyKey = null,
    Guid? RecordedBy = null,
    string? Note = null)
{
    public void Validate()
    {
        if (PaymentId == Guid.Empty)
            throw new ArgumentException("Payment id cannot be empty.", nameof(PaymentId));
        if (Amount <= 0)
            throw new ArgumentException("Tender amount must be greater than zero.", nameof(Amount));
    }
}
