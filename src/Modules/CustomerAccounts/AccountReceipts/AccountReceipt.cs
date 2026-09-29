using ALKAROS.CustomerAccounts.AccountPayments;

namespace ALKAROS.CustomerAccounts.AccountReceipts;

/// <summary>V14-ACC-009: the receipt for one verified (Approved) account payment. Never edited or deleted.</summary>
public sealed record AccountReceipt(
    Guid Id,
    string ReceiptNumber,
    Guid CustomerId,
    Guid AccountPaymentId,
    decimal Amount,
    string CurrencyCode,
    string IdempotencyKey,
    Guid? IssuedBy,
    DateTimeOffset IssuedAt);

/// <summary>What <see cref="IAccountReceiptService.IssueAsync"/> produced.</summary>
public sealed record AccountReceiptIssueResult(AccountReceipt Receipt, bool WasReplayed);

/// <summary>
/// Typed evidence handed to reconciliation (V14-ACC-007) when a receipt was asked for an account payment that is not
/// verified: an Unknown, Requested or Declined payment never closes as a receipt.
/// </summary>
public sealed record AccountReceiptReconciliationEvidence(
    Guid AccountPaymentId,
    Guid CustomerId,
    AccountPaymentStatus PaymentStatus,
    decimal Amount,
    string Reason);

public abstract class AccountReceiptException(string message) : InvalidOperationException(message);

/// <summary>The account payment is not Approved; <see cref="Evidence"/> is what reconciliation needs.</summary>
public sealed class AccountReceiptPaymentNotVerifiedException(AccountReceiptReconciliationEvidence evidence)
    : AccountReceiptException($"Account payment {evidence.AccountPaymentId} is {evidence.PaymentStatus}; no receipt is issued.")
{
    public AccountReceiptReconciliationEvidence Evidence { get; } = evidence;
}

/// <summary>An idempotency key that already issued a receipt for a different account payment.</summary>
public sealed class AccountReceiptIdempotencyKeyReusedException(string idempotencyKey)
    : AccountReceiptException($"Idempotency key '{idempotencyKey}' already issued a receipt for another account payment.")
{
    public string IdempotencyKey { get; } = idempotencyKey;
}
