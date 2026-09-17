namespace ALKAROS.Payments.PaymentAggregate;

/// <summary>
/// The Payment aggregate root (payments.payments). Enforces the canonical
/// Payment transition matrix (PDF:I.46A / PDF:II.5.2 / V0-DOM-001:
/// Initiated→Pending; Pending→Approved|Declined|Cancelled|Unknown;
/// Unknown→ReconciliationRequired; ReconciliationRequired→Approved|Declined|
/// Cancelled) and the requested/tendered/approved/change money invariants
/// (V0-DOM-004, V0-CMP-002). Refund transitions (Approved→Refunded|
/// PartiallyRefunded) are out of scope for this task — see
/// <see cref="PaymentStatus"/>.
///
/// Immutable, like Order/Bill: every mutating method returns a new instance
/// rather than mutating this one, so a caller can never observe a
/// partially-applied transition.
/// </summary>
public sealed class Payment
{
    private readonly List<PaymentStatusHistoryEntry> _history;

    public Payment(
        Guid id,
        Guid billId,
        decimal requestedAmount,
        string currencyCode = "TRY",
        decimal? tenderedAmount = null,
        decimal? approvedAmount = null,
        decimal changeAmount = 0m,
        PaymentStatus status = PaymentStatus.Initiated,
        IReadOnlyList<PaymentStatusHistoryEntry>? history = null,
        DateTimeOffset? initiatedAt = null,
        DateTimeOffset? tenderedAt = null,
        DateTimeOffset? approvedAt = null,
        DateTimeOffset? declinedAt = null,
        DateTimeOffset? cancelledAt = null,
        long rowVersion = 1,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Payment id cannot be empty.", nameof(id));
        if (billId == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(billId));
        if (string.IsNullOrWhiteSpace(currencyCode))
            throw new ArgumentException("Currency code cannot be empty.", nameof(currencyCode));
        if (requestedAmount <= 0)
            throw new InvalidPaymentAmountException(
                $"Requested amount '{requestedAmount}' must be greater than zero.");

        // Initiated<->tendered is a single fact, not two independently
        // settable fields: a payment has been tendered exactly when it has
        // left Initiated, and never before.
        if ((status == PaymentStatus.Initiated) != (tenderedAmount is null))
            throw new InvalidPaymentAmountException(
                $"Payment status '{status}' and tendered amount '{tenderedAmount?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null"}' are an invalid combination.");
        if (tenderedAmount is <= 0)
            throw new InvalidPaymentAmountException(
                $"Tendered amount '{tenderedAmount}' must be greater than zero.");

        // Approved amount exists exactly when the payment has reached
        // Approved (this task never produces Refunded/PartiallyRefunded,
        // the only other states where a settled amount could apply).
        if ((status == PaymentStatus.Approved) != (approvedAmount is not null))
            throw new InvalidPaymentAmountException(
                $"Payment status '{status}' and approved amount '{approvedAmount?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null"}' are an invalid combination.");
        if (approvedAmount is <= 0)
            throw new InvalidPaymentAmountException(
                $"Approved amount '{approvedAmount}' must be greater than zero.");
        if (approvedAmount is not null && approvedAmount > tenderedAmount)
            throw new InvalidPaymentAmountException(
                $"Approved amount '{approvedAmount}' cannot exceed tendered amount '{tenderedAmount}'.");

        if (changeAmount < 0)
            throw new InvalidPaymentAmountException(
                $"Change amount '{changeAmount}' cannot be negative.");
        if (approvedAmount is not null)
        {
            var expectedChange = tenderedAmount!.Value - approvedAmount.Value;
            if (changeAmount != expectedChange)
                throw new InvalidPaymentAmountException(
                    $"Change amount '{changeAmount}' does not reconcile with tendered '{tenderedAmount}' minus approved '{approvedAmount}'.");
        }
        else if (changeAmount != 0)
        {
            throw new InvalidPaymentAmountException(
                "Change amount cannot be set before the payment is approved.");
        }

        Id = id;
        BillId = billId;
        CurrencyCode = currencyCode;
        RequestedAmount = requestedAmount;
        TenderedAmount = tenderedAmount;
        ApprovedAmount = approvedAmount;
        ChangeAmount = changeAmount;
        Status = status;
        var at = updatedAt ?? DateTimeOffset.UtcNow;
        InitiatedAt = initiatedAt ?? at;
        TenderedAt = tenderedAt;
        ApprovedAt = approvedAt;
        DeclinedAt = declinedAt;
        CancelledAt = cancelledAt;
        RowVersion = rowVersion;
        CreatedAt = createdAt ?? InitiatedAt;
        UpdatedAt = at;
        _history = history is null ? [] : history.ToList();
    }

