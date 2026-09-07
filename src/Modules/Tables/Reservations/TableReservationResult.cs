namespace ALKAROS.Tables.Reservations;

/// <summary>
/// Result of a successful table reservation creation (V1-TBL-004).
/// </summary>
/// <param name="ReservationRowVersion">
/// The freshly-inserted reservation row's own version — found missing by an
/// independent audit (2026-09-07): without it, no client could ever supply
/// <see cref="ClaimReservationRequest.ExpectedReservationRowVersion"/> /
/// <see cref="CancelReservationRequest.ExpectedReservationRowVersion"/> /
/// <see cref="ExpireReservationRequest.ExpectedReservationRowVersion"/>,
/// making Claim/Cancel/Expire structurally unreachable.
/// </param>
public sealed record TableReservationResult(
    Guid ReservationId,
    Guid TableId,
    long ReservationRowVersion,
    long NewTableRowVersion,
    TableReservationStatus Status,
    DateTimeOffset ReservedAt,
    DateTimeOffset? ExpiresAt);

/// <summary>
/// Read model for a single reservation (V1-RMD-117) — the GET counterpart to
/// <see cref="TableReservationResult"/>/<see cref="TableReservationReleaseResult"/>,
/// so a client can recover a reservation's current row version (e.g. after a
/// page reload) instead of only ever seeing it in the create/release response.
/// </summary>
public sealed record TableReservationDto(
    Guid ReservationId,
    Guid TableId,
    Guid? OrderId,
    Guid? ActorId,
    TableReservationActorType ActorType,
    TableReservationStatus Status,
    string Reason,
    int PartySize,
    DateTimeOffset ReservedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? ReleasedAt,
    Guid? ReleasedBy,
    string? ReleaseReason,
    long RowVersion)
{
    public static TableReservationDto From(TableReservationRecord record) => new(
        record.Id,
        record.TableId,
        record.OrderId,
        record.ActorId,
        record.ActorType,
        record.Status,
        record.Reason,
        record.PartySize,
        record.ReservedAt,
        record.ExpiresAt,
        record.ReleasedAt,
        record.ReleasedBy,
        record.ReleaseReason,
        record.RowVersion);
}

/// <summary>
/// Result of a table reservation release (Claimed, Cancelled, or Expired) (V1-TBL-004).
/// </summary>
public sealed record TableReservationReleaseResult(
    Guid ReservationId,
    Guid TableId,
    long NewReservationRowVersion,
    long NewTableRowVersion,
    TableReservationStatus PreviousStatus,
    TableReservationStatus NewStatus,
    string FinalTableStatus,
    DateTimeOffset ReleasedAt);
