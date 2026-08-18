namespace ALKAROS.Clients.WaiterPwa.SessionQueue;

/// <summary>
/// Type of operations that can be queued by Waiter PWA (V1-WTR-001).
/// </summary>
public enum QueuedOperationType
{
    SubmitOrder,
    AddOrderNote,
    UpdateTableStatus,
    DirectPaymentSettlement // Explicitly UNSUPPORTED in offline mode
}

/// <summary>
/// A queued operation in the Waiter PWA local storage (V1-WTR-001).
/// </summary>
public sealed record QueuedOperation(
    Guid OperationId,
    string IdempotencyKey,
    QueuedOperationType OperationType,
    string PayloadJson,
    DateTimeOffset QueuedAt,
    int RetryAttempts = 0);

/// <summary>
/// Result of attempting to enqueue or replay an operation (V1-WTR-001).
/// </summary>
public sealed record QueueOperationResult(
    bool IsEnqueued,
    bool IsReplayed,
    string? ErrorMessage,
    bool IsRejectedUnsupportedOffline);
