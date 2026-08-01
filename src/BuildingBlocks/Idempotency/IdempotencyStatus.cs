namespace ALKAROS.Idempotency;

/// <summary>
/// The outcome of an idempotency registration (V0-ARC-003 §1).
/// <see cref="Created"/> means the operation ran for the first time;
/// <see cref="Replayed"/> means the same key and request hash were seen
/// before and the cached response is returned.
/// </summary>
public enum IdempotencyStatus
{
    Created = 0,
    Replayed = 1,
}
