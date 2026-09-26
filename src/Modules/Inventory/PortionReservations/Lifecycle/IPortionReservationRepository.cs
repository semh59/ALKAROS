using Npgsql;

namespace ALKAROS.Inventory.PortionReservations.Lifecycle;

public interface IPortionReservationRepository
{
    Task InsertAsync(PortionReservation reservation, CancellationToken cancellationToken = default);
    Task<PortionReservation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PortionReservation?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PortionReservation>> GetByOrderItemIdAsync(Guid orderItemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PortionReservation>> GetActiveByStockItemAndLocationAsync(Guid stockItemId, Guid stockLocationId, CancellationToken cancellationToken = default);
    Task<bool> UpdateStatusOptimisticAsync(PortionReservation reservation, int expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// V1-RMD-310: reads the reservation row locked (FOR UPDATE) inside the caller's transaction. The default
    /// falls back to the unlocked read for in-memory fakes; the Postgres repository overrides it.
    /// </summary>
    Task<PortionReservation?> GetByIdForUpdateAsync(
        Guid id, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    /// <summary>V1-RMD-310: the optimistic status update inside the caller's transaction.</summary>
    Task<bool> UpdateStatusOptimisticAsync(
        PortionReservation reservation, int expectedVersion, NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
        => UpdateStatusOptimisticAsync(reservation, expectedVersion, cancellationToken);
}
