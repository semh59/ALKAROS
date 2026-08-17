namespace ALKAROS.Kitchen.TicketLifecycle;

public interface IKitchenTicketRepository
{
    Task<KitchenTicket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KitchenTicket>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KitchenTicket>> GetActiveByStationAsync(string stationId, CancellationToken cancellationToken = default);
    Task AddAsync(KitchenTicket ticket, CancellationToken cancellationToken = default);
    Task<long> SaveAsync(KitchenTicket ticket, long expectedRowVersion, CancellationToken cancellationToken = default);
}
