namespace ALKAROS.Inventory.PortionReservations.Lifecycle;

public interface IPortionReservationLifecycleService
{
    Task<ReservationTransitionResult> CreateReservationAsync(CreateReservationCommand command, CancellationToken cancellationToken = default);
    Task<ReservationTransitionResult> ReleaseReservationAsync(TransitionReservationCommand command, CancellationToken cancellationToken = default);
    Task<ReservationTransitionResult> ConsumeReservationAsync(TransitionReservationCommand command, CancellationToken cancellationToken = default);
    Task<ReservationTransitionResult> WasteReservationAsync(TransitionReservationCommand command, CancellationToken cancellationToken = default);
    Task<PortionReservation?> GetReservationByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
