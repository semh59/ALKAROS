namespace ALKAROS.CustomerAccounts.TransactionLedger;

/// <summary>
/// A caller's request to append one ledger row. Self-validates every
/// invariant `docs/domain/customer-credit-invoice-semantics.md` states
/// before anything reaches the store - the store itself only persists and
/// idempotency-checks (V0-DOM-007's own separation: the decision record
/// owns the rules, the ledger owns storage).
/// </summary>
public sealed record RecordAccountTransactionRequest
{
    public Guid CustomerId { get; }
    public AccountTransactionType TransactionType { get; }
    public decimal Amount { get; }

    /// <summary>
    /// The immutable source event this row represents (e.g. "Bill"/a bill
    /// id for a Charge, "Payment"/a payment id for a Payment). Together with
    /// <see cref="CustomerId"/> and <see cref="TransactionType"/>, this is
    /// the idempotency key `IAccountTransactionLedger.RecordAsync` dedupes
    /// on - a retried source event never creates a second ledger row.
    /// </summary>
    public string SourceReferenceType { get; }
    public Guid SourceReferenceId { get; }
    public string? Note { get; }
    public Guid? CreatedBy { get; }
    public DateTimeOffset OccurredAt { get; }

    public RecordAccountTransactionRequest(
        Guid customerId,
        AccountTransactionType transactionType,
        decimal amount,
        string sourceReferenceType,
        Guid sourceReferenceId,
        string? note,
        Guid? createdBy,
        DateTimeOffset occurredAt)
    {
        if (customerId == Guid.Empty)
            throw new ArgumentException("CustomerId must not be empty.", nameof(customerId));
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceReferenceType);
        if (sourceReferenceId == Guid.Empty)
            throw new ArgumentException("SourceReferenceId must not be empty.", nameof(sourceReferenceId));
        if (amount == 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "A zero-amount transaction has no balance effect.");

        // CORR:C3 / V0-DOM-007 invariant 5: amount is a non-negative
        // magnitude for every type except Adjustment, which carries its own
        // sign.
        if (transactionType != AccountTransactionType.Adjustment && amount < 0)
            throw new ArgumentOutOfRangeException(
                nameof(amount), amount, $"{transactionType} amounts must be a non-negative magnitude; only Adjustment carries a sign.");

        // V0-DOM-007 invariant 5: "an Adjustment with negative amount
        // requires a note and created_by" - stated for negative Adjustment
        // specifically, not every Adjustment.
        if (transactionType == AccountTransactionType.Adjustment && amount < 0
            && (string.IsNullOrWhiteSpace(note) || createdBy is null))
            throw new ArgumentException("A negative Adjustment requires both a Note and a CreatedBy actor.", nameof(note));

        CustomerId = customerId;
        TransactionType = transactionType;
        Amount = amount;
        SourceReferenceType = sourceReferenceType;
        SourceReferenceId = sourceReferenceId;
        Note = note;
        CreatedBy = createdBy;
        OccurredAt = occurredAt;
    }
}
