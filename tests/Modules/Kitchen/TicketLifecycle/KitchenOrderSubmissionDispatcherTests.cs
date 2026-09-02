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
