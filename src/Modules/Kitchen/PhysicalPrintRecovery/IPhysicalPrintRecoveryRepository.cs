namespace ALKAROS.Kitchen.PhysicalPrintRecovery;

/// <summary>
/// Repository interface for persisting physical print deliveries (kitchen.physical_print_deliveries).
/// </summary>
public interface IPhysicalPrintRecoveryRepository
{
    Task<PhysicalPrintDelivery?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PhysicalPrintDelivery>> GetByPrintJobIdAsync(Guid printJobId, CancellationToken ct = default);
    Task<IReadOnlyList<PhysicalPrintDelivery>> GetByTicketIdAsync(Guid ticketId, CancellationToken ct = default);
    Task<IReadOnlyList<PhysicalPrintDelivery>> GetPendingUnknownDeliveriesAsync(CancellationToken ct = default);
    Task AddAsync(PhysicalPrintDelivery delivery, CancellationToken ct = default);
    Task SaveAsync(PhysicalPrintDelivery delivery, CancellationToken ct = default);
}
