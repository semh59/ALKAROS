namespace ALKAROS.CustomerData.AnonymizationState;

/// <summary>
/// A single anonymization request against a `CustomerProfile`
/// (`ALKAROS.CustomerData.Profiles`, V14-CST-001). Invariants mirror
/// V0-DOM-001's "actor and reason recorded where required": a
/// <see cref="BlockedReason"/> exists if and only if the request is
/// currently <see cref="AnonymizationRequestStatus.RetentionBlocked"/>, and
/// <see cref="AnonymizedAt"/> exists if and only if it is
/// <see cref="AnonymizationRequestStatus.Anonymized"/>.
/// </summary>
public sealed record CustomerAnonymizationRequest
{
    public Guid Id { get; }
    public Guid CustomerId { get; }
    public AnonymizationRequestStatus Status { get; }
    public DateTimeOffset RequestedAt { get; }
    public string? RequestedBy { get; }
    public string? BlockedReason { get; }
    public DateTimeOffset? AnonymizedAt { get; }
    public int RowVersion { get; }

    public CustomerAnonymizationRequest(
        Guid id,
        Guid customerId,
        AnonymizationRequestStatus status,
        DateTimeOffset requestedAt,
        string? requestedBy,
        string? blockedReason,
        DateTimeOffset? anonymizedAt,
        int rowVersion)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id must not be empty.", nameof(id));
        if (customerId == Guid.Empty)
            throw new ArgumentException("CustomerId must not be empty.", nameof(customerId));
        if (rowVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(rowVersion), rowVersion, "Row version must be a positive integer.");
        if ((status == AnonymizationRequestStatus.RetentionBlocked) != (blockedReason is not null))
            throw new ArgumentException("BlockedReason must be set if and only if Status is RetentionBlocked.");
        if ((status == AnonymizationRequestStatus.Anonymized) != (anonymizedAt is not null))
            throw new ArgumentException("AnonymizedAt must be set if and only if Status is Anonymized.");

        Id = id;
        CustomerId = customerId;
        Status = status;
        RequestedAt = requestedAt;
        RequestedBy = requestedBy;
        BlockedReason = blockedReason;
        AnonymizedAt = anonymizedAt;
        RowVersion = rowVersion;
    }
}
