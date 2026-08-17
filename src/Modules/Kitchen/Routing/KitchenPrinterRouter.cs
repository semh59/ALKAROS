namespace ALKAROS.Kitchen.Routing;

/// <summary>
/// Reference implementation of deterministic kitchen printer routing (V0-DOM-011 / V1-KIT-002).
/// Implements Most-Specific-Wins with explicit fallback chain and ambiguity rejection.
/// </summary>
public sealed class KitchenPrinterRouter : IKitchenPrinterRouter
{
    public RoutingResult ResolveRoute(
        RoutingEvaluationRequest request,
        IReadOnlyList<PrinterRoute> routes,
        IReadOnlyList<Printer> printers)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(printers);

        var printerLookup = printers.DistinctBy(p => p.Id).ToDictionary(p => p.Id);

        // Group active matching routes by RouteLevel
        var activeRoutes = routes.Where(r => r.IsActive).ToList();

        // 1. Check for Ambiguity (Rule 3) at each specificity level
        var matchingItemRoutes = request.ItemId.HasValue
            ? activeRoutes.Where(r => r.RouteLevel == RouteLevel.Item && r.ItemId == request.ItemId.Value).ToList()
            : [];

        if (matchingItemRoutes.Count > 1)
        {
            return RoutingResult.AmbiguousRoute(
                $"Ambiguous routing: {matchingItemRoutes.Count} active item routes found for item '{request.ItemId}'.");
        }

        var matchingProductRoutes = activeRoutes
            .Where(r => r.RouteLevel == RouteLevel.Product && r.ProductId == request.ProductId)
            .ToList();

        if (matchingProductRoutes.Count > 1)
        {
            return RoutingResult.AmbiguousRoute(
                $"Ambiguous routing: {matchingProductRoutes.Count} active product routes found for product '{request.ProductId}'.");
        }

        var reqDate = request.Date;
        var matchingDailyRoutes = reqDate.HasValue
            ? activeRoutes.Where(r => r.RouteLevel == RouteLevel.DailySpecial &&
                                      r.SpecialDate == reqDate.Value &&
                                      r.CategoryId == request.CategoryId).ToList()
            : [];

        if (matchingDailyRoutes.Count > 1)
        {
            return RoutingResult.AmbiguousRoute(
                $"Ambiguous routing: {matchingDailyRoutes.Count} active daily special routes found for category '{request.CategoryId}' on date '{reqDate}'.");
        }

        var matchingCategoryRoutes = activeRoutes
            .Where(r => r.RouteLevel == RouteLevel.Category && r.CategoryId == request.CategoryId)
            .ToList();

        if (matchingCategoryRoutes.Count > 1)
        {
            return RoutingResult.AmbiguousRoute(
                $"Ambiguous routing: {matchingCategoryRoutes.Count} active category routes found for category '{request.CategoryId}'.");
        }

        var matchingDefaultRoutes = activeRoutes
            .Where(r => r.RouteLevel == RouteLevel.Default)
            .ToList();

        if (matchingDefaultRoutes.Count > 1)
        {
            return RoutingResult.AmbiguousRoute(
                $"Ambiguous routing: {matchingDefaultRoutes.Count} active default routes found in configuration.");
        }

        // 2. Precedence Hierarchy & Fallback Chain (Rules 1, 2, 4)
        // Level 1: Item-level
        if (matchingItemRoutes.Count == 1)
        {
            var itemRoute = matchingItemRoutes[0];
            if (printerLookup.TryGetValue(itemRoute.PrinterId, out var printer) && printer.IsActive)
            {
                return RoutingResult.Success(printer.Id, RouteLevel.Item);
            }
            // If printer is disabled or missing -> Fallback to next level
        }

        // Level 2: Product-level
        if (matchingProductRoutes.Count == 1)
        {
            var productRoute = matchingProductRoutes[0];
            if (printerLookup.TryGetValue(productRoute.PrinterId, out var printer) && printer.IsActive)
            {
                return RoutingResult.Success(printer.Id, RouteLevel.Product);
            }
            // If printer is disabled or missing -> Fallback to next level
        }

        // Level 3: Daily Special-level
        if (matchingDailyRoutes.Count == 1)
        {
            var dailyRoute = matchingDailyRoutes[0];
            if (printerLookup.TryGetValue(dailyRoute.PrinterId, out var printer) && printer.IsActive)
            {
                return RoutingResult.Success(printer.Id, RouteLevel.DailySpecial);
            }
            // If printer is disabled or missing -> Fallback to next level
        }

        // Level 4: Category-level
        if (matchingCategoryRoutes.Count == 1)
        {
            var categoryRoute = matchingCategoryRoutes[0];
            if (printerLookup.TryGetValue(categoryRoute.PrinterId, out var printer) && printer.IsActive)
            {
                return RoutingResult.Success(printer.Id, RouteLevel.Category);
            }
            // If printer is disabled or missing -> Fallback to next level
        }

        // Level 5: Default route
        if (matchingDefaultRoutes.Count == 1)
        {
            var defaultRoute = matchingDefaultRoutes[0];
            if (printerLookup.TryGetValue(defaultRoute.PrinterId, out var printer) && printer.IsActive)
            {
                return RoutingResult.Success(printer.Id, RouteLevel.Default);
            }
        }

        // If no matching route or all candidate printers are disabled -> NO_AVAILABLE_PRINTER
        return RoutingResult.NoAvailablePrinter(
            "No active printer available for kitchen item across all routing specificity levels.");
    }
}
