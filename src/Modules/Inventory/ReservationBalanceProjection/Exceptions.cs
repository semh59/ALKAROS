namespace ALKAROS.Inventory.ReservationBalanceProjection;

public class ReservationBalanceException : Exception
{
    public ReservationBalanceException(string message) : base(message) { }
    public ReservationBalanceException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class ReservationBalanceNotFoundException : ReservationBalanceException
{
    public ReservationBalanceNotFoundException(Guid stockItemId, Guid stockLocationId)
        : base($"Stock balance not found for stock item '{stockItemId}' at location '{stockLocationId}'.") { }
}

public sealed class InvalidReservationBalanceDeltaException : ReservationBalanceException
{
    public InvalidReservationBalanceDeltaException(string message) : base(message) { }
}
