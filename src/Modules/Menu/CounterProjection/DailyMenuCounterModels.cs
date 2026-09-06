namespace ALKAROS.Menu.CounterProjection;

public enum ReservationCounterTerminalStatus
{
    Released,
    Consumed,
    Waste
}

public sealed record ProductionOutputCounterEvent(
    Guid DailyMenuItemId,
    Guid ProductionOutputId,
    decimal Quantity,
    DateTimeOffset? OccurredAt = null);

public sealed record ReservationReservedCounterEvent(
    Guid DailyMenuItemId,
    Guid ReservationId,
    decimal Quantity,
    DateTimeOffset? OccurredAt = null);

public sealed record ReservationTerminalCounterEvent(
    Guid DailyMenuItemId,
    Guid ReservationId,
    ReservationCounterTerminalStatus TerminalStatus,
    decimal Quantity,
    DateTimeOffset? OccurredAt = null);

public sealed record DailyMenuItemCounters(
    Guid DailyMenuItemId,
    decimal PlannedPortions,
    decimal PreparedPortions,
    decimal AvailablePortions,
    decimal ReservedPortions,
    decimal ConsumedPortions,
    decimal WastePortions,
    bool IsOutOfStock);

public sealed record ApplyCounterResult(
    DailyMenuItemCounters Counters,
    bool IsIdempotentReplay);

public sealed record DailyMenuCounterDrift(
    Guid DailyMenuItemId,
    decimal ProjectedPrepared,
    decimal AuthoritativePrepared,
    decimal PreparedDrift,
    decimal ProjectedReserved,
    decimal AuthoritativeReserved,
    decimal ReservedDrift,
    decimal ProjectedConsumed,
    decimal AuthoritativeConsumed,
    decimal ConsumedDrift,
    decimal ProjectedWaste,
    decimal AuthoritativeWaste,
    decimal WasteDrift,
    decimal ProjectedAvailable,
    decimal AuthoritativeAvailable,
    decimal AvailableDrift);

public sealed record DailyMenuCounterDriftReport(
    bool HasDrift,
    IReadOnlyList<DailyMenuCounterDrift> Drifts);

public sealed record DailyMenuCounterRebuildReport(
    int RebuiltItemsCount,
    TimeSpan Elapsed);
