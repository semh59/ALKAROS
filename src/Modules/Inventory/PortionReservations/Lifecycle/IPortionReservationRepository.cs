namespace ALKAROS.Inventory.PortionReservations.Lifecycle;

public interface IPortionReservationRepository
{
    Task InsertAsync(PortionReservation reservation, CancellationToken cancellationToken = default);
    Task<PortionReservation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PortionReservation?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PortionReservation>> GetByOrderItemIdAsync(Guid orderItemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PortionReservation>> GetActiveByStockItemAndLocationAsync(Guid stockItemId, Guid stockLocationId, CancellationToken cancellationToken = default);
    Task<bool> UpdateStatusOptimisticAsync(PortionReservation reservation, int expectedVersion, CancellationToken cancellationToken = default);
}
