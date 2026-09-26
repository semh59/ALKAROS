using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.PortionReservations.Lifecycle;

namespace ALKAROS.Inventory.ReservationBalanceProjection;

public interface IReservationBalanceProjector
{
    Task<ApplyReservationResult> ApplyReservationCreatedAsync(PortionReservation reservation, CancellationToken ct = default);
    Task<ApplyReservationResult> ApplyReservationTransitionAsync(PortionReservation reservation, PortionReservationStatus previousStatus, CancellationToken ct = default);

    /// <summary>
    /// V1-RMD-310: applies a terminal reservation's projection inside the caller's transaction, once per
    /// reservation (the applied-event record makes a repeat a no-op). Unlike the overload above it does not
    /// need the previous status, so a repeat after an interrupted cancellation still completes the projection.
    /// </summary>
    Task<ApplyReservationResult> ApplyTerminalInTransactionAsync(
        PortionReservation reservation, Npgsql.NpgsqlConnection connection, Npgsql.NpgsqlTransaction transaction,
        CancellationToken ct = default);
    Task<ReservationBalanceRebuildReport> RebuildReservationBalancesAsync(CancellationToken ct = default);
    Task<ReservationBalanceDriftReport> DetectDriftAsync(CancellationToken ct = default);
}
