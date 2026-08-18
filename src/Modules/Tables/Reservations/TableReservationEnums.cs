namespace ALKAROS.Tables.Reservations;

/// <summary>
/// Status lifecycle of a table reservation (V1-TBL-004, PDF:II.5.15, V0-DOM-005).
/// </summary>
public enum TableReservationStatus
{
    Active = 1,
    Claimed = 2,
    Cancelled = 3,
    Expired = 4
}

/// <summary>
/// Actor type responsible for reservation creation or release (V1-TBL-004).
/// </summary>
public enum TableReservationActorType
{
    User = 1,
    Device = 2,
    Customer = 3,
    System = 4
}
