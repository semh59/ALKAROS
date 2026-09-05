namespace ALKAROS.Inventory.PortionReservations.Concurrency;

public interface IPortionReservationArbitratorRepository
{
    Task<ReservationArbitrationResult> TryReserveAtomicAsync(ArbitrateReservationCommand command, CancellationToken ct = default);
}
