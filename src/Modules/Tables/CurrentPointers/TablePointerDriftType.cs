namespace ALKAROS.Tables.CurrentPointers;

/// <summary>
/// Bitwise flags representing types of pointer and status drift between soft cache pointers and authoritative records (V1-TBL-005).
/// </summary>
[Flags]
public enum TablePointerDriftType
{
    None = 0,
    MissingOrderPointer = 1 << 0,
    StaleOrderPointer = 1 << 1,
    MissingBillPointer = 1 << 2,
    StaleBillPointer = 1 << 3,
    StatusMismatch = 1 << 4,
    GhostOrderPointer = 1 << 5,
    GhostBillPointer = 1 << 6
}
