namespace ALKAROS.Tables.Reservations;

/// <summary>
/// Base exception for table reservation domain errors.
/// </summary>
public abstract class TableReservationException : Exception
{
    protected TableReservationException(string message) : base(message) { }
    protected TableReservationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when a table is not found.
/// </summary>
public sealed class TableNotFoundException : TableReservationException
{
    public TableNotFoundException(Guid tableId, string message) : base(message)
    {
        TableId = tableId;
    }

    public Guid TableId { get; }
}

/// <summary>
/// Thrown when a table cannot be reserved because its state is not Available (e.g. Occupied, Reserved, Cleaning, OutOfService, or Inactive).
/// </summary>
public sealed class TableNotAvailableForReservationException : TableReservationException
{
    public TableNotAvailableForReservationException(Guid tableId, string actualState, string reason)
        : base($"Table {tableId} is in '{actualState}' state and cannot be reserved: {reason}")
    {
        TableId = tableId;
        ActualState = actualState;
        Reason = reason;
    }

    public Guid TableId { get; }
    public string ActualState { get; }
    public string Reason { get; }
}

/// <summary>
/// Thrown when a reservation is not found.
/// </summary>
public sealed class ReservationNotFoundException : TableReservationException
{
    public ReservationNotFoundException(Guid reservationId)
        : base($"Reservation '{reservationId}' was not found.")
    {
        ReservationId = reservationId;
    }

    public Guid ReservationId { get; }
}

/// <summary>
/// Thrown when a reservation action is attempted in an invalid state (e.g. attempting to claim or cancel an already cancelled or claimed reservation).
/// </summary>
public sealed class InvalidReservationStateException : TableReservationException
{
    public InvalidReservationStateException(Guid reservationId, TableReservationStatus actualStatus, string attemptedAction)
        : base($"Cannot perform '{attemptedAction}' on reservation {reservationId} because it is in '{actualStatus}' status.")
    {
        ReservationId = reservationId;
        ActualStatus = actualStatus;
        AttemptedAction = attemptedAction;
    }

    public Guid ReservationId { get; }
    public TableReservationStatus ActualStatus { get; }
    public string AttemptedAction { get; }
}

/// <summary>
/// Thrown when an optimistic concurrency check fails on table or reservation row versions.
/// </summary>
public sealed class TableReservationConcurrencyException : TableReservationException
{
    public TableReservationConcurrencyException(Guid entityId, string entityType, long expectedVersion, long actualVersion)
        : base($"Concurrency conflict on {entityType} {entityId}: expected row version {expectedVersion}, actual {actualVersion}.")
    {
        EntityId = entityId;
        EntityType = entityType;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }

    public Guid EntityId { get; }
    public string EntityType { get; }
    public long ExpectedVersion { get; }
    public long ActualVersion { get; }
}
