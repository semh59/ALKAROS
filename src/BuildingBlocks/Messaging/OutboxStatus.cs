namespace ALKAROS.Messaging;

/// <summary>
/// Lifecycle of an outbox message (V0-ARC-003 §3): pending until the
/// dispatcher confirms delivery, dead after the retry threshold.
/// </summary>
public enum OutboxStatus
{
    Pending = 0,
    Dispatched = 1,
    Dead = 2,
}
