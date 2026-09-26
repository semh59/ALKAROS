using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.PortionReservations.Lifecycle;
using Npgsql;

namespace ALKAROS.Inventory.ReservationBalanceProjection;

public interface IReservationBalanceRepository
{
    Task<StockBalance?> GetBalanceAsync(Guid stockItemId, Guid stockLocationId, CancellationToken ct = default);
    Task<IReadOnlyList<StockBalance>> GetAllBalancesAsync(CancellationToken ct = default);
    Task<ApplyReservationResult> ApplyReservationCreatedAtomicAsync(PortionReservation reservation, CancellationToken ct = default);
    Task<ApplyReservationResult> ApplyReservationTerminalAtomicAsync(PortionReservation reservation, PortionReservationStatus terminalStatus, CancellationToken ct = default);

    /// <summary>
    /// V1-RMD-310: the same once-only terminal projection inside the caller's transaction. The default falls back
    /// to the self-committing version for in-memory fakes; the Postgres repository overrides it.
    /// </summary>
    Task<ApplyReservationResult> ApplyReservationTerminalAsync(
        PortionReservation reservation, PortionReservationStatus terminalStatus, NpgsqlConnection connection,
        NpgsqlTransaction transaction, CancellationToken ct = default)
        => ApplyReservationTerminalAtomicAsync(reservation, terminalStatus, ct);
    Task SetExactReservedBalanceAsync(Guid stockItemId, Guid stockLocationId, decimal reservedQuantity, CancellationToken ct = default);
    Task<IReadOnlyDictionary<(Guid StockItemId, Guid StockLocationId), decimal>> AggregateActiveReservationsAsync(CancellationToken ct = default);
    Task ResetAppliedEventsAsync(CancellationToken ct = default);
    Task RecordAppliedEventsForRebuildAsync(IReadOnlyList<ReservationAppliedEvent> events, CancellationToken ct = default);
}
