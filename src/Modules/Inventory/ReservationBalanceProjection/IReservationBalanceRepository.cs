using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.PortionReservations.Lifecycle;

namespace ALKAROS.Inventory.ReservationBalanceProjection;

public interface IReservationBalanceRepository
{
    Task<StockBalance?> GetBalanceAsync(Guid stockItemId, Guid stockLocationId, CancellationToken ct = default);
    Task<IReadOnlyList<StockBalance>> GetAllBalancesAsync(CancellationToken ct = default);
    Task<ApplyReservationResult> ApplyReservationCreatedAtomicAsync(PortionReservation reservation, CancellationToken ct = default);
    Task<ApplyReservationResult> ApplyReservationTerminalAtomicAsync(PortionReservation reservation, PortionReservationStatus terminalStatus, CancellationToken ct = default);
    Task SetExactReservedBalanceAsync(Guid stockItemId, Guid stockLocationId, decimal reservedQuantity, CancellationToken ct = default);
    Task<IReadOnlyDictionary<(Guid StockItemId, Guid StockLocationId), decimal>> AggregateActiveReservationsAsync(CancellationToken ct = default);
    Task ResetAppliedEventsAsync(CancellationToken ct = default);
    Task RecordAppliedEventsForRebuildAsync(IReadOnlyList<ReservationAppliedEvent> events, CancellationToken ct = default);
}
