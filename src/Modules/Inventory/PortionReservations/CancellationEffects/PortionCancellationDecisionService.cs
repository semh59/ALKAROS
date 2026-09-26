using ALKAROS.Inventory.PortionReservations.Lifecycle;
using ALKAROS.Inventory.ReservationBalanceProjection;
using ALKAROS.Inventory.WasteRecording;
using Npgsql;

namespace ALKAROS.Inventory.PortionReservations.CancellationEffects;

/// <summary>
/// Decides whether a cancelled order item's portion reservation is released (the kitchen had not started) or
/// wasted (it had), and applies every effect of that decision in one transaction (V1-RMD-310). The reservation
/// row is read locked, so a concurrent cancellation of the same reservation waits and then sees the result. For
/// waste the stock movement and the on-hand decrement are written before the reserved quantity is
/// lowered, so available stock never rises on the way — the portion that is thrown away can never be sold.
/// A repeat after an interrupted earlier run (a reservation already Released or Waste) completes whatever that
/// run left undone: the projection and the waste movement are each recorded once, never twice.
/// <para>Only the overload that takes the caller's transaction is atomic; every production caller
/// (<c>ICrossChannelPortionArbiter.CompensateAsync</c>) uses it. The overload without one is kept for existing
/// callers and tests: each of its steps commits on its own, in the same safe order.</para>
/// </summary>
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

    public Task<CancellationDecisionResult> ProcessCancellationAsync(
        ProcessCancellationCommand command,
        CancellationToken ct = default)
    {
        Validate(command);
        return ProcessCoreAsync(command, null, null, ct);
    }

    public Task<CancellationDecisionResult> ProcessCancellationAsync(
        ProcessCancellationCommand command,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken ct = default)
    {
        Validate(command);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        return ProcessCoreAsync(command, connection, transaction, ct);
    }

    private static void Validate(ProcessCancellationCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.ReservationId == Guid.Empty)
            throw new InvalidCancellationCommandException("ReservationId cannot be empty.");
        if (command.OrderItemId == Guid.Empty)
            throw new InvalidCancellationCommandException("OrderItemId cannot be empty.");
        if (command.ActorId == Guid.Empty)
            throw new InvalidCancellationCommandException("ActorId cannot be empty.");
    }

    // connection/transaction are null for the overload without a caller transaction.
    private async Task<CancellationDecisionResult> ProcessCoreAsync(
        ProcessCancellationCommand command,
        NpgsqlConnection? connection,
        NpgsqlTransaction? transaction,
        CancellationToken ct)
    {
        var reservation = (transaction is null
                ? await _reservationRepo.GetByIdAsync(command.ReservationId, ct)
                : await _reservationRepo.GetByIdForUpdateAsync(command.ReservationId, connection!, transaction, ct))
            ?? throw new PortionReservationNotFoundException(command.ReservationId);

        if (reservation.Status == PortionReservationStatus.Consumed)
        {
            throw new InvalidCancellationCommandException(
                $"Cannot cancel portion reservation '{command.ReservationId}' because it is already Consumed.");
        }

        if (reservation.Status == PortionReservationStatus.Released)
        {
            await _balanceProjector.ApplyTerminalInTransactionAsync(reservation, connection!, transaction!, ct);
            return CancellationDecisionResult.ReplayRelease(reservation, "Already released.");
        }

        if (reservation.Status == PortionReservationStatus.Waste)
        {
            await RecordWasteOnceAsync(reservation, command, reservation.TransitionReason ?? "Recorded as waste", connection, transaction, ct);
            await _balanceProjector.ApplyTerminalInTransactionAsync(reservation, connection!, transaction!, ct);
            return CancellationDecisionResult.ReplayWaste(reservation, "Already recorded as waste.");
        }

        var kitchenStatus = transaction is null
            ? await _kitchenProvider.GetItemPreparationStatusAsync(command.OrderItemId, ct)
            : await _kitchenProvider.GetItemPreparationStatusAsync(command.OrderItemId, connection!, transaction, ct);

        if (kitchenStatus == KitchenItemPreparationStatus.NotStarted)
        {
            var reason = command.CancellationReason ?? "Cancelled before kitchen preparation started";
            var released = await _lifecycleService.TransitionInTransactionAsync(
                new TransitionReservationCommand(
                    reservation.Id, PortionReservationStatus.Released, command.ActorId, reason, command.IdempotencyKey),
                connection!, transaction!, ct);
            await _balanceProjector.ApplyTerminalInTransactionAsync(released.Reservation, connection!, transaction!, ct);
            return CancellationDecisionResult.SuccessRelease(released.Reservation, reason);
        }

        var wasteReason = command.CancellationReason ?? $"Cancelled after kitchen preparation started ({kitchenStatus})";
        var waste = await RecordWasteOnceAsync(reservation, command, wasteReason, connection, transaction, ct);
        var wasted = await _lifecycleService.TransitionInTransactionAsync(
            new TransitionReservationCommand(
                reservation.Id, PortionReservationStatus.Waste, command.ActorId, wasteReason, command.IdempotencyKey),
            connection!, transaction!, ct);
        await _balanceProjector.ApplyTerminalInTransactionAsync(wasted.Reservation, connection!, transaction!, ct);
        return CancellationDecisionResult.SuccessWaste(wasted.Reservation, waste!, wasteReason);
    }

    /// <summary>
    /// The waste movement for this reservation, recorded at most once whatever the idempotency key of the run
    /// that recorded it: an existing record for the reservation is returned as it is.
    /// </summary>
    private async Task<WasteRecord?> RecordWasteOnceAsync(
        PortionReservation reservation,
        ProcessCancellationCommand command,
        string reason,
        NpgsqlConnection? connection,
        NpgsqlTransaction? transaction,
        CancellationToken ct)
    {
        var existing = await _wasteService.GetWasteRecordsBySourceAsync(WasteSources.PortionReservation, reservation.Id, ct);
        if (existing.Count > 0)
            return existing[0];

        var request = new RecordWasteRequest(
            StockItemId: reservation.StockItemId,
            StockLocationId: reservation.StockLocationId,
            Quantity: reservation.Quantity,
            UnitCode: reservation.UnitCode,
            Reason: reason,
            RecordedBy: command.ActorId,
            WasteSource: WasteSources.PortionReservation,
            SourceReferenceId: reservation.Id,
            IdempotencyKey: !string.IsNullOrWhiteSpace(command.IdempotencyKey)
                ? "waste-" + command.IdempotencyKey.Trim()
                : $"waste-portion-reservation:{reservation.Id:N}");

        var result = await _wasteService.RecordWasteAsync(request, connection!, transaction!, ct);
        return result.Record;
    }
}
