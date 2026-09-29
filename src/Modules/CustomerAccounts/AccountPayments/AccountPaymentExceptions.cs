namespace ALKAROS.CustomerAccounts.AccountPayments;

public sealed class InvalidAccountPaymentTransitionException(string message) : InvalidOperationException(message);

/// <summary>The row version the caller read is no longer current: another request moved the payment first.</summary>
public sealed class AccountPaymentConcurrencyException(Guid accountPaymentId)
    : InvalidOperationException($"Account payment {accountPaymentId} was changed by another request.")
{
    public Guid AccountPaymentId { get; } = accountPaymentId;
}

/// <summary>An idempotency key that already belongs to a different account payment (other customer, method or amount).</summary>
public sealed class AccountPaymentIdempotencyKeyReusedException(string idempotencyKey)
    : InvalidOperationException($"Idempotency key '{idempotencyKey}' already belongs to a different account payment.")
{
    public string IdempotencyKey { get; } = idempotencyKey;
}

/// <summary>The evidence already proves another account payment; one cash transaction or provider reference proves one payment.</summary>
public sealed class AccountPaymentEvidenceAlreadyLinkedException(AccountPaymentEvidence evidence)
    : InvalidOperationException($"{evidence.Type} '{evidence.Reference}' already proves another account payment.")
{
    public AccountPaymentEvidence Evidence { get; } = evidence;
}

public sealed class AccountPaymentNotFoundException(Guid accountPaymentId)
    : KeyNotFoundException($"Account payment {accountPaymentId} was not found.")
{
    public Guid AccountPaymentId { get; } = accountPaymentId;
}
