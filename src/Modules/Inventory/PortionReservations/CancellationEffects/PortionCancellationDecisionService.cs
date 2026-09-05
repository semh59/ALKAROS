using ALKAROS.Inventory.PortionReservations.Lifecycle;
using ALKAROS.Inventory.ReservationBalanceProjection;
using ALKAROS.Inventory.WasteRecording;

namespace ALKAROS.Inventory.PortionReservations.CancellationEffects;

public sealed class PortionCancellationDecisionService : IPortionCancellationDecisionService
{
    private readonly IPortionReservationRepository _reservationRepo;
    private readonly IPortionReservationLifecycleService _lifecycleService;
    private readonly IReservationBalanceProjector _balanceProjector;
    private readonly IWasteRecordingService _wasteService;
    private readonly IKitchenItemStateProvider _kitchenProvider;

    public PortionCancellationDecisionService(
        IPortionReservationRepository reservationRepo,
        IPortionReservationLifecycleService lifecycleService,
        IReservationBalanceProjector balanceProjector,
        IWasteRecordingService wasteService,
        IKitchenItemStateProvider kitchenProvider)
    {
        _reservationRepo = reservationRepo ?? throw new ArgumentNullException(nameof(reservationRepo));
        _lifecycleService = lifecycleService ?? throw new ArgumentNullException(nameof(lifecycleService));
        _balanceProjector = balanceProjector ?? throw new ArgumentNullException(nameof(balanceProjector));
        _wasteService = wasteService ?? throw new ArgumentNullException(nameof(wasteService));
        _kitchenProvider = kitchenProvider ?? throw new ArgumentNullException(nameof(kitchenProvider));
    }

    public async Task<CancellationDecisionResult> ProcessCancellationAsync(
        ProcessCancellationCommand command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.ReservationId == Guid.Empty)
            throw new InvalidCancellationCommandException("ReservationId cannot be empty.");
        if (command.OrderItemId == Guid.Empty)
            throw new InvalidCancellationCommandException("OrderItemId cannot be empty.");
        if (command.ActorId == Guid.Empty)
            throw new InvalidCancellationCommandException("ActorId cannot be empty.");

        var reservation = await _reservationRepo.GetByIdAsync(command.ReservationId, ct);
        if (reservation == null)
            throw new PortionReservationNotFoundException(command.ReservationId);

        // Idempotency check: if reservation already in terminal state
        if (reservation.Status == PortionReservationStatus.Released)
        {
            return CancellationDecisionResult.ReplayRelease(reservation, "Already released.");
        }
        if (reservation.Status == PortionReservationStatus.Waste)
        {
            return CancellationDecisionResult.ReplayWaste(reservation, "Already recorded as waste.");
        }
        if (reservation.Status == PortionReservationStatus.Consumed)
        {
            throw new InvalidCancellationCommandException(
                $"Cannot cancel portion reservation '{command.ReservationId}' because it is already Consumed.");
        }

        // Status lookup from kitchen
        var kitchenStatus = await _kitchenProvider.GetItemPreparationStatusAsync(command.OrderItemId, ct);

        if (kitchenStatus == KitchenItemPreparationStatus.NotStarted)
        {
            // 1. Pre-kitchen cancellation -> RELEASE
            var reason = command.CancellationReason ?? "Cancelled before kitchen preparation started";
            var transitionCmd = new TransitionReservationCommand(
                reservation.Id, PortionReservationStatus.Released, command.ActorId, reason, command.IdempotencyKey);

            var transitionResult = await _lifecycleService.ReleaseReservationAsync(transitionCmd, ct);

            // Apply transition to balance projection (restores available quantity)
            await _balanceProjector.ApplyReservationTransitionAsync(
                transitionResult.Reservation, PortionReservationStatus.Reserved, ct);

            return CancellationDecisionResult.SuccessRelease(transitionResult.Reservation, reason);
        }
        else
        {
            // 2. Post-preparation cancellation -> WASTE
            var reason = command.CancellationReason ?? $"Cancelled after kitchen preparation started ({kitchenStatus})";
            var transitionCmd = new TransitionReservationCommand(
                reservation.Id, PortionReservationStatus.Waste, command.ActorId, reason, command.IdempotencyKey);

            var transitionResult = await _lifecycleService.WasteReservationAsync(transitionCmd, ct);

            // Apply transition to balance projection (decrements reserved quantity)
            await _balanceProjector.ApplyReservationTransitionAsync(
                transitionResult.Reservation, PortionReservationStatus.Reserved, ct);

            // Record waste via V11-INV-006 contract (StockMovement out, on-hand decremented)
            var wasteReq = new RecordWasteRequest(
                StockItemId: reservation.StockItemId,
                StockLocationId: reservation.StockLocationId,
                Quantity: reservation.Quantity,
                UnitCode: reservation.UnitCode,
                Reason: reason,
                RecordedBy: command.ActorId,
                WasteSource: WasteSources.PortionReservation,
                SourceReferenceId: reservation.Id,
                IdempotencyKey: !string.IsNullOrWhiteSpace(command.IdempotencyKey) ? "waste-" + command.IdempotencyKey.Trim() : null);

            var wasteResult = await _wasteService.RecordWasteAsync(wasteReq, ct);

            return CancellationDecisionResult.SuccessWaste(transitionResult.Reservation, wasteResult.Record, reason);
        }
    }
}
