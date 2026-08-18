namespace ALKAROS.Tables.CurrentPointers;

/// <summary>
/// Domain projector interface for detecting and rebuilding table cache pointers from authoritative records (V1-TBL-005).
/// </summary>
public interface ITablePointerProjector
{
    /// <summary>
    /// Checks a specific table for pointer/status drift against authoritative orders and bills.
    /// </summary>
    Task<TablePointerDiscrepancy?> DetectTableDriftAsync(
        Guid tableId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans all active tables in the system and returns discrepancies where cache pointers drifted.
    /// </summary>
    Task<IReadOnlyList<TablePointerDiscrepancy>> DetectAllDriftAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically calculates and projects authoritative pointers onto a single table, updating row_version if modified.
    /// </summary>
    Task<TablePointerRebuildResult> RebuildTablePointersAsync(
        Guid tableId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans and rebuilds pointer projections for all tables in the system transactionally.
    /// </summary>
    Task<TablePointerRebuildSummary> RebuildAllTablePointersAsync(
        CancellationToken cancellationToken = default);
}
