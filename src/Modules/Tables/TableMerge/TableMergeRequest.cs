namespace ALKAROS.Tables.TableMerge;

/// <summary>
/// Command request to merge multiple tables into a primary table (V1-TBL-003).
/// </summary>
public sealed record TableMergeRequest(
    Guid PrimaryTableId,
    long ExpectedPrimaryRowVersion,
    IReadOnlyList<TableMergeParticipant> Participants,
    string Reason,
    Guid MergedBy,
    DateTimeOffset? MergedAt = null)
{
    public void Validate()
    {
        if (PrimaryTableId == Guid.Empty)
            throw new ArgumentException("Primary table ID cannot be empty.", nameof(PrimaryTableId));
        if (ExpectedPrimaryRowVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(ExpectedPrimaryRowVersion), "Expected primary row version must be positive.");
        if (Participants is null || Participants.Count == 0)
            throw new ArgumentException("Merge request must contain at least one participant table.", nameof(Participants));

        var seenIds = new HashSet<Guid> { PrimaryTableId };
        foreach (var participant in Participants)
        {
            if (participant.TableId == Guid.Empty)
                throw new ArgumentException("Participant table ID cannot be empty.", nameof(Participants));
            if (participant.ExpectedRowVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(Participants), "Participant expected row version must be positive.");
            if (participant.TableId == PrimaryTableId)
                throw new SameTableMergeException(PrimaryTableId);
            if (!seenIds.Add(participant.TableId))
                throw new DuplicateMergeParticipantException(participant.TableId);
        }

        if (string.IsNullOrWhiteSpace(Reason))
            throw new ArgumentException("Merge reason cannot be empty.", nameof(Reason));
        if (MergedBy == Guid.Empty)
            throw new ArgumentException("MergedBy user ID cannot be empty.", nameof(MergedBy));
    }
}

/// <summary>
/// Command request to undo / reverse an active table merge (V1-TBL-003).
/// </summary>
public sealed record TableUnmergeRequest(
    Guid MergeGroupId,
    long ExpectedPrimaryRowVersion,
    IReadOnlyList<TableMergeParticipant> ExpectedParticipantVersions,
    string Reason,
    Guid UnmergedBy,
    DateTimeOffset? UnmergedAt = null)
{
    public void Validate()
    {
        if (MergeGroupId == Guid.Empty)
            throw new ArgumentException("Merge group ID cannot be empty.", nameof(MergeGroupId));
        if (ExpectedPrimaryRowVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(ExpectedPrimaryRowVersion), "Expected primary row version must be positive.");
        if (ExpectedParticipantVersions is null || ExpectedParticipantVersions.Count == 0)
            throw new ArgumentException("Expected participant versions must contain at least one participant.", nameof(ExpectedParticipantVersions));

        foreach (var participant in ExpectedParticipantVersions)
        {
            if (participant.TableId == Guid.Empty)
                throw new ArgumentException("Participant table ID cannot be empty.", nameof(ExpectedParticipantVersions));
            if (participant.ExpectedRowVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(ExpectedParticipantVersions), "Participant expected row version must be positive.");
        }

        if (string.IsNullOrWhiteSpace(Reason))
            throw new ArgumentException("Unmerge reason cannot be empty.", nameof(Reason));
        if (UnmergedBy == Guid.Empty)
            throw new ArgumentException("UnmergedBy user ID cannot be empty.", nameof(UnmergedBy));
    }
}
