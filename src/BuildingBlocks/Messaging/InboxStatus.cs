namespace ALKAROS.Messaging;

/// <summary>
/// Lifecycle of an inbox message (V0-ARC-003 §2): pending until a dispatcher
/// claims it, in flight while the lease is held, processed after successful
/// handling, dead after the poison threshold is reached.
/// </summary>
public enum InboxStatus
{
    Pending = 0,
    Processed = 1,
    Dead = 2,
    InFlight = 3,
}
