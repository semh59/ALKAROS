namespace ALKAROS.Payments.ManualResolution;

public enum ManualCardConfirmationStatus
{
    Pending,
    Approved,
    Rejected,
}

/// <summary>
/// V1-RMD-283: a claim that an unconfirmed card payment WAS charged, backed by the receipt (slip) number.
/// It moves money only when a second, different authorized person approves it.
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
    long RowVersion);
