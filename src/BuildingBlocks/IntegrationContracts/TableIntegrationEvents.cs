namespace ALKAROS.IntegrationContracts;

/// <summary>
/// Stable wire names for the integration events carried through the outbox
/// (V0-ARC-001 §2: publisher owns the contract). The name is versioned so a
/// breaking payload change ships as a new type alongside the old one.
/// </summary>
public static class IntegrationEventTypes
{
    public const string TableMerged = "tables.table-merged.v1";
    public const string TableTransferred = "tables.table-transferred.v1";
    public const string TableUnmerged = "tables.table-unmerged.v1";
}

/// <summary>
/// Table Management merged one participant table into a primary table. Order
/// and Bill consume this to move their own still-active rows from the
/// participant to the primary table (V0-ARC-001 row 3: Table → Order, Bill via
/// integration event; the consumer mutates only its own state).
/// </summary>
public sealed record TableMerged(
    Guid MergeGroupId,
    Guid ParticipantTableId,
    Guid PrimaryTableId,
    DateTimeOffset OccurredAt);

/// <summary>
/// Table Management transferred a table's active work to another table. Order
/// and Bill move their active rows from source to target.
/// </summary>
public sealed record TableTransferred(
    Guid TransferId,
    Guid SourceTableId,
    Guid TargetTableId,
    DateTimeOffset OccurredAt);

/// <summary>
/// Table Management unmerged a participant table out of a merge group. Order
/// and Bill move the identified row back from the primary table to the table
/// it came from; the move is a no-op when the row is no longer where the
/// unmerge expects it (at-least-once, idempotent).
/// </summary>
public sealed record TableUnmerged(
    Guid MergeGroupId,
    Guid MergedTableId,
    Guid PrimaryTableId,
    Guid? OriginalOrderId,
    Guid? OriginalBillId,
    DateTimeOffset OccurredAt);
