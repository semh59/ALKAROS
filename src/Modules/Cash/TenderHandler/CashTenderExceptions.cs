namespace ALKAROS.Cash.TenderHandler;

/// <summary>Base exception for cash-tender domain errors (V13-CSH-003).</summary>
public abstract class CashTenderException : Exception
{
    protected CashTenderException(string message) : base(message) { }
}

/// <summary>Thrown when the Bill named by a cash tender request does not exist.</summary>
public sealed class CashTenderBillNotFoundException : CashTenderException
{
    public CashTenderBillNotFoundException(Guid billId)
        : base($"Bill '{billId}' was not found.")
    {
        BillId = billId;
    }

    public Guid BillId { get; }
}

/// <summary>
/// Thrown when a cash tender is attempted against a session that is not
/// open (PDF:II.5.9 — a sale must never post to a session already counting
/// or closed).
/// </summary>
public sealed class ClosedCashSessionException : CashTenderException
{
    public ClosedCashSessionException(Guid cashSessionId, Cash.Contracts.CashSessionStatus status)
        : base($"Cash session '{cashSessionId}' is '{status}', not Open — a cash tender cannot be posted to it.")
    {
        CashSessionId = cashSessionId;
        Status = status;
    }

    public Guid CashSessionId { get; }
    public Cash.Contracts.CashSessionStatus Status { get; }
}

/// <summary>
/// Thrown when the amount tendered by the customer is less than the amount
/// due — a cash tender can never leave the bill under-covered the way a
/// partial card authorization might; the cashier must collect enough cash
/// up front.
/// </summary>
public sealed class InsufficientCashTenderException : CashTenderException
{
    public InsufficientCashTenderException(decimal tenderedAmount, decimal amountDue)
        : base($"Tendered amount '{tenderedAmount}' is less than the amount due '{amountDue}'.")
    {
        TenderedAmount = tenderedAmount;
        AmountDue = amountDue;
    }

    public decimal TenderedAmount { get; }
    public decimal AmountDue { get; }
}

/// <summary>
/// V1-RMD-409 (V1-RMD-393 F-04): the Bill already has a Payment that is not settled (a card attempt at Unknown,
/// Pending or ReconciliationRequired). If that card was really charged, taking cash too would charge the guest twice;
/// EFT and card already refuse this, cash now does as well.
/// </summary>
public sealed class CashTenderUnsettledPaymentExistsException : CashTenderException
{
    public CashTenderUnsettledPaymentExistsException(Guid billId, Guid existingPaymentId)
        : base($"Bill '{billId}' has an unsettled payment '{existingPaymentId}'; it must be resolved before a cash tender.")
    {
        BillId = billId;
        ExistingPaymentId = existingPaymentId;
    }

    public Guid BillId { get; }

    public Guid ExistingPaymentId { get; }
}
