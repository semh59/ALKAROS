namespace ALKAROS.Inventory.PortionReservations.Lifecycle;

public class PortionReservationException : Exception
{
    public PortionReservationException(string message) : base(message) { }
    public PortionReservationException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class PortionReservationNotFoundException : PortionReservationException
{
    public PortionReservationNotFoundException(Guid id)
        : base($"Portion reservation '{id}' was not found.") { }
}

public sealed class InvalidPortionReservationTransitionException : PortionReservationException
{
    public InvalidPortionReservationTransitionException(string message) : base(message) { }
}

public sealed class PortionReservationConflictException : PortionReservationException
{
    public PortionReservationConflictException(string message) : base(message) { }
}

public sealed class InvalidPortionReservationQuantityException : PortionReservationException
{
    public InvalidPortionReservationQuantityException(string message) : base(message) { }
}

public sealed class UnauthorizedReservationActorException : PortionReservationException
{
    public UnauthorizedReservationActorException(string message) : base(message) { }
}
