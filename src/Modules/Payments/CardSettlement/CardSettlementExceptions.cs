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
