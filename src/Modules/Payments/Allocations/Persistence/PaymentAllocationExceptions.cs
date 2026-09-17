namespace ALKAROS.Payments.Allocations.Persistence;

/// <summary>Base exception for PaymentAllocation domain errors (V13-ALC-001).</summary>
public abstract class PaymentAllocationException : Exception
{
    protected PaymentAllocationException(string message) : base(message) { }
}

/// <summary>Thrown when an allocation amount is zero or negative (CORR:C4).</summary>
public sealed class InvalidPaymentAllocationAmountException : PaymentAllocationException
{
    public InvalidPaymentAllocationAmountException(decimal amount)
        : base($"Allocation amount '{amount}' must be greater than zero.")
    {
        Amount = amount;
    }

    public decimal Amount { get; }
}

/// <summary>
/// Thrown when an allocation targets a Bill other than the one its Payment
/// actually belongs to (V0-DOM-004 same-bill invariant).
/// </summary>
public sealed class CrossBillPaymentAllocationException : PaymentAllocationException
{
    public CrossBillPaymentAllocationException(Guid paymentId, Guid paymentBillId, Guid targetBillId)
        : base($"Payment {paymentId} belongs to bill {paymentBillId}, not {targetBillId}; " +
               "an allocation must target the payment's own bill.")
    {
        PaymentId = paymentId;
        PaymentBillId = paymentBillId;
        TargetBillId = targetBillId;
    }

    public Guid PaymentId { get; }
    public Guid PaymentBillId { get; }
    public Guid TargetBillId { get; }
}

/// <summary>
/// Thrown when the payment's currency does not match the bill's currency
/// (V0-DOM-004: allocation.currency = payment.currency = bill.currency).
/// </summary>
public sealed class CurrencyMismatchAllocationException : PaymentAllocationException
{
    public CurrencyMismatchAllocationException(string paymentCurrency, string billCurrency)
        : base($"Payment currency '{paymentCurrency}' does not match bill currency '{billCurrency}'.")
    {
        PaymentCurrency = paymentCurrency;
        BillCurrency = billCurrency;
    }

    public string PaymentCurrency { get; }
    public string BillCurrency { get; }
}

/// <summary>
/// Thrown when an allocation amount exceeds what the bill still has left to
/// be paid (V0-DOM-004 remaining-amount invariant). The surplus belongs in
/// Payment.ChangeAmount, never in a larger allocation row.
/// </summary>
public sealed class OverAllocationException : PaymentAllocationException
{
    public OverAllocationException(Guid billId, decimal requestedAmount, decimal remainingPayable)
        : base($"Bill {billId} has {remainingPayable} remaining payable; allocation of {requestedAmount} " +
               "would over-allocate it.")
    {
        BillId = billId;
        RequestedAmount = requestedAmount;
        RemainingPayable = remainingPayable;
    }

    public Guid BillId { get; }
    public decimal RequestedAmount { get; }
    public decimal RemainingPayable { get; }
}
