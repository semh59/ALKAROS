namespace ALKAROS.CustomerAccounts.CashReceipts;

/// <summary>A customer paying cash towards their account, taken into an open cash session.</summary>
public sealed record CashAccountReceiptRequest(
    Guid CustomerId,
    Guid CashSessionId,
    decimal Amount,
    string IdempotencyKey,
    Guid? RecordedBy = null,
    string CurrencyCode = "TRY")
{
    public void Validate()
    {
        if (CustomerId == Guid.Empty)
            throw new ArgumentException("Customer id cannot be empty.", nameof(CustomerId));
        if (CashSessionId == Guid.Empty)
            throw new ArgumentException("Cash session id cannot be empty.", nameof(CashSessionId));
        if (Amount <= 0m || decimal.Round(Amount, 2) != Amount)
            throw new ArgumentOutOfRangeException(nameof(Amount), Amount, "Amount must be positive with at most two decimals.");
        if (string.IsNullOrWhiteSpace(IdempotencyKey))
            throw new ArgumentException("Idempotency key cannot be empty.", nameof(IdempotencyKey));
        if (CurrencyCode != "TRY")
            throw new CashAccountReceiptCurrencyNotSupportedException(CurrencyCode);
    }
}

/// <summary>The three records a cash account receipt produced, and the customer's balance right after it.</summary>
public sealed record CashAccountReceiptResult(
    Guid AccountPaymentId,
    Guid CashTransactionId,
    Guid AccountTransactionId,
    decimal Amount,
    decimal BalanceAfter,
    bool WasReplayed);
