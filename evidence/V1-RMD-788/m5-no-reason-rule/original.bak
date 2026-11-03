using ALKAROS.Cash.Contracts;

namespace ALKAROS.Cash.TransactionLedger;

/// <summary>Which way a <see cref="CashTransaction"/> moves the drawer's expected cash.</summary>
public enum CashTransactionDirection
{
    In,
    Out,
}

/// <summary>
/// A single, append-only cash-drawer movement (V13-CSH-002, PDF:I.38-I.44,
/// PDF:II.2.7, PDF:II.5.9, PDF:III.9.2). Never updated: a correction is
/// always a new row (a CountAdjustment), never a silent overwrite of an
/// existing one (Acceptance evidence).
///
/// <see cref="Amount"/> is always a positive magnitude; <see cref="Direction"/>
/// carries the sign. Opening/Sale/CashIn are always In and CashOut/Refund
/// are always Out (the constructor refuses any other direction for those);
/// CountAdjustment and ClosingDifference may legitimately go either way (a
/// recount can be short or over, a closing variance can be a shortage or
/// an overage), so their direction is caller-supplied.
/// </summary>
public sealed class CashTransaction
{
    /// <summary>The largest value the NUMERIC(18,2) ledger column holds.</summary>
    public const decimal MaxAmount = 9_999_999_999_999_999.99m;

    /// <summary>The longest reason a manual cash movement may carry.</summary>
    public const int MaxReasonLength = 500;

    /// <summary>True for a positive amount with at most two decimals that fits the ledger column exactly.</summary>
    public static bool IsStorableAmount(decimal amount)
        => amount > 0 && amount <= MaxAmount && decimal.Round(amount, 2) == amount;

    public CashTransaction(
        Guid id,
        Guid cashSessionId,
        CashTransactionType type,
        decimal amount,
        CashTransactionDirection direction,
        Guid? relatedPaymentId = null,
        string? notes = null,
        Guid? recordedBy = null,
        DateTimeOffset? occurredAt = null,
        string? idempotencyKey = null)
        : this(id, cashSessionId, type, amount, direction, relatedPaymentId, notes, recordedBy, occurredAt, idempotencyKey, requireMovementReason: true)
    {
    }

    /// <summary>Rebuilds a stored row; rows written before the reason rule may have none and must still load.</summary>
    internal static CashTransaction Restore(
        Guid id, Guid cashSessionId, CashTransactionType type, decimal amount, CashTransactionDirection direction,
        Guid? relatedPaymentId, string? notes, Guid? recordedBy, DateTimeOffset occurredAt, string? idempotencyKey)
        => new(id, cashSessionId, type, amount, direction, relatedPaymentId, notes, recordedBy, occurredAt, idempotencyKey, requireMovementReason: false);

    private CashTransaction(
        Guid id,
        Guid cashSessionId,
        CashTransactionType type,
        decimal amount,
        CashTransactionDirection direction,
        Guid? relatedPaymentId,
        string? notes,
        Guid? recordedBy,
        DateTimeOffset? occurredAt,
        string? idempotencyKey,
        bool requireMovementReason)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Cash transaction id cannot be empty.", nameof(id));
        if (cashSessionId == Guid.Empty)
            throw new ArgumentException("Cash session id cannot be empty.", nameof(cashSessionId));
        if (!IsStorableAmount(amount))
            throw new InvalidCashTransactionAmountException(amount);

        var fixedDirection = FixedDirectionFor(type);
        if (fixedDirection is not null && fixedDirection != direction)
            throw new InvalidCashTransactionDirectionException(type, direction, fixedDirection.Value);

        // Payment linkage (In scope): a Sale/Refund entry IS a cash movement
        // against a real Payment and must say which one; every other type
        // has no payment to point at.
        var requiresPayment = type is CashTransactionType.Sale or CashTransactionType.Refund;
        if (requiresPayment && (relatedPaymentId is null || relatedPaymentId.Value == Guid.Empty))
            throw new MissingRelatedPaymentException(type);
        if (!requiresPayment && relatedPaymentId is not null)
            throw new UnexpectedRelatedPaymentException(type);

        // Explicit correction requirement: a recount adjustment must
        // always state why, never a bare unexplained number.
        if (type == CashTransactionType.CountAdjustment && string.IsNullOrWhiteSpace(notes))
            throw new MissingCountAdjustmentReasonException();

        // A manual cash-in/cash-out is money moving without a sale: it always says why.
        if (requireMovementReason && type is CashTransactionType.CashIn or CashTransactionType.CashOut)
        {
            notes = notes?.Trim();
            if (string.IsNullOrEmpty(notes) || notes.Length > MaxReasonLength)
                throw new InvalidCashMovementReasonException();
        }

        Id = id;
        CashSessionId = cashSessionId;
        Type = type;
        Amount = amount;
        Direction = direction;
        RelatedPaymentId = relatedPaymentId;
        Notes = notes;
        RecordedBy = recordedBy;
        OccurredAt = occurredAt ?? DateTimeOffset.UtcNow;
        IdempotencyKey = idempotencyKey;
    }

    public Guid Id { get; }
    public Guid CashSessionId { get; }
    public CashTransactionType Type { get; }
    public decimal Amount { get; }
    public CashTransactionDirection Direction { get; }
    public Guid? RelatedPaymentId { get; }
    public string? Notes { get; }
    public Guid? RecordedBy { get; }
    public DateTimeOffset OccurredAt { get; }

    /// <summary>
    /// V1-RMD-241: caller-supplied retry key for endpoints that expose one
    /// (currently only the manual cash-in/cash-out endpoint). Null for
    /// every other production entry (Opening/Sale/Refund), whose own
    /// callers are already idempotent by construction.
    /// </summary>
    public string? IdempotencyKey { get; }

    /// <summary>The signed contribution this entry makes to the drawer's running cash.</summary>
    public decimal SignedAmount => Direction == CashTransactionDirection.In ? Amount : -Amount;

    /// <summary>
    /// The entries that feed the running "ExpectedCash" total
    /// (docs/domain/cash-session-design.md §4 formula). CountAdjustment and
    /// ClosingDifference are audit-trail entries recorded AFTER the fact,
    /// not inputs into that same running total.
    /// </summary>
    public bool ContributesToExpectedCash => Type is not (CashTransactionType.CountAdjustment or CashTransactionType.ClosingDifference);

    private static CashTransactionDirection? FixedDirectionFor(CashTransactionType type) => type switch
    {
        CashTransactionType.Opening or CashTransactionType.Sale or CashTransactionType.CashIn => CashTransactionDirection.In,
        CashTransactionType.CashOut or CashTransactionType.Refund => CashTransactionDirection.Out,
        CashTransactionType.CountAdjustment or CashTransactionType.ClosingDifference => null,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown cash transaction type."),
    };
}
