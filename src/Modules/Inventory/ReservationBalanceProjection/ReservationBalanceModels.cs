using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.PortionReservations.Lifecycle;

namespace ALKAROS.Inventory.ReservationBalanceProjection;

public static class ReservationAppliedEventType
{
    public const string Reserved = "Reserved";
    public const string Terminal = "Terminal";
}

public sealed record ReservationAppliedEvent(
    Guid Id,
    Guid ReservationId,
    string EventType,
    string? TerminalStatus,
    Guid StockItemId,
    Guid StockLocationId,
    decimal Quantity,
    DateTimeOffset AppliedAt);

public sealed record ApplyReservationResult(
    StockBalance Balance,
    bool IsIdempotentReplay);

public sealed record ReservationBalanceRebuildReport(
    int TotalActiveReservations,
    int TotalBalancesUpdated,
    TimeSpan Duration);

public sealed record ReservationBalanceDrift(
    Guid StockItemId,
    Guid StockLocationId,
    decimal ProjectedReserved,
    decimal ActualReserved,
    decimal ReservedDrift,
    decimal ProjectedAvailable,
    decimal ExpectedAvailable,
    decimal AvailableDrift);

public sealed record ReservationBalanceDriftReport(
    bool HasDrift,
    IReadOnlyList<ReservationBalanceDrift> Drifts);
