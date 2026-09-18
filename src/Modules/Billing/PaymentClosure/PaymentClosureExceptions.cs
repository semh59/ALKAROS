namespace ALKAROS.Billing.PaymentClosure;

/// <summary>Thrown when the Bill named by a projection rebuild does not exist.</summary>
public sealed class PaymentClosureBillNotFoundException : Exception
{
    public PaymentClosureBillNotFoundException(Guid billId)
        : base($"Bill '{billId}' was not found.")
    {
        BillId = billId;
    }

    public Guid BillId { get; }
}
