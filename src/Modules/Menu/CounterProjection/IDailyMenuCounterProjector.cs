namespace ALKAROS.Menu.CounterProjection;

public interface IDailyMenuCounterProjector
{
    Task<DailyMenuItemCounters?> GetCountersAsync(Guid dailyMenuItemId, CancellationToken ct = default);

    Task<ApplyCounterResult> ApplyProductionOutputAsync(
        ProductionOutputCounterEvent evt,
        CancellationToken ct = default);

    Task<ApplyCounterResult> ApplyReservationReservedAsync(
        ReservationReservedCounterEvent evt,
        CancellationToken ct = default);

    Task<ApplyCounterResult> ApplyReservationTerminalAsync(
        ReservationTerminalCounterEvent evt,
        CancellationToken ct = default);

    /// <summary>
    /// Moves the portions of a consumed reservation that were given back out of <c>consumed</c>, and the wasted ones from
    /// <c>consumed</c> to <c>waste</c>. The event carries cumulative amounts: only the part above what was already applied
    /// counts, so a repeated or late delivery changes nothing. The reservation's own Reserved and Consumed events must
    /// have been applied first.
    /// </summary>
    Task<ApplyCounterResult> ApplyReservationAdjustmentAsync(
        ReservationAdjustmentCounterEvent evt,
        CancellationToken ct = default);

    Task<DailyMenuCounterRebuildReport> RebuildDailyMenuCountersAsync(
        Guid dailyMenuId,
        CancellationToken ct = default);

    Task<DailyMenuCounterDriftReport> DetectDriftAsync(
        Guid dailyMenuId,
        CancellationToken ct = default);
}
