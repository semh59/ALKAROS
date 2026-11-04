namespace ALKAROS.Payments.ManualResolution;

public enum ManualCardConfirmationStatus
{
    Pending,
    Approved,
    Rejected,
}

/// <summary>
/// A claim that an unconfirmed card payment WAS charged, backed by the receipt (slip) number.
/// It moves money only when a second, different authorized person approves it; the person who claimed it approves it
/// themselves only when the business turned the shared self-approval setting on, and the row then says so.
/// </summary>
public sealed record ManualCardConfirmation(
    Guid Id,
    Guid PaymentId,
    Guid BillId,
    string SlipNumber,
    decimal Amount,
    ManualCardConfirmationStatus Status,
    Guid RequestedBy,
    DateTimeOffset RequestedAt,
    string? RequestNote,
    Guid? DecidedBy,
    DateTimeOffset? DecidedAt,
    string? DecisionNote,
    long RowVersion,
    bool SelfApproved = false);
