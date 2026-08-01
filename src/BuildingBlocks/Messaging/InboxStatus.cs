namespace ALKAROS.Messaging;

/// <summary>
/// Lifecycle of an inbox message (V0-ARC-003 §2): pending until processed,
/// dead after the poison threshold is reached.
/// </summary>
public enum InboxStatus
{
    Pending = 0,
    Processed = 1,
    Dead = 2,
}
