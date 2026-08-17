namespace ALKAROS.Kitchen.Routing;

/// <summary>
/// Repository interface for kitchen printer entities.
/// </summary>
public interface IPrinterRepository
{
    Task<Printer?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Printer>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Printer>> GetActiveAsync(CancellationToken ct = default);
    Task SaveAsync(Printer printer, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Repository interface for printer routing rules.
/// </summary>
public interface IPrinterRouteRepository
{
    Task<PrinterRoute?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PrinterRoute>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PrinterRoute>> GetActiveRoutesAsync(CancellationToken ct = default);
    Task SaveRouteAsync(PrinterRoute route, CancellationToken ct = default);
    Task SaveRoutesAtomicallyAsync(IReadOnlyList<PrinterRoute> routes, CancellationToken ct = default);
    Task DeleteRouteAsync(Guid id, CancellationToken ct = default);
}
