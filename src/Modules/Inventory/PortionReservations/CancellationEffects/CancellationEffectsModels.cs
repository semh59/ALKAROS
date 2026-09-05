using ALKAROS.Inventory.PortionReservations.Lifecycle;
using ALKAROS.Inventory.WasteRecording;

namespace ALKAROS.Inventory.PortionReservations.CancellationEffects;

public enum CancellationAction
{
    Release,
    Waste
}

public enum KitchenItemPreparationStatus
{
    NotStarted,
    InProgress,
    Completed
}

public sealed record ProcessCancellationCommand(
    Guid ReservationId,
    Guid OrderItemId,
    Guid ActorId,
    string? CancellationReason = null,
    string? IdempotencyKey = null);

public sealed record CancellationDecisionResult(
    CancellationAction Action,
    string Reason,
    PortionReservation Reservation,
    WasteRecord? WasteRecord,
    bool IsIdempotentReplay)
{
    public static CancellationDecisionResult SuccessRelease(PortionReservation reservation, string reason) =>
        new(CancellationAction.Release, reason, reservation, null, false);

    public static CancellationDecisionResult ReplayRelease(PortionReservation reservation, string reason) =>
        new(CancellationAction.Release, reason, reservation, null, true);

    public static CancellationDecisionResult SuccessWaste(PortionReservation reservation, WasteRecord wasteRecord, string reason) =>
        new(CancellationAction.Waste, reason, reservation, wasteRecord, false);

    public static CancellationDecisionResult ReplayWaste(PortionReservation reservation, string reason) =>
        new(CancellationAction.Waste, reason, reservation, null, true);
}
