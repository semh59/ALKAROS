namespace ALKAROS.Tables.Reservations;

/// <summary>
/// Result of a successful table reservation creation (V1-TBL-004).
/// </summary>
public sealed record TableReservationResult(
    Guid ReservationId,
    Guid TableId,
    long NewTableRowVersion,
    TableReservationStatus Status,
    DateTimeOffset ReservedAt,
    DateTimeOffset? ExpiresAt);

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
