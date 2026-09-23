namespace ALKAROS.Payments.EftTender;

/// <summary>Base exception for EFT/Havale tender domain errors (V13-PAY-005).</summary>
public abstract class EftTenderException : Exception
{
    protected EftTenderException(string message) : base(message) { }
}

/// <summary>Thrown when the Bill named by an EFT tender request does not exist.</summary>
public sealed class EftTenderBillNotFoundException : EftTenderException
{
    public EftTenderBillNotFoundException(Guid billId)
        : base($"Bill '{billId}' was not found.")
    {
        BillId = billId;
    }

    public Guid BillId { get; }
}

/// <summary>
/// Thrown when an EFT tender amount exceeds the Bill's remaining payable.
/// EFT has no change-giving concept (unlike Cash) — an over-tender is a hard
/// rejection, never a surplus to hand back.
/// </summary>
public sealed class EftOverTenderException : EftTenderException
{
    public EftOverTenderException(Guid billId, decimal amount, decimal remainingPayable)
        : base($"EFT amount '{amount}' exceeds bill '{billId}''s remaining payable '{remainingPayable}'.")
    {
        BillId = billId;
        Amount = amount;
        RemainingPayable = remainingPayable;
    }

    public Guid BillId { get; }
    public decimal Amount { get; }
    public decimal RemainingPayable { get; }
}
