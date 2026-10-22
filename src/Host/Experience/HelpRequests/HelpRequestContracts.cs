namespace ALKAROS.Host.Experience.HelpRequests;

/// <summary>
/// V1-WTR-014: what a waiter is calling for. Fixed catalog for the same
/// reason void/comp reason codes are (PDF:I.24 precedent) — free text alone
/// makes a "why" column useless for anything but reading one row at a time.
/// </summary>
public static class HelpRequestTypeCatalog
{
    public const string Spill = "Spill";
    public const string Complaint = "Complaint";
    public const string Approval = "Approval";
    public const string Other = "Other";

    private static readonly HashSet<string> ValidTypes = new(StringComparer.Ordinal)
    {
        Spill, Complaint, Approval, Other,
    };

    public static bool IsValid(string? type) => type is not null && ValidTypes.Contains(type);
}

/// <summary>Request body for raising a help request at a table.</summary>
public sealed record HelpRequestV1(Guid TableId, string RequestType);

/// <summary>
/// Broadcast to every connected manager/supervisor session
/// (<see cref="HelpRequestHub"/>) the moment a help request is raised.
/// </summary>
public sealed record HelpRequestedV1(
    Guid TableId,
    string TableNumber,
    string RequestType,
    string RequestedByDisplayName,
    DateTimeOffset CreatedAt,
    Guid HelpRequestId);

/// <summary>
/// The waiter's answer to a raised request. <c>ConnectedRecipients</c> is how many manager sessions were
/// connected when it was broadcast: 0 means nobody was told live, the request is only stored and waits in
/// the open list.
/// </summary>
public sealed record HelpRequestRaisedV1(
    Guid TableId,
    string TableNumber,
    string RequestType,
    string RequestedByDisplayName,
    DateTimeOffset CreatedAt,
    Guid HelpRequestId,
    int ConnectedRecipients);

/// <summary>A request nobody has acknowledged yet, as listed to managers.</summary>
public sealed record OpenHelpRequestV1(
    Guid HelpRequestId,
    Guid TableId,
    string TableNumber,
    string RequestType,
    string RequestedByDisplayName,
    DateTimeOffset CreatedAt);

/// <summary>Sent to the requesting waiter's own devices once a manager has taken the call.</summary>
public sealed record HelpRequestSeenV1(string TableNumber, string SeenByDisplayName);

/// <summary>Raised when the same table already has an unanswered request inside the cooldown window.</summary>
public sealed class HelpRequestCooldownActiveException : Exception
{
    public HelpRequestCooldownActiveException(TimeSpan remaining)
        : base($"An unanswered help request for this table was already raised; {remaining.TotalSeconds:F0}s of cooldown remain.")
    {
        Remaining = remaining;
    }

    public TimeSpan Remaining { get; }
}
