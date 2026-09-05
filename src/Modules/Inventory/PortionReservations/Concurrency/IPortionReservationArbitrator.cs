namespace ALKAROS.Inventory.PortionReservations.Concurrency;

public interface IPortionReservationArbitrator
{
    Task<ReservationArbitrationResult> ArbitrateReservationAsync(ArbitrateReservationCommand command, CancellationToken ct = default);
}
