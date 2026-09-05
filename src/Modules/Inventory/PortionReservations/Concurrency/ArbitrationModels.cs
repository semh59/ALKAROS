using ALKAROS.Inventory.PortionReservations.Lifecycle;

namespace ALKAROS.Inventory.PortionReservations.Concurrency;

public enum ArbitrationStatus
{
    Reserved,
    OutOfStock,
    IdempotentReplay
}

public sealed record ArbitrateReservationCommand(
    Guid OrderId,
    Guid OrderItemId,
    Guid StockItemId,
    Guid StockLocationId,
    decimal Quantity,
    string UnitCode,
    Guid ActorId,
    string? IdempotencyKey = null,
    string? MetadataJson = null);

public sealed record ReservationArbitrationResult(
    ArbitrationStatus Status,
    PortionReservation? Reservation,
    decimal RemainingAvailableQuantity,
    string? FailureReason,
    bool IsSuccess)
{
    public static ReservationArbitrationResult Success(PortionReservation reservation, decimal remainingAvailable) =>
        new(ArbitrationStatus.Reserved, reservation, remainingAvailable, null, true);

    public static ReservationArbitrationResult Replay(PortionReservation reservation, decimal remainingAvailable) =>
        new(ArbitrationStatus.IdempotentReplay, reservation, remainingAvailable, null, true);

    public static ReservationArbitrationResult OutOfStock(decimal remainingAvailable, string reason = "Insufficient available stock.") =>
        new(ArbitrationStatus.OutOfStock, null, remainingAvailable, reason, false);
}
