namespace ALKAROS.Payments.CardSettlement;

/// <summary>
/// The result of processing (or replaying) a <see cref="CardSettlementRequest"/>.
/// </summary>
/// <param name="PaymentId">The Payment aggregate this attempt created.</param>
/// <param name="Outcome">The recorded outcome.</param>
/// <param name="AllocationId">Set only when <see cref="Outcome"/> is Approved.</param>
/// <param name="ApprovedAmount">Set only when <see cref="Outcome"/> is Approved.</param>
/// <param name="WasReplayed">
/// True when this result was reconstructed from an already-recorded attempt
/// (crash-and-resume or a genuine duplicate call) rather than freshly
/// processed.
/// </param>
public sealed record CardSettlementResult(
    Guid PaymentId,
    CardSettlementOutcome Outcome,
    Guid? AllocationId,
    decimal? ApprovedAmount,
    bool WasReplayed);
