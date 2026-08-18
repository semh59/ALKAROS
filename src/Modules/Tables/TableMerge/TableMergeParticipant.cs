namespace ALKAROS.Tables.TableMerge;

/// <summary>
/// Represents a participant table in a merge/unmerge request along with its expected row version.
/// </summary>
public sealed record TableMergeParticipant(
    Guid TableId,
    long ExpectedRowVersion);
