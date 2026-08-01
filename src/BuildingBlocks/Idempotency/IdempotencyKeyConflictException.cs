namespace ALKAROS.Idempotency;

/// <summary>
/// Raised when a client resends an operation id with a different request
/// body than the one that created the record (V0-ARC-003 §1: same key +
/// different hash is rejected as IDEMPOTENCY_KEY_CONFLICT).
/// </summary>
public sealed class IdempotencyKeyConflictException : InvalidOperationException
{
    public IdempotencyKeyConflictException(IdempotencyKey key)
        : base(
            $"Idempotency key conflict: client '{key.ClientId}' reused operation id "
            + $"'{key.OperationId}' with a different request body.")
    {
        Key = key;
    }

    public IdempotencyKey Key { get; }
}
