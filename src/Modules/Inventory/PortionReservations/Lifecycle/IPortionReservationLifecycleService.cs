using Npgsql;

namespace ALKAROS.Inventory.PortionReservations.Lifecycle;

public interface IPortionReservationLifecycleService
{
    Task<ReservationTransitionResult> CreateReservationAsync(CreateReservationCommand command, CancellationToken cancellationToken = default);
    Task<ReservationTransitionResult> ReleaseReservationAsync(TransitionReservationCommand command, CancellationToken cancellationToken = default);
    Task<ReservationTransitionResult> ConsumeReservationAsync(TransitionReservationCommand command, CancellationToken cancellationToken = default);
    Task<ReservationTransitionResult> WasteReservationAsync(TransitionReservationCommand command, CancellationToken cancellationToken = default);
    Task<PortionReservation?> GetReservationByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// V1-RMD-310: a Release or Waste transition inside the caller's transaction, reading the reservation locked.
    /// With a null <paramref name="transaction"/> the transition commits on its own (the non-transactional
    /// cancellation overload and in-memory fakes).
    /// </summary>
    Task<ReservationTransitionResult> TransitionInTransactionAsync(
        TransitionReservationCommand command, NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);
}
