namespace ALKAROS.Tables.TableMerge;

/// <summary>
/// A persistent, reversible record of a table merge (table_mgmt.table_merges, PDF:I.10, PDF:III.5.4).
/// Records multi-table merge membership without deleting physical tables, orders, or bills.
/// </summary>
public sealed class TableMergeRecord
{
    public TableMergeRecord(
        Guid id,
        Guid mergeGroupId,
        Guid primaryTableId,
        Guid mergedTableId,
        Guid? originalOrderId,
        Guid? originalBillId,
        TableMergeStatus status,
        string reason,
        Guid mergedBy,
        DateTimeOffset mergedAt,
        DateTimeOffset? unmergedAt = null,
        Guid? unmergedBy = null,
        string? unmergeReason = null,
        long rowVersion = 1)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Merge ID cannot be empty.", nameof(id));
        if (mergeGroupId == Guid.Empty)
            throw new ArgumentException("Merge group ID cannot be empty.", nameof(mergeGroupId));
        if (primaryTableId == Guid.Empty)
            throw new ArgumentException("Primary table ID cannot be empty.", nameof(primaryTableId));
        if (mergedTableId == Guid.Empty)
            throw new ArgumentException("Merged table ID cannot be empty.", nameof(mergedTableId));
        if (primaryTableId == mergedTableId)
            throw new ArgumentException("Primary table and merged table cannot be the same.", nameof(mergedTableId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason cannot be empty.", nameof(reason));
        if (mergedBy == Guid.Empty)
            throw new ArgumentException("MergedBy user ID cannot be empty.", nameof(mergedBy));
        if (rowVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(rowVersion), "Row version must be positive.");

        Id = id;
        MergeGroupId = mergeGroupId;
        PrimaryTableId = primaryTableId;
        MergedTableId = mergedTableId;
        OriginalOrderId = originalOrderId;
        OriginalBillId = originalBillId;
        Status = status;
        Reason = reason;
        MergedBy = mergedBy;
        MergedAt = mergedAt;
        UnmergedAt = unmergedAt;
        UnmergedBy = unmergedBy;
        UnmergeReason = unmergeReason;
        RowVersion = rowVersion;
    }

    public Guid Id { get; }
    public Guid MergeGroupId { get; }
    public Guid PrimaryTableId { get; }
    public Guid MergedTableId { get; }
    public Guid? OriginalOrderId { get; }
    public Guid? OriginalBillId { get; }
    public TableMergeStatus Status { get; }
    public string Reason { get; }
    public Guid MergedBy { get; }
    public DateTimeOffset MergedAt { get; }
    public DateTimeOffset? UnmergedAt { get; }
    public Guid? UnmergedBy { get; }
    public string? UnmergeReason { get; }
    public long RowVersion { get; }

    public bool IsActive => Status == TableMergeStatus.Active;
}
