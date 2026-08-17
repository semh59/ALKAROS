namespace ALKAROS.Kitchen.Routing;

/// <summary>
/// Service interface for orchestrating kitchen printer routing resolution.
/// </summary>
public interface IKitchenRoutingService
{
    Task<RoutingResult> ResolveItemRouteAsync(RoutingEvaluationRequest request, CancellationToken ct = default);
    Task ApplyConfigurationAsync(IReadOnlyList<PrinterRoute> routes, bool requireDefaultRoute = true, CancellationToken ct = default);
}

/// <summary>
/// High-level service that loads current printer and route state from repositories and executes deterministic resolution.
/// </summary>
public sealed class KitchenRoutingService : IKitchenRoutingService
{
    private readonly IPrinterRepository _printerRepository;
    private readonly IPrinterRouteRepository _routeRepository;
    private readonly IKitchenPrinterRouter _router;

    public KitchenRoutingService(
        IPrinterRepository printerRepository,
        IPrinterRouteRepository routeRepository,
        IKitchenPrinterRouter? router = null)
    {
        _printerRepository = printerRepository ?? throw new ArgumentNullException(nameof(printerRepository));
        _routeRepository = routeRepository ?? throw new ArgumentNullException(nameof(routeRepository));
        _router = router ?? new KitchenPrinterRouter();
    }

    public async Task<RoutingResult> ResolveItemRouteAsync(RoutingEvaluationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var printers = await _printerRepository.GetAllAsync(ct).ConfigureAwait(false);
        var routes = await _routeRepository.GetActiveRoutesAsync(ct).ConfigureAwait(false);

        return _router.ResolveRoute(request, routes, printers);
    }

    public async Task ApplyConfigurationAsync(
        IReadOnlyList<PrinterRoute> routes,
        bool requireDefaultRoute = true,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var printers = await _printerRepository.GetAllAsync(ct).ConfigureAwait(false);

        // Atomic validation
        PrinterRoutingConfigurationValidator.ValidateConfiguration(routes, printers, requireDefaultRoute);

        // Atomic persist
        await _routeRepository.SaveRoutesAtomicallyAsync(routes, ct).ConfigureAwait(false);
    }
}
