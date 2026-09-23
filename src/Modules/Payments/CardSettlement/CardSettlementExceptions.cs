namespace ALKAROS.Payments.CardSettlement;

/// <summary>Base exception for card settlement orchestration errors (V13-PAY-004).</summary>
public abstract class CardSettlementException : Exception
{
    protected CardSettlementException(string message) : base(message) { }
}

/// <summary>
/// Thrown when a replayed idempotency key does not reproduce the exact
/// attempt already recorded under it (a different provider correlation id,
/// or a different outcome/approved amount) — a genuine data mismatch is
/// never silently accepted as a replay.
/// </summary>
public sealed class CardSettlementReplayMismatchException : CardSettlementException
{
    public CardSettlementReplayMismatchException(string idempotencyKey, string reason)
        : base($"Card settlement attempt '{idempotencyKey}' does not match the previously recorded attempt: {reason}")
    {
        IdempotencyKey = idempotencyKey;
    }

    public string IdempotencyKey { get; }
}

/// <summary>Thrown when the Bill a settlement attempt targets does not exist.</summary>
public sealed class CardSettlementBillNotFoundException : CardSettlementException
{
    public CardSettlementBillNotFoundException(Guid billId)
        : base($"Bill '{billId}' was not found.")
    {
        BillId = billId;
    }

    public Guid BillId { get; }
}

/// <summary>
/// V1-RMD-258: thrown when a NEW settlement attempt (no existing attempt row
/// under this idempotency key) targets a Bill that already has a Payment
/// sitting at <c>Pending</c>/<c>Unknown</c>/<c>ReconciliationRequired</c> —
/// a genuinely new attempt must never be layered on top of an already
/// unresolved one (that would risk two attempts eventually both trying to
/// settle the same remaining amount). The existing unresolved attempt must
/// be reconciled first (V13-REC-001).
/// </summary>
public sealed class CardSettlementUnsettledPaymentExistsException : CardSettlementException
{
    public CardSettlementUnsettledPaymentExistsException(Guid billId, Guid existingPaymentId, string existingStatus)
        : base($"Bill '{billId}' already has a payment '{existingPaymentId}' at status '{existingStatus}'; " +
               "it must be reconciled before a new card settlement attempt can be recorded.")
    {
        BillId = billId;
        ExistingPaymentId = existingPaymentId;
        ExistingStatus = existingStatus;
    }

    public Guid BillId { get; }
    public Guid ExistingPaymentId { get; }
    public string ExistingStatus { get; }
}

/// <summary>
/// V1-RMD-258: thrown when a replayed idempotency key's recorded attempt
/// belongs to a DIFFERENT Bill than the one the current request names — a
/// genuine cross-bill idempotency-key collision (key-generation bug or a
/// stale/replayed client context), never silently accepted as a valid
/// replay for the wrong bill.
/// </summary>
public sealed class CardSettlementBillMismatchException : CardSettlementException
{
    public CardSettlementBillMismatchException(string idempotencyKey, Guid recordedBillId, Guid requestedBillId)
        : base($"Card settlement attempt '{idempotencyKey}' was recorded against bill '{recordedBillId}', " +
               $"not the requested bill '{requestedBillId}'.")
    {
        IdempotencyKey = idempotencyKey;
        RecordedBillId = recordedBillId;
        RequestedBillId = requestedBillId;
    }

    public string IdempotencyKey { get; }
    public Guid RecordedBillId { get; }
    public Guid RequestedBillId { get; }
}
