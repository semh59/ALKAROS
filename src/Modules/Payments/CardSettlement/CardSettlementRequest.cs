using ALKAROS.Payments.TenderRouting;

namespace ALKAROS.Payments.CardSettlement;

/// <summary>
/// A request to durably record the outcome of a BankCard settlement attempt
/// against a Bill. The caller (a future V13-HUG-001/V13-PAY-003 terminal
/// integration) has already obtained <paramref name="Result"/> from a real
/// <see cref="ITenderHandler"/> call — this orchestrator never calls a
/// terminal itself (Out of scope: terminal protocol).
/// </summary>
/// <param name="BillId">The Bill this settlement attempt pays toward.</param>
/// <param name="AttemptedAmount">
/// The amount the terminal was asked to charge. Used as the Payment's
/// requested/tendered amount regardless of outcome; for an Approved outcome
/// the terminal's own <see cref="TenderApproved.ApprovedAmount"/> is what
/// actually gets applied (see <see cref="ICardSettlementOrchestrator"/>).
/// </param>
/// <param name="IdempotencyKey">
/// The correlation key this orchestrator locks on and deduplicates by. A
/// retry (crash-and-resume) must supply the exact same key to replay the
/// prior outcome instead of processing again.
/// </param>
/// <param name="ProviderCorrelationId">
/// The terminal's own transaction reference for this attempt. A replay
/// with the same <paramref name="IdempotencyKey"/> but a different
/// <paramref name="ProviderCorrelationId"/> (or a different
/// <paramref name="Result"/> shape) is a genuine mismatch, never silently
/// accepted as a replay.
/// </param>
/// <param name="Result">The already-obtained tender outcome to record.</param>
public sealed record CardSettlementRequest(
    Guid BillId,
    decimal AttemptedAmount,
    string IdempotencyKey,
    string ProviderCorrelationId,
    TenderHandlerResult Result)
{
    public void Validate()
    {
        if (BillId == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(BillId));
        if (AttemptedAmount <= 0)
            throw new ArgumentException("Attempted amount must be greater than zero.", nameof(AttemptedAmount));
        if (string.IsNullOrWhiteSpace(IdempotencyKey))
            throw new ArgumentException("Idempotency key cannot be empty.", nameof(IdempotencyKey));
        if (string.IsNullOrWhiteSpace(ProviderCorrelationId))
            throw new ArgumentException("Provider correlation id cannot be empty.", nameof(ProviderCorrelationId));
        if (Result is null)
            throw new ArgumentException("Result cannot be null.", nameof(Result));
        if (Result is TenderApproved approved && approved.ApprovedAmount > AttemptedAmount)
            throw new ArgumentException(
                "Approved amount cannot exceed the attempted amount.", nameof(Result));
    }
}
