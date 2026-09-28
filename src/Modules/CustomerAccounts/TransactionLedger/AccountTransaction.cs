namespace ALKAROS.CustomerAccounts.TransactionLedger;

/// <summary>
/// A single persisted, immutable row of <c>customer_account.account_transactions</c>
/// (migration 161). <see cref="Direction"/> is read back from the
/// database's own generated column - it is never supplied by the
/// application (V0-DOM-007) - though it always agrees with
/// <see cref="AccountTransactionDirectionRules.DirectionFor"/> by
/// construction.
/// </summary>
public sealed record AccountTransaction
{
    public Guid Id { get; }
    public Guid CustomerId { get; }
    public AccountTransactionType TransactionType { get; }
    public AccountTransactionDirection Direction { get; }
    public decimal Amount { get; }
    public string SourceReferenceType { get; }
    public Guid SourceReferenceId { get; }
    public string? Note { get; }
    public Guid? CreatedBy { get; }
    public DateTimeOffset OccurredAt { get; }

    public AccountTransaction(
        Guid id,
        Guid customerId,
        AccountTransactionType transactionType,
        AccountTransactionDirection direction,
        decimal amount,
        string sourceReferenceType,
        Guid sourceReferenceId,
        string? note,
        Guid? createdBy,
        DateTimeOffset occurredAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id must not be empty.", nameof(id));
        if (customerId == Guid.Empty)
            throw new ArgumentException("CustomerId must not be empty.", nameof(customerId));
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceReferenceType);

        Id = id;
        CustomerId = customerId;
        TransactionType = transactionType;
        Direction = direction;
        Amount = amount;
        SourceReferenceType = sourceReferenceType;
        SourceReferenceId = sourceReferenceId;
        Note = note;
        CreatedBy = createdBy;
        OccurredAt = occurredAt;
    }

    /// <summary>
    /// The signed contribution to <c>receivable_balance</c> (V0-DOM-007
    /// section 2). Adjustment already carries its own sign in
    /// <see cref="Amount"/>, so its effect is the amount itself; every other
    /// type stores a non-negative magnitude, so Debit contributes +Amount
    /// and Credit contributes -Amount.
    /// </summary>
    public decimal SignedBalanceEffect => TransactionType == AccountTransactionType.Adjustment
        ? Amount
        : Direction == AccountTransactionDirection.Debit ? Amount : -Amount;
}
