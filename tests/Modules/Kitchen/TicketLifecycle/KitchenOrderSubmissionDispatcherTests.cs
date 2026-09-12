namespace ALKAROS.Kitchen.TicketLifecycle.Tests;

using ALKAROS.Kitchen.Routing;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.Orders.OrderAggregate;
using FluentAssertions;
using Xunit;

/// <summary>
/// Covers the routing decision that lets a submitted order land on several
/// kitchen stations instead of a single env-var-pinned one
/// (deep-analysis finding B-3).
/// </summary>
public sealed class KitchenOrderSubmissionDispatcherTests
{
    private static readonly IKitchenPrinterRouter Router = new KitchenPrinterRouter();

    private static OrderItem Item(Guid productId) =>
        new(Guid.NewGuid(), Guid.NewGuid(), productId, "Item", 1m, 10m, 10m);

    [Fact]
    public void ProductRoutesSplitOrderItemsAcrossStationsAndUnroutedItemsUseTheDefault()
    {
        var grillPrinter = new Printer(Guid.NewGuid(), "Grill", "GRILL");
        var barPrinter = new Printer(Guid.NewGuid(), "Bar", "BAR");

        var grilledProduct = Guid.NewGuid();
        var barProduct = Guid.NewGuid();
        var unroutedProduct = Guid.NewGuid();

        var routes = new[]
        {
            PrinterRoute.CreateProductRoute(Guid.NewGuid(), grilledProduct, grillPrinter.Id),
            PrinterRoute.CreateProductRoute(Guid.NewGuid(), barProduct, barPrinter.Id),
            PrinterRoute.CreateDefaultRoute(Guid.NewGuid(), grillPrinter.Id),
        };

        var grilledItem = Item(grilledProduct);
        var barItem = Item(barProduct);
        var unroutedItem = Item(unroutedProduct);

        var map = KitchenOrderSubmissionDispatcher.MapItemsToStations(
            [grilledItem, barItem, unroutedItem],
            routes,
            [grillPrinter, barPrinter],
            Router,
            defaultStationId: "PASS");

        map[grilledItem.Id].Should().Be("GRILL");
        map[barItem.Id].Should().Be("BAR");
        // Falls through the precedence chain to the Default route's printer.
        map[unroutedItem.Id].Should().Be("GRILL");
    }

    /// <summary>
    /// V1-WTR-025's E2E audit (2026-09-12): RouteLevel.Category was fully
    /// implemented in KitchenPrinterRouter's own precedence chain but
    /// unreachable end to end - the only real caller of
    /// RoutingEvaluationRequest never populated CategoryId at all, so a
    /// configured category route (e.g. "the whole Izgara group goes to the
    /// grill printer") could never once match. This is the caller's own
    /// contract, not KitchenPrinterRouterTests' territory (that project
    /// already covers the router's own precedence logic in isolation).
    /// </summary>
    [Fact]
    public void CategoryRoutesSendEveryProductInThatCategoryToItsOwnPrinter()
    {
        var grillPrinter = new Printer(Guid.NewGuid(), "Grill", "GRILL");
        var grillCategoryId = Guid.NewGuid();
        var koftePrduct = Guid.NewGuid();
        var izgaraProduct = Guid.NewGuid();
        var uncategorizedProduct = Guid.NewGuid();

        var routes = new[] { PrinterRoute.CreateCategoryRoute(Guid.NewGuid(), grillCategoryId, grillPrinter.Id) };
        var categoryByProductId = new Dictionary<Guid, Guid?>
        {
            [koftePrduct] = grillCategoryId,
            [izgaraProduct] = grillCategoryId,
            // Deliberately absent: an item whose product this dictionary
            // never resolved (e.g. a product deleted between order time and
            // dispatch) must fall through to the default, not throw.
        };

        var kofteItem = Item(koftePrduct);
        var izgaraItem = Item(izgaraProduct);
        var uncategorizedItem = Item(uncategorizedProduct);

        var map = KitchenOrderSubmissionDispatcher.MapItemsToStations(
            [kofteItem, izgaraItem, uncategorizedItem],
            routes,
            [grillPrinter],
            Router,
            defaultStationId: "MAIN",
            categoryByProductId);

        map[kofteItem.Id].Should().Be("GRILL");
        map[izgaraItem.Id].Should().Be("GRILL");
        map[uncategorizedItem.Id].Should().Be("MAIN");
    }

    [Fact]
    public void WithNoRoutesEveryItemUsesTheDefaultStation()
    {
        var first = Item(Guid.NewGuid());
        var second = Item(Guid.NewGuid());

        var map = KitchenOrderSubmissionDispatcher.MapItemsToStations(
            [first, second],
            routes: [],
            printers: [],
            Router,
            defaultStationId: "MAIN");

        map[first.Id].Should().Be("MAIN");
        map[second.Id].Should().Be("MAIN");
    }

    [Fact]
    public void WhenAResolvedPrinterIsNotInThePrinterListTheItemFallsBackToTheDefault()
    {
        var missingPrinterId = Guid.NewGuid();
        var product = Guid.NewGuid();
        var routes = new[] { PrinterRoute.CreateProductRoute(Guid.NewGuid(), product, missingPrinterId) };
        var item = Item(product);

        var map = KitchenOrderSubmissionDispatcher.MapItemsToStations(
            [item],
            routes,
            printers: [],
            Router,
            defaultStationId: "MAIN");

        map[item.Id].Should().Be("MAIN");
    }
}