    public Guid Id { get; }
    public Guid BillId { get; }
    public string CurrencyCode { get; }

    /// <summary>The amount originally requested to be paid.</summary>
    public decimal RequestedAmount { get; }

    /// <summary>The amount actually handed over by the customer; null until tendered (Initiated→Pending).</summary>
    public decimal? TenderedAmount { get; }

    /// <summary>The amount actually applied toward the bill; null until Approved.</summary>
    public decimal? ApprovedAmount { get; }

    /// <summary>Tendered minus approved — the surplus returned to the customer (V0-DOM-004). Never negative.</summary>
    public decimal ChangeAmount { get; }

    public PaymentStatus Status { get; }
    public DateTimeOffset InitiatedAt { get; }
    public DateTimeOffset? TenderedAt { get; }
    public DateTimeOffset? ApprovedAt { get; }
    public DateTimeOffset? DeclinedAt { get; }
    public DateTimeOffset? CancelledAt { get; }
    public long RowVersion { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }
    public IReadOnlyList<PaymentStatusHistoryEntry> History => _history;

    /// <summary>
    /// Checks whether transitioning to <paramref name="target"/> is valid
    /// according to the canonical Payment transition matrix (V0-DOM-001).
    /// Refunded/PartiallyRefunded are never reachable through this
    /// aggregate — out of scope, see V0-DOM-003/V13-ALC-003/V13-ALC-004.
    /// </summary>
    public bool CanTransitionTo(PaymentStatus target) => target switch
    {
        PaymentStatus.Pending => Status == PaymentStatus.Initiated,
        PaymentStatus.Approved => Status is PaymentStatus.Pending or PaymentStatus.ReconciliationRequired,
        PaymentStatus.Declined => Status is PaymentStatus.Pending or PaymentStatus.ReconciliationRequired,
        PaymentStatus.Cancelled => Status is PaymentStatus.Pending or PaymentStatus.ReconciliationRequired,
        PaymentStatus.Unknown => Status == PaymentStatus.Pending,
        PaymentStatus.ReconciliationRequired => Status == PaymentStatus.Unknown,
        _ => false,
    };

    /// <summary>
    /// Records that the customer has handed over <paramref name="tenderedAmount"/>
    /// (Initiated→Pending). Zero or negative amounts are rejected.
    /// </summary>
    public Payment Tender(
        decimal tenderedAmount,
        string? reason = null,
        Guid? changedBy = null,
        DateTimeOffset? at = null)
    {
        if (!CanTransitionTo(PaymentStatus.Pending))
            throw new InvalidPaymentTransitionException(Id, Status, PaymentStatus.Pending);

        var when = at ?? DateTimeOffset.UtcNow;
        return new Payment(
            Id, BillId, RequestedAmount, CurrencyCode,
            tenderedAmount, ApprovedAmount, ChangeAmount,
            PaymentStatus.Pending,
            AppendHistory(PaymentStatus.Pending, reason, changedBy, when),
            InitiatedAt, when, ApprovedAt, DeclinedAt, CancelledAt,
            RowVersion, CreatedAt, when);
    }

    /// <summary>
    /// Approves the payment for <paramref name="approvedAmount"/> (Pending or
    /// ReconciliationRequired → Approved). The surplus over the approved
    /// amount becomes <see cref="ChangeAmount"/> (V0-DOM-004); approving more
    /// than was tendered is rejected.
    /// </summary>
    public Payment Approve(
        decimal approvedAmount,
        string? reason = null,
        Guid? changedBy = null,
        DateTimeOffset? at = null)
    {
        if (!CanTransitionTo(PaymentStatus.Approved))
            throw new InvalidPaymentTransitionException(Id, Status, PaymentStatus.Approved);

        var when = at ?? DateTimeOffset.UtcNow;
        var change = TenderedAmount!.Value - approvedAmount;
        return new Payment(
            Id, BillId, RequestedAmount, CurrencyCode,
            TenderedAmount, approvedAmount, change,
            PaymentStatus.Approved,
            AppendHistory(PaymentStatus.Approved, reason, changedBy, when),
            InitiatedAt, TenderedAt, when, DeclinedAt, CancelledAt,
            RowVersion, CreatedAt, when);
    }

