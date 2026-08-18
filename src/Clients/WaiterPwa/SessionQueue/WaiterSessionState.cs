namespace ALKAROS.Clients.WaiterPwa.SessionQueue;

/// <summary>
/// Domain model for Waiter PWA mobile device session (V1-WTR-001, PDF:I.7, PDF:I.14-I.15, V1-IAM-003).
/// </summary>
public sealed record WaiterPwaSession(
    Guid SessionId,
    Guid WaiterId,
    string WaiterName,
    string DeviceFingerprint,
    DateTimeOffset ExpiresAt,
    bool IsActive,
    bool IsRevoked)
{
    public bool IsValid(DateTimeOffset utcNow) => IsActive && !IsRevoked && utcNow < ExpiresAt;
}
