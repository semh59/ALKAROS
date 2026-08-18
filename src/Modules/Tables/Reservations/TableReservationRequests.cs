namespace ALKAROS.Tables.Reservations;

/// <summary>
/// Command request to create a new table reservation and project table status to 'Reserved' (V1-TBL-004).
/// </summary>
public sealed record CreateReservationRequest(
    Guid TableId,
    long ExpectedTableRowVersion,
    Guid? OrderId,
    Guid? ActorId,
    TableReservationActorType ActorType,
    string Reason,
    int PartySize = 1,
    DateTimeOffset? ReservedAt = null,
    DateTimeOffset? ExpiresAt = null)
{
    public void Validate()
    {
        if (TableId == Guid.Empty)
            throw new ArgumentException("Table ID cannot be empty.", nameof(TableId));
        if (ExpectedTableRowVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(ExpectedTableRowVersion), "Expected table row version must be positive.");
        if (string.IsNullOrWhiteSpace(Reason))
            throw new ArgumentException("Reservation reason cannot be empty.", nameof(Reason));
        if (PartySize <= 0)
            throw new ArgumentOutOfRangeException(nameof(PartySize), "Party size must be positive.");
        if (ExpiresAt.HasValue && ReservedAt.HasValue && ExpiresAt.Value <= ReservedAt.Value)
            throw new ArgumentException("ExpiresAt must be after ReservedAt.", nameof(ExpiresAt));
    }
}

/// <summary>
/// Command request to claim / seat a reservation and project table status to 'Occupied' (V1-TBL-004).
/// </summary>
public sealed record ClaimReservationRequest(
    Guid ReservationId,
    long ExpectedReservationRowVersion,
    long ExpectedTableRowVersion,
    Guid? OrderId,
    Guid? ClaimedBy,
    DateTimeOffset? ClaimedAt = null)
{
    public void Validate()
    {
        if (ReservationId == Guid.Empty)
            throw new ArgumentException("Reservation ID cannot be empty.", nameof(ReservationId));
        if (ExpectedReservationRowVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(ExpectedReservationRowVersion), "Expected reservation row version must be positive.");
        if (ExpectedTableRowVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(ExpectedTableRowVersion), "Expected table row version must be positive.");
    }
}

/// <summary>
/// Command request to cancel a reservation and project table status back to 'Available' (V1-TBL-004).
/// </summary>
public sealed record CancelReservationRequest(
    Guid ReservationId,
    long ExpectedReservationRowVersion,
    long ExpectedTableRowVersion,
    Guid? CancelledBy,
    string Reason,
    DateTimeOffset? CancelledAt = null)
{
    public void Validate()
    {
        if (ReservationId == Guid.Empty)
            throw new ArgumentException("Reservation ID cannot be empty.", nameof(ReservationId));
        if (ExpectedReservationRowVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(ExpectedReservationRowVersion), "Expected reservation row version must be positive.");
        if (ExpectedTableRowVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(ExpectedTableRowVersion), "Expected table row version must be positive.");
        if (string.IsNullOrWhiteSpace(Reason))
            throw new ArgumentException("Cancellation reason cannot be empty.", nameof(Reason));
    }
}

/// <summary>
/// Command request to expire a reservation and project table status back to 'Available' (V1-TBL-004).
/// </summary>
public sealed record ExpireReservationRequest(
    Guid ReservationId,
    long ExpectedReservationRowVersion,
    long ExpectedTableRowVersion,
    Guid? ExpiredBy,
    string Reason = "Reservation expired",
    DateTimeOffset? ExpiredAt = null)
{
    public void Validate()
    {
        if (ReservationId == Guid.Empty)
            throw new ArgumentException("Reservation ID cannot be empty.", nameof(ReservationId));
        if (ExpectedReservationRowVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(ExpectedReservationRowVersion), "Expected reservation row version must be positive.");
        if (ExpectedTableRowVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(ExpectedTableRowVersion), "Expected table row version must be positive.");
        if (string.IsNullOrWhiteSpace(Reason))
            throw new ArgumentException("Expiry reason cannot be empty.", nameof(Reason));
    }
}
