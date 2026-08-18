namespace ALKAROS.Tables.CurrentPointers;

/// <summary>
/// Diagnostic report describing drift between a table's cached pointers and authoritative owner relations (V1-TBL-005).
/// </summary>
public sealed record TablePointerDiscrepancy(
    Guid TableId,
    string TableNumber,
    string CurrentStatus,
    string ProjectedStatus,
    Guid? CurrentOrderId,
    Guid? AuthoritativeOrderId,
    Guid? CurrentBillId,
    Guid? AuthoritativeBillId,
    TablePointerDriftType DriftTypes)
{
    public bool HasDrift => DriftTypes != TablePointerDriftType.None;
}
