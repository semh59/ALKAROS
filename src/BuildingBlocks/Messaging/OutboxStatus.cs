namespace ALKAROS.Messaging;

/// <summary>
/// Lifecycle of an outbox message (V0-ARC-003 §3): pending until a
/// dispatcher claims it, in flight while the lease is held, dispatched after
/// confirmed delivery, dead after the retry threshold.
/// </summary>
public enum OutboxStatus
{
    Pending = 0,
    Dispatched = 1,
    Dead = 2,
    InFlight = 3,
}
