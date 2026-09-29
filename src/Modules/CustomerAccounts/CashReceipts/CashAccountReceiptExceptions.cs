namespace ALKAROS.CustomerAccounts.CashReceipts;

public abstract class CashAccountReceiptException(string message) : InvalidOperationException(message);

public sealed class CashAccountReceiptCurrencyNotSupportedException(string currencyCode)
    : CashAccountReceiptException($"Currency '{currencyCode}' is not supported for a cash account receipt; only TRY is.")
{
    public string CurrencyCode { get; } = currencyCode;
}

/// <summary>The cash session does not exist or is no longer Open (counting, closing or closed).</summary>
public sealed class CashAccountReceiptSessionNotOpenException(Guid cashSessionId, string? status)
    : CashAccountReceiptException($"Cash session {cashSessionId} is not open ({status ?? "not found"}).")
{
    public Guid CashSessionId { get; } = cashSessionId;
    public string? Status { get; } = status;
}

/// <summary>The customer does not exist or is anonymized.</summary>
public sealed class CashAccountReceiptCustomerNotFoundException(Guid customerId)
    : CashAccountReceiptException($"Customer {customerId} does not exist or is anonymized.")
{
    public Guid CustomerId { get; } = customerId;
}

/// <summary>Overpayment policy: a receipt cannot exceed what the customer owes (no prepayment, no credit balance).</summary>
public sealed class CashAccountReceiptOverpaymentException(Guid customerId, decimal amount, decimal outstanding)
    : CashAccountReceiptException($"A receipt of {amount} exceeds customer {customerId}'s outstanding balance of {outstanding}.")
{
    public Guid CustomerId { get; } = customerId;
    public decimal Amount { get; } = amount;
    public decimal Outstanding { get; } = outstanding;
}
