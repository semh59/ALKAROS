namespace ALKAROS.Idempotency;

/// <summary>
/// The per-client idempotency scope defined by V0-ARC-003 §1: a client
/// identifies its operation with a client-supplied operation id. The pair
/// is the storage key of an idempotency record.
/// </summary>
public sealed record IdempotencyKey
{
    private const int MaxLength = 100;

    public string ClientId { get; }

    public string OperationId { get; }

    public IdempotencyKey(string clientId, string operationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        if (clientId.Length > MaxLength || operationId.Length > MaxLength)
            throw new ArgumentException(
                $"Client id and operation id must not exceed {MaxLength} characters.",
                nameof(operationId));

        ClientId = clientId;
        OperationId = operationId;
    }
}
