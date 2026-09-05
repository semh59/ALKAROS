using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.PortionReservations.Lifecycle;

namespace ALKAROS.Inventory.ReservationBalanceProjection;

public interface IReservationBalanceProjector
{
    Task<ApplyReservationResult> ApplyReservationCreatedAsync(PortionReservation reservation, CancellationToken ct = default);
    Task<ApplyReservationResult> ApplyReservationTransitionAsync(PortionReservation reservation, PortionReservationStatus previousStatus, CancellationToken ct = default);
    Task<ReservationBalanceRebuildReport> RebuildReservationBalancesAsync(CancellationToken ct = default);
    Task<ReservationBalanceDriftReport> DetectDriftAsync(CancellationToken ct = default);
}
