namespace ALKAROS.Tables.TableMerge;

/// <summary>
/// Data access contract for reversible table merge operations (V1-TBL-003, PDF:I.10, PDF:III.5.4).
/// </summary>
public interface ITableMergeRepository
{
    /// <summary>
    /// Retrieves a single table merge record by ID.
    /// </summary>
    Task<TableMergeRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all merge participant records for a merge group.
    /// </summary>
    Task<IReadOnlyList<TableMergeRecord>> GetByGroupIdAsync(Guid mergeGroupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves active merge records where the table is the primary table.
    /// </summary>
    Task<IReadOnlyList<TableMergeRecord>> GetActiveByPrimaryTableAsync(Guid primaryTableId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves active merge record where the table is a merged participant.
    /// </summary>
    Task<TableMergeRecord?> GetActiveByMergedTableAsync(Guid mergedTableId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes atomic table merge across primary and participant tables in a single transaction.
    /// </summary>
    Task<TableMergeResult> ExecuteMergeAsync(
        TableMergeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes atomic table unmerge (undo), restoring original table associations in a single transaction.
    /// </summary>
    Task<TableUnmergeResult> ExecuteUnmergeAsync(
        TableUnmergeRequest request,
        CancellationToken cancellationToken = default);
}
