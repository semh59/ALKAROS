namespace ALKAROS.CustomerAccounts.AccountPayments;

public enum AccountPaymentMethod
{
    Cash,
    BankCard,
}

public enum AccountPaymentStatus
{
    Requested,
    Approved,
    Declined,
    Unknown,
}

/// <summary>What proves an Approved account payment: the method-specific owner's own record of the money.</summary>
public enum AccountPaymentEvidenceType
{
    /// <summary>A <c>cash.cash_transactions</c> row id, written by the cash receipt flow (V14-ACC-005).</summary>
    CashTransaction,

    /// <summary>The card provider's approval reference, written by the card receipt flow (V14-ACC-006).</summary>
    CardProviderReference,
}

/// <summary>An evidence reference together with its type.</summary>
public sealed record AccountPaymentEvidence(AccountPaymentEvidenceType Type, string Reference)
{
    public static AccountPaymentEvidence ForCashTransaction(Guid cashTransactionId)
        => new(AccountPaymentEvidenceType.CashTransaction, cashTransactionId.ToString("D"));
}

/// <summary>
/// V14-ACC-004: a payment a customer makes towards their receivable account, independent of any Bill. Immutable,
/// like Payment: every transition returns a new instance.
/// <list type="bullet">
/// <item>Requested -&gt; Approved | Declined | Unknown; Unknown -&gt; Approved | Declined. Approved and Declined are
/// terminal.</item>
/// <item>Approved always carries evidence whose type matches the method (a cash transaction for Cash, a provider
/// reference for BankCard). Unknown is never success.</item>
/// </list>
/// This aggregate writes no AccountTransaction, CashTransaction or PaymentAllocation; the method-specific owners do.
/// </summary>
public sealed class AccountPayment
{
    public AccountPayment(
        Guid id,
        Guid customerId,
        AccountPaymentMethod method,
        decimal amount,
        string idempotencyKey,
        DateTimeOffset requestedAt,
        string currencyCode = "TRY",
        AccountPaymentStatus status = AccountPaymentStatus.Requested,
        AccountPaymentEvidence? evidence = null,
        Guid? requestedBy = null,
        DateTimeOffset? updatedAt = null,
        long rowVersion = 1)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Account payment id cannot be empty.", nameof(id));
        if (customerId == Guid.Empty)
            throw new ArgumentException("Customer id cannot be empty.", nameof(customerId));
        if (amount <= 0m || decimal.Round(amount, 2) != amount)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be positive with at most two decimals.");
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key cannot be empty.", nameof(idempotencyKey));
        if (currencyCode is not { Length: 3 })
            throw new ArgumentException("Currency code must be a three-letter code.", nameof(currencyCode));
        if (status == AccountPaymentStatus.Approved && evidence is null)
            throw new InvalidAccountPaymentTransitionException("An Approved account payment requires evidence.");
        if (evidence is not null)
            EnsureEvidenceMatchesMethod(method, evidence);

        Id = id;
        CustomerId = customerId;
        Method = method;
        Amount = amount;
        IdempotencyKey = idempotencyKey;
        RequestedAt = requestedAt;
        CurrencyCode = currencyCode;
        Status = status;
        Evidence = evidence;
        RequestedBy = requestedBy;
        UpdatedAt = updatedAt ?? requestedAt;
        RowVersion = rowVersion;
    }

    public Guid Id { get; }
    public Guid CustomerId { get; }
    public AccountPaymentMethod Method { get; }
    public decimal Amount { get; }
    public string IdempotencyKey { get; }
    public DateTimeOffset RequestedAt { get; }
    public string CurrencyCode { get; }
    public AccountPaymentStatus Status { get; }
    public AccountPaymentEvidence? Evidence { get; }
    public Guid? RequestedBy { get; }
    public DateTimeOffset UpdatedAt { get; }
    public long RowVersion { get; }

    public bool IsTerminal => Status is AccountPaymentStatus.Approved or AccountPaymentStatus.Declined;

    public static AccountPayment Request(
        Guid customerId, AccountPaymentMethod method, decimal amount, string idempotencyKey, Guid? requestedBy, DateTimeOffset now)
        => new(Guid.NewGuid(), customerId, method, amount, idempotencyKey, now, requestedBy: requestedBy);

    public AccountPayment Approve(AccountPaymentEvidence evidence, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (string.IsNullOrWhiteSpace(evidence.Reference))
            throw new InvalidAccountPaymentTransitionException("Approval evidence reference cannot be empty.");
        EnsureCanMoveTo(AccountPaymentStatus.Approved);
        EnsureEvidenceMatchesMethod(Method, evidence);
        return With(AccountPaymentStatus.Approved, evidence, at);
    }

    public AccountPayment Decline(DateTimeOffset at)
    {
        EnsureCanMoveTo(AccountPaymentStatus.Declined);
        return With(AccountPaymentStatus.Declined, Evidence, at);
    }

    public AccountPayment MarkUnknown(DateTimeOffset at)
    {
        EnsureCanMoveTo(AccountPaymentStatus.Unknown);
        return With(AccountPaymentStatus.Unknown, Evidence, at);
    }

    public static bool CanTransition(AccountPaymentStatus from, AccountPaymentStatus to) => (from, to) switch
    {
        (AccountPaymentStatus.Requested, AccountPaymentStatus.Approved) => true,
        (AccountPaymentStatus.Requested, AccountPaymentStatus.Declined) => true,
        (AccountPaymentStatus.Requested, AccountPaymentStatus.Unknown) => true,
        (AccountPaymentStatus.Unknown, AccountPaymentStatus.Approved) => true,
        (AccountPaymentStatus.Unknown, AccountPaymentStatus.Declined) => true,
        _ => false,
    };

    private void EnsureCanMoveTo(AccountPaymentStatus target)
    {
        if (!CanTransition(Status, target))
            throw new InvalidAccountPaymentTransitionException($"Account payment {Id} cannot move from {Status} to {target}.");
    }

    private static void EnsureEvidenceMatchesMethod(AccountPaymentMethod method, AccountPaymentEvidence evidence)
    {
        var expected = method == AccountPaymentMethod.Cash
            ? AccountPaymentEvidenceType.CashTransaction
            : AccountPaymentEvidenceType.CardProviderReference;
        if (evidence.Type != expected)
            throw new InvalidAccountPaymentTransitionException($"A {method} account payment is proven by {expected}, not {evidence.Type}.");
    }

    private AccountPayment With(AccountPaymentStatus status, AccountPaymentEvidence? evidence, DateTimeOffset at)
        => new(Id, CustomerId, Method, Amount, IdempotencyKey, RequestedAt, CurrencyCode, status, evidence, RequestedBy, at, RowVersion);
}

/// <summary>One row of an account payment's append-only status history.</summary>
public sealed record AccountPaymentStatusChange(
    AccountPaymentStatus? OldStatus,
    AccountPaymentStatus NewStatus,
    string? EvidenceReference,
    string? Reason,
    Guid? ChangedBy,
    DateTimeOffset ChangedAt);
