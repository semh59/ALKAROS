namespace ALKAROS.Cash.TransactionLedger;

/// <summary>States of a <see cref="CashMovementRequest"/>; stored as these exact strings.</summary>
public static class CashMovementRequestStatus
{
    public const string Requested = "Requested";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
}

/// <summary>
/// A cash-out above the approval threshold. Nothing reaches the ledger until a manager approves it; the approval
/// writes the CashOut row and records who approved and whether that was the requester themselves.
/// </summary>
public sealed record CashMovementRequest(
    Guid Id,
    Guid CashSessionId,
    decimal Amount,
    string Reason,
    string IdempotencyKey,
    string Status,
    Guid RequestedBy,
    DateTimeOffset RequestedAt,
    Guid? DecidedBy,
    DateTimeOffset? DecidedAt,
    string? DecisionNote,
    bool SelfApproved,
    Guid? CashTransactionId);

/// <summary>An open request as the drawer screen lists it.</summary>
public sealed record PendingCashMovementRequest(
    Guid Id, decimal Amount, string Reason, Guid RequestedBy, string? RequestedByName, DateTimeOffset RequestedAt);

/// <summary>A manual cash-in/cash-out row as the drawer screen lists it, with who recorded and who approved it.</summary>
public sealed record CashMovementEntry(
    Guid Id,
    string Type,
    string Direction,
    decimal Amount,
    string? Notes,
    Guid? RecordedBy,
    string? RecordedByName,
    DateTimeOffset OccurredAt,
    Guid? ApprovedBy,
    string? ApprovedByName,
    bool SelfApproved);
