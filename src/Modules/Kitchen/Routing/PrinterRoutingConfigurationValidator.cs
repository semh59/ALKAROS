namespace ALKAROS.Kitchen.Routing;

/// <summary>
/// Validates printer routing configuration rules atomically (V0-DOM-011 Section 4).
/// </summary>
public static class PrinterRoutingConfigurationValidator
{
    /// <summary>
    /// Validates a set of printer routes against registered printers.
    /// Throws <see cref="InvalidPrinterConfigurationException"/> or <see cref="AmbiguousRouteException"/> if invalid.
    /// </summary>
    public static void ValidateConfiguration(
        IReadOnlyList<PrinterRoute> routes,
        IReadOnlyList<Printer> printers,
        bool requireDefaultRoute = true)
    {
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(printers);

        var printerIds = printers.Select(p => p.Id).ToHashSet();

        // 1. Printer must exist (Rule 3 in Section 4)
        foreach (var route in routes)
        {
            if (!printerIds.Contains(route.PrinterId))
            {
                throw new InvalidPrinterConfigurationException(
                    $"Route '{route.Id}' references non-existent printer '{route.PrinterId}'.");
            }
        }

        // Only validate active routes for duplication / completeness
        var activeRoutes = routes.Where(r => r.IsActive).ToList();

        // 2. Default must exist (Rule 2 in Section 4)
        if (requireDefaultRoute)
        {
            var defaultCount = activeRoutes.Count(r => r.RouteLevel == RouteLevel.Default);
            if (defaultCount == 0)
            {
                throw new InvalidPrinterConfigurationException(
                    "Configuration validation failed: At least one active Default printer route must be configured.");
            }
        }

        // 3. No duplicate routes at the same specificity level (Rule 1 in Section 4 & Ambiguity Rejection)
        // Default level duplicates
        var activeDefaults = activeRoutes.Where(r => r.RouteLevel == RouteLevel.Default).ToList();
        if (activeDefaults.Count > 1)
        {
            throw new AmbiguousRouteException(
                $"Duplicate default routes detected: {activeDefaults.Count} active default routes configured.");
        }

        // Category level duplicates
        var categoryGroups = activeRoutes
            .Where(r => r.RouteLevel == RouteLevel.Category && r.CategoryId.HasValue)
            .GroupBy(r => r.CategoryId!.Value);

        foreach (var group in categoryGroups)
        {
            if (group.Count() > 1)
            {
                throw new AmbiguousRouteException(
                    $"Duplicate category routes detected: Category '{group.Key}' has {group.Count()} active routes.");
            }
        }

        // Product level duplicates
        var productGroups = activeRoutes
            .Where(r => r.RouteLevel == RouteLevel.Product && r.ProductId.HasValue)
            .GroupBy(r => r.ProductId!.Value);

        foreach (var group in productGroups)
        {
            if (group.Count() > 1)
            {
                throw new AmbiguousRouteException(
                    $"Duplicate product routes detected: Product '{group.Key}' has {group.Count()} active routes.");
            }
        }

        // Item level duplicates
        var itemGroups = activeRoutes
            .Where(r => r.RouteLevel == RouteLevel.Item && r.ItemId.HasValue)
            .GroupBy(r => r.ItemId!.Value);

        foreach (var group in itemGroups)
        {
            if (group.Count() > 1)
            {
                throw new AmbiguousRouteException(
                    $"Duplicate item routes detected: Item '{group.Key}' has {group.Count()} active routes.");
            }
        }

        // Daily special level duplicates (date + category)
        var dailyGroups = activeRoutes
            .Where(r => r.RouteLevel == RouteLevel.DailySpecial && r.SpecialDate.HasValue && r.CategoryId.HasValue)
            .GroupBy(r => (r.SpecialDate!.Value, r.CategoryId!.Value));

        foreach (var group in dailyGroups)
        {
            if (group.Count() > 1)
            {
                throw new AmbiguousRouteException(
                    $"Duplicate daily special routes detected: Date '{group.Key.Item1}' and Category '{group.Key.Item2}' has {group.Count()} active routes.");
            }
        }
    }
}
