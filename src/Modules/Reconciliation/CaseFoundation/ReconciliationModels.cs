namespace ALKAROS.Reconciliation.CaseFoundation;

/// <summary>
/// Domain record for a reconciliation discrepancy case (V1-REC-001, PDF:III.23).
/// </summary>
public sealed record ReconciliationCaseRecord(
    Guid CaseId,
    string DeduplicationKey,
    CaseType CaseType,
    string SourceARef,
    string SourceBRef,
    decimal DiscrepancyAmount,
    CaseSeverity Severity,
    CaseStatus Status,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ResolvedAt,
    int RowVersion,
    string? DetailsJson);

/// <summary>
/// Immutable audit action record in the reconciliation case trail (V1-REC-001).
/// </summary>
public sealed record CaseActionRecord(
    Guid ActionId,
    Guid CaseId,
    ActionType ActionType,
    Guid PerformedBy,
    DateTimeOffset PerformedAt,
    string? DetailsJson);

/// <summary>
/// Command request to create or deduplicate a reconciliation case (V1-REC-001).
/// </summary>
public sealed record CreateCaseRequest(
    string DeduplicationKey,
    CaseType CaseType,
    string SourceARef,
    string SourceBRef,
    decimal DiscrepancyAmount,
    CaseSeverity Severity,
    Guid PerformedBy,
    string? DetailsJson = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DeduplicationKey))
            throw new ArgumentException("Deduplication key cannot be null or whitespace.", nameof(DeduplicationKey));

        if (string.IsNullOrWhiteSpace(SourceARef))
            throw new ArgumentException("Source A reference cannot be null or whitespace.", nameof(SourceARef));

        if (string.IsNullOrWhiteSpace(SourceBRef))
            throw new ArgumentException("Source B reference cannot be null or whitespace.", nameof(SourceBRef));

        if (PerformedBy == Guid.Empty)
            throw new ArgumentException("PerformedBy user ID cannot be empty.", nameof(PerformedBy));
    }
}

/// <summary>
/// Request to transition case status (V1-REC-001).
/// </summary>
public sealed record TransitionCaseStatusRequest(
    Guid CaseId,
    CaseStatus NewStatus,
    int ExpectedVersion,
    Guid PerformedBy,
    string? ReasonOrNote = null);

/// <summary>
/// Request to append an audit note to a case (V1-REC-001).
/// </summary>
public sealed record AddCaseNoteRequest(
    Guid CaseId,
    string Note,
    Guid PerformedBy);
