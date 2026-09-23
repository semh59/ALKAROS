namespace ALKAROS.Payments.CardSettlement;

/// <summary>
/// A persisted row of payments.card_settlement_attempts — the durable
/// record that makes resume, duplicate suppression, and provider-mismatch
/// detection possible (V13-PAY-004 In scope).
/// </summary>
public sealed class CardSettlementAttempt
{
    public CardSettlementAttempt(
        Guid id,
        Guid billId,
        string idempotencyKey,
        string providerCorrelationId,
        Guid paymentId,
        CardSettlementOutcome outcome,
        decimal? approvedAmount,
        Guid? allocationId,
        string? reason,
        bool fiscalHandoffQueued,
        DateTimeOffset? createdAt = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Attempt id cannot be empty.", nameof(id));
        if (billId == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(billId));
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key cannot be empty.", nameof(idempotencyKey));
        if (string.IsNullOrWhiteSpace(providerCorrelationId))
            throw new ArgumentException("Provider correlation id cannot be empty.", nameof(providerCorrelationId));
        if (paymentId == Guid.Empty)
            throw new ArgumentException("Payment id cannot be empty.", nameof(paymentId));

        var isApproved = outcome == CardSettlementOutcome.Approved;
        if (isApproved != (approvedAmount is not null))
            throw new ArgumentException(
                $"Outcome '{outcome}' and approved amount '{approvedAmount}' are an invalid combination.");
        if (isApproved != (allocationId is not null))
            throw new ArgumentException(
                $"Outcome '{outcome}' and allocation id '{allocationId}' are an invalid combination.");
        if (isApproved != fiscalHandoffQueued)
            throw new ArgumentException(
                $"Outcome '{outcome}' and fiscal handoff queued '{fiscalHandoffQueued}' are an invalid combination.");
        if (approvedAmount is <= 0)
            throw new ArgumentException("Approved amount must be greater than zero.", nameof(approvedAmount));

        Id = id;
        BillId = billId;
        IdempotencyKey = idempotencyKey;
        ProviderCorrelationId = providerCorrelationId;
        PaymentId = paymentId;
        Outcome = outcome;
        ApprovedAmount = approvedAmount;
        AllocationId = allocationId;
        Reason = reason;
        FiscalHandoffQueued = fiscalHandoffQueued;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
    }

    public Guid Id { get; }

    /// <summary>
    /// V1-RMD-258: the Bill this attempt was recorded against — added so a
    /// replay can detect a genuine cross-bill idempotency-key collision
    /// (a key reused for a different bill than the one it was first recorded
    /// under) instead of returning the wrong bill's result.
    /// </summary>
    public Guid BillId { get; }
    public string IdempotencyKey { get; }
    public string ProviderCorrelationId { get; }
    public Guid PaymentId { get; }
    public CardSettlementOutcome Outcome { get; }
    public decimal? ApprovedAmount { get; }
    public Guid? AllocationId { get; }
    public string? Reason { get; }
    public bool FiscalHandoffQueued { get; }
    public DateTimeOffset CreatedAt { get; }
}
