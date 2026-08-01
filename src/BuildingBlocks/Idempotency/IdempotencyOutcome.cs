namespace ALKAROS.Idempotency;

/// <summary>
/// The result of <see cref="IdempotencyKeyStore.RegisterOrReplayAsync"/>:
/// whether the operation was newly created or replayed, and the envelope
/// bytes that must be returned to the caller (the cached response on replay).
/// </summary>
public sealed record IdempotencyOutcome(IdempotencyStatus Status, byte[] ResponseEnvelope)
{
    public bool IsReplay => Status == IdempotencyStatus.Replayed;
}
