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
    DateTimeOffset CreatedAt);

/// <summary>Raised when the same table's cooldown window has not elapsed yet.</summary>
public sealed class HelpRequestCooldownActiveException : Exception
{
    public HelpRequestCooldownActiveException(TimeSpan remaining)
        : base($"A help request for this table was already raised {remaining.TotalSeconds:F0}s ago.")
    {
        Remaining = remaining;
    }

    public TimeSpan Remaining { get; }
}
