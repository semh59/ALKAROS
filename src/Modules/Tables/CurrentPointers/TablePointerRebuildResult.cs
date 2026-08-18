namespace ALKAROS.Tables.CurrentPointers;

/// <summary>
/// Result of an individual table pointer rebuild operation (V1-TBL-005).
/// </summary>
public sealed record TablePointerRebuildResult(
    Guid TableId,
    string TableNumber,
    string PreviousStatus,
    string NewStatus,
    Guid? PreviousOrderId,
    Guid? NewOrderId,
    Guid? PreviousBillId,
    Guid? NewBillId,
    long PreviousRowVersion,
    long NewRowVersion,
    TablePointerDriftType CorrectedDrift,
    bool WasModified,
    DateTimeOffset RebuiltAt);

/// <summary>
/// Summary report of a system-wide table pointer projection rebuild execution (V1-TBL-005).
/// </summary>
public sealed record TablePointerRebuildSummary(
    int TotalScannedTables,
    int DriftedTablesCount,
    int RebuiltTablesCount,
    IReadOnlyList<TablePointerRebuildResult> Results,
    IReadOnlyList<TablePointerDiscrepancy> DetectedDiscrepancies,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt)
{
    public TimeSpan Duration => CompletedAt - StartedAt;
    public bool AllClean => DriftedTablesCount == 0;
}
