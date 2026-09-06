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

    Task<DailyMenuCounterRebuildReport> RebuildDailyMenuCountersAsync(
        Guid dailyMenuId,
        CancellationToken ct = default);

    Task<DailyMenuCounterDriftReport> DetectDriftAsync(
        Guid dailyMenuId,
        CancellationToken ct = default);
}
