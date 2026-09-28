namespace ALKAROS.CustomerData.AnonymizationState;

/// <summary>
/// V14-CST-002's own four states (Goal): a customer anonymization request
/// starts life resolved into either <see cref="RetentionBlocked"/> or
/// <see cref="Pending"/> by an <see cref="IAnonymizationRetentionGuard"/>
/// check made at request time, then only ever moves forward -
/// <see cref="Anonymized"/> is terminal (V0-DOM-001's general contract:
/// terminal states cannot silently reopen). See
/// <see cref="CustomerAnonymizationTransitions"/> for the explicit edge set.
/// </summary>
public enum AnonymizationRequestStatus
{
    /// <summary>Transient: only ever observed mid-call inside <c>CustomerAnonymizationService.RequestAsync</c>, never persisted - a stored row is always already resolved into RetentionBlocked or Pending.</summary>
    Requested,
    RetentionBlocked,
    Pending,
    Anonymized,
}
