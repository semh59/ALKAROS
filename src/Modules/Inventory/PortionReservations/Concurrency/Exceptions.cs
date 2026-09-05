namespace ALKAROS.Inventory.PortionReservations.Concurrency;

public class PortionReservationArbitrationException : Exception
{
    public PortionReservationArbitrationException(string message) : base(message) { }
    public PortionReservationArbitrationException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class InsufficientPortionStockException : PortionReservationArbitrationException
{
    public InsufficientPortionStockException(Guid stockItemId, Guid stockLocationId, decimal requested, decimal available)
        : base($"Insufficient stock for item '{stockItemId}' at location '{stockLocationId}'. Requested: {requested}, Available: {available}.") { }
}

public sealed class InvalidArbitrationCommandException : PortionReservationArbitrationException
{
    public InvalidArbitrationCommandException(string message) : base(message) { }
}
