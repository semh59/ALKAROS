namespace ALKAROS.Tables.TableMerge;

/// <summary>
/// Result of an executed table merge operation (V1-TBL-003).
/// </summary>
public sealed record TableMergeResult(
    Guid MergeGroupId,
    IReadOnlyList<Guid> TableMergeIds,
    Guid PrimaryTableId,
    long NewPrimaryRowVersion,
    IReadOnlyList<Guid> MergedTableIds,
    IReadOnlyDictionary<Guid, long> NewParticipantRowVersions,
    IReadOnlyList<Guid> ConsolidatedOrderIds,
    IReadOnlyList<Guid> ConsolidatedBillIds,
    DateTimeOffset MergedAt);

/// <summary>
/// Result of an executed table unmerge (undo) operation (V1-TBL-003).
/// </summary>
public sealed record TableUnmergeResult(
    Guid MergeGroupId,
    Guid PrimaryTableId,
    long NewPrimaryRowVersion,
    IReadOnlyDictionary<Guid, long> NewParticipantRowVersions,
    IReadOnlyList<Guid> RestoredOrderIds,
    IReadOnlyList<Guid> RestoredBillIds,
    DateTimeOffset UnmergedAt);