    /// <summary>Declines the payment (Pending or ReconciliationRequired → Declined).</summary>
    public Payment Decline(string? reason = null, Guid? changedBy = null, DateTimeOffset? at = null)
    {
        if (!CanTransitionTo(PaymentStatus.Declined))
            throw new InvalidPaymentTransitionException(Id, Status, PaymentStatus.Declined);

        var when = at ?? DateTimeOffset.UtcNow;
        return new Payment(
            Id, BillId, RequestedAmount, CurrencyCode,
            TenderedAmount, ApprovedAmount, ChangeAmount,
            PaymentStatus.Declined,
            AppendHistory(PaymentStatus.Declined, reason, changedBy, when),
            InitiatedAt, TenderedAt, ApprovedAt, when, CancelledAt,
            RowVersion, CreatedAt, when);
    }

    /// <summary>Cancels the payment (Pending or ReconciliationRequired → Cancelled).</summary>
    public Payment Cancel(string? reason = null, Guid? changedBy = null, DateTimeOffset? at = null)
    {
        if (!CanTransitionTo(PaymentStatus.Cancelled))
            throw new InvalidPaymentTransitionException(Id, Status, PaymentStatus.Cancelled);

        var when = at ?? DateTimeOffset.UtcNow;
        return new Payment(
            Id, BillId, RequestedAmount, CurrencyCode,
            TenderedAmount, ApprovedAmount, ChangeAmount,
            PaymentStatus.Cancelled,
            AppendHistory(PaymentStatus.Cancelled, reason, changedBy, when),
            InitiatedAt, TenderedAt, ApprovedAt, DeclinedAt, when,
            RowVersion, CreatedAt, when);
    }

    /// <summary>
    /// Marks the payment Unknown after a provider timeout (Pending→Unknown).
    /// Not an implicit approval or decline (CORR:C29) — a
    /// ReconciliationCase-driven caller (V13-PAY-003/V13-HUG-*) decides the
    /// eventual outcome via <see cref="RequestReconciliation"/> then
    /// <see cref="Approve"/>/<see cref="Decline"/>/<see cref="Cancel"/>.
    /// </summary>
    public Payment MarkUnknown(string? reason = null, Guid? changedBy = null, DateTimeOffset? at = null)
    {
        if (!CanTransitionTo(PaymentStatus.Unknown))
            throw new InvalidPaymentTransitionException(Id, Status, PaymentStatus.Unknown);

        var when = at ?? DateTimeOffset.UtcNow;
        return new Payment(
            Id, BillId, RequestedAmount, CurrencyCode,
            TenderedAmount, ApprovedAmount, ChangeAmount,
            PaymentStatus.Unknown,
            AppendHistory(PaymentStatus.Unknown, reason, changedBy, when),
            InitiatedAt, TenderedAt, ApprovedAt, DeclinedAt, CancelledAt,
            RowVersion, CreatedAt, when);
    }

    /// <summary>Opens reconciliation for an Unknown payment (Unknown→ReconciliationRequired).</summary>
    public Payment RequestReconciliation(string? reason = null, Guid? changedBy = null, DateTimeOffset? at = null)
    {
        if (!CanTransitionTo(PaymentStatus.ReconciliationRequired))
            throw new InvalidPaymentTransitionException(Id, Status, PaymentStatus.ReconciliationRequired);

        var when = at ?? DateTimeOffset.UtcNow;
        return new Payment(
            Id, BillId, RequestedAmount, CurrencyCode,
            TenderedAmount, ApprovedAmount, ChangeAmount,
            PaymentStatus.ReconciliationRequired,
            AppendHistory(PaymentStatus.ReconciliationRequired, reason, changedBy, when),
            InitiatedAt, TenderedAt, ApprovedAt, DeclinedAt, CancelledAt,
            RowVersion, CreatedAt, when);
    }

    private List<PaymentStatusHistoryEntry> AppendHistory(
        PaymentStatus target, string? reason, Guid? changedBy, DateTimeOffset at)
        => _history.Append(new PaymentStatusHistoryEntry(Guid.NewGuid(), Id, Status, target, reason, changedBy, at)).ToList();
}
