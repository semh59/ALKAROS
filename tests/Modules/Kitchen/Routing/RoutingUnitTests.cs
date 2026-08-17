namespace ALKAROS.Kitchen.Routing.Tests;

using ALKAROS.Kitchen.Routing;
using FluentAssertions;
using Xunit;

public sealed class RoutingUnitTests
{
    private readonly KitchenPrinterRouter _router = new();

    private readonly Printer _printerItem = new(Guid.NewGuid(), "ItemPrinter", "Station1", "192.168.1.101", 9100, isActive: true);
    private readonly Printer _printerProduct = new(Guid.NewGuid(), "ProductPrinter", "Station2", "192.168.1.102", 9100, isActive: true);
    private readonly Printer _printerDaily = new(Guid.NewGuid(), "DailyPrinter", "Station3", "192.168.1.103", 9100, isActive: true);
    private readonly Printer _printerCategory = new(Guid.NewGuid(), "CategoryPrinter", "Station4", "192.168.1.104", 9100, isActive: true);
    private readonly Printer _printerDefault = new(Guid.NewGuid(), "DefaultPrinter", "StationMain", "192.168.1.100", 9100, isActive: true);

    [Fact]
    public void Rule1ItemOverridesProductCategoryAndDefault()
    {
        // Example 1 from V0-DOM-011: Item-level override wins over all lower specificity routes
        var itemId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var routes = new List<PrinterRoute>
        {
            PrinterRoute.CreateDefaultRoute(Guid.NewGuid(), _printerDefault.Id),
            PrinterRoute.CreateCategoryRoute(Guid.NewGuid(), categoryId, _printerCategory.Id),
            PrinterRoute.CreateProductRoute(Guid.NewGuid(), productId, _printerProduct.Id),
            PrinterRoute.CreateItemRoute(Guid.NewGuid(), itemId, _printerItem.Id)
        };

        var request = new RoutingEvaluationRequest(productId, categoryId, itemId: itemId);
        var result = _router.ResolveRoute(request, routes, [_printerItem, _printerProduct, _printerCategory, _printerDefault]);

        result.Resolved.Should().BeTrue();
        result.PrinterId.Should().Be(_printerItem.Id);
        result.RouteLevel.Should().Be(RouteLevel.Item);
    }

    [Fact]
    public void Rule1ProductOverridesDailySpecialCategoryAndDefault()
    {
        var productId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var date = new DateOnly(2026, 8, 17);

        var routes = new List<PrinterRoute>
        {
            PrinterRoute.CreateDefaultRoute(Guid.NewGuid(), _printerDefault.Id),
            PrinterRoute.CreateCategoryRoute(Guid.NewGuid(), categoryId, _printerCategory.Id),
            PrinterRoute.CreateDailySpecialRoute(Guid.NewGuid(), date, categoryId, _printerDaily.Id),
            PrinterRoute.CreateProductRoute(Guid.NewGuid(), productId, _printerProduct.Id)
        };

        var request = new RoutingEvaluationRequest(productId, categoryId, date: date);
        var result = _router.ResolveRoute(request, routes, [_printerProduct, _printerDaily, _printerCategory, _printerDefault]);

        result.Resolved.Should().BeTrue();
        result.PrinterId.Should().Be(_printerProduct.Id);
        result.RouteLevel.Should().Be(RouteLevel.Product);
    }

    [Fact]
    public void Rule1DailySpecialOverridesCategoryAndDefault()
    {
        var productId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var date = new DateOnly(2026, 8, 17);

        var routes = new List<PrinterRoute>
        {
            PrinterRoute.CreateDefaultRoute(Guid.NewGuid(), _printerDefault.Id),
            PrinterRoute.CreateCategoryRoute(Guid.NewGuid(), categoryId, _printerCategory.Id),
            PrinterRoute.CreateDailySpecialRoute(Guid.NewGuid(), date, categoryId, _printerDaily.Id)
        };

        var request = new RoutingEvaluationRequest(productId, categoryId, date: date);
        var result = _router.ResolveRoute(request, routes, [_printerDaily, _printerCategory, _printerDefault]);

        result.Resolved.Should().BeTrue();
        result.PrinterId.Should().Be(_printerDaily.Id);
        result.RouteLevel.Should().Be(RouteLevel.DailySpecial);
    }

    [Fact]
    public void Rule1CategoryOverridesDefault()
    {
        var productId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var routes = new List<PrinterRoute>
        {
            PrinterRoute.CreateDefaultRoute(Guid.NewGuid(), _printerDefault.Id),
            PrinterRoute.CreateCategoryRoute(Guid.NewGuid(), categoryId, _printerCategory.Id)
        };

        var request = new RoutingEvaluationRequest(productId, categoryId);
        var result = _router.ResolveRoute(request, routes, [_printerCategory, _printerDefault]);

        result.Resolved.Should().BeTrue();
        result.PrinterId.Should().Be(_printerCategory.Id);
        result.RouteLevel.Should().Be(RouteLevel.Category);
    }

    [Fact]
    public void Rule1FallsBackToDefaultWhenNoHigherRouteMatches()
    {
        var productId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var routes = new List<PrinterRoute>
        {
            PrinterRoute.CreateDefaultRoute(Guid.NewGuid(), _printerDefault.Id)
        };

        var request = new RoutingEvaluationRequest(productId, categoryId);
        var result = _router.ResolveRoute(request, routes, [_printerDefault]);

        result.Resolved.Should().BeTrue();
        result.PrinterId.Should().Be(_printerDefault.Id);
        result.RouteLevel.Should().Be(RouteLevel.Default);
    }

    [Fact]
    public void Rule2DisabledItemPrinterFallsBackToCategoryRoute()
    {
        // Example 2 from V0-DOM-011: Item printer is disabled -> falls back to Category printer
        var itemId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var disabledItemPrinter = _printerItem.SetActive(false);

        var routes = new List<PrinterRoute>
        {
            PrinterRoute.CreateDefaultRoute(Guid.NewGuid(), _printerDefault.Id),
            PrinterRoute.CreateCategoryRoute(Guid.NewGuid(), categoryId, _printerCategory.Id),
            PrinterRoute.CreateItemRoute(Guid.NewGuid(), itemId, disabledItemPrinter.Id)
        };

        var request = new RoutingEvaluationRequest(productId, categoryId, itemId: itemId);
        var result = _router.ResolveRoute(request, routes, [disabledItemPrinter, _printerCategory, _printerDefault]);

        result.Resolved.Should().BeTrue();
        result.PrinterId.Should().Be(_printerCategory.Id);
        result.RouteLevel.Should().Be(RouteLevel.Category);
    }

    [Fact]
    public void Rule2DisabledProductFallsBackToDailySpecial()
    {
        var productId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var date = new DateOnly(2026, 8, 17);

        var disabledProductPrinter = _printerProduct.SetActive(false);

        var routes = new List<PrinterRoute>
        {
            PrinterRoute.CreateDefaultRoute(Guid.NewGuid(), _printerDefault.Id),
            PrinterRoute.CreateCategoryRoute(Guid.NewGuid(), categoryId, _printerCategory.Id),
            PrinterRoute.CreateDailySpecialRoute(Guid.NewGuid(), date, categoryId, _printerDaily.Id),
            PrinterRoute.CreateProductRoute(Guid.NewGuid(), productId, disabledProductPrinter.Id)
        };

        var request = new RoutingEvaluationRequest(productId, categoryId, date: date);
        var result = _router.ResolveRoute(request, routes, [disabledProductPrinter, _printerDaily, _printerCategory, _printerDefault]);

        result.Resolved.Should().BeTrue();
        result.PrinterId.Should().Be(_printerDaily.Id);
        result.RouteLevel.Should().Be(RouteLevel.DailySpecial);
    }

    [Fact]
    public void Rule2DisabledItemAndCategoryPrintersFallBackToDefault()
    {
        var itemId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var disabledItemPrinter = _printerItem.SetActive(false);
        var disabledCategoryPrinter = _printerCategory.SetActive(false);

        var routes = new List<PrinterRoute>
        {
            PrinterRoute.CreateDefaultRoute(Guid.NewGuid(), _printerDefault.Id),
            PrinterRoute.CreateCategoryRoute(Guid.NewGuid(), categoryId, disabledCategoryPrinter.Id),
            PrinterRoute.CreateItemRoute(Guid.NewGuid(), itemId, disabledItemPrinter.Id)
        };

        var request = new RoutingEvaluationRequest(productId, categoryId, itemId: itemId);
        var result = _router.ResolveRoute(request, routes, [disabledItemPrinter, disabledCategoryPrinter, _printerDefault]);

        result.Resolved.Should().BeTrue();
        result.PrinterId.Should().Be(_printerDefault.Id);
        result.RouteLevel.Should().Be(RouteLevel.Default);
    }

    [Fact]
    public void Rule2AllPrintersDisabledReturnsNoAvailablePrinter()
    {
        // Example 4 from V0-DOM-011: All matching routes point to disabled printers
        var itemId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var disabledItemPrinter = _printerItem.SetActive(false);
        var disabledCategoryPrinter = _printerCategory.SetActive(false);
        var disabledDefaultPrinter = _printerDefault.SetActive(false);

        var routes = new List<PrinterRoute>
        {
            PrinterRoute.CreateDefaultRoute(Guid.NewGuid(), disabledDefaultPrinter.Id),
            PrinterRoute.CreateCategoryRoute(Guid.NewGuid(), categoryId, disabledCategoryPrinter.Id),
            PrinterRoute.CreateItemRoute(Guid.NewGuid(), itemId, disabledItemPrinter.Id)
        };

        var request = new RoutingEvaluationRequest(productId, categoryId, itemId: itemId);
        var result = _router.ResolveRoute(request, routes, [disabledItemPrinter, disabledCategoryPrinter, disabledDefaultPrinter]);

        result.Resolved.Should().BeFalse();
        result.ErrorCode.Should().Be(RoutingResult.ErrorNoAvailablePrinter);
        result.PrinterId.Should().BeNull();
    }

    [Fact]
    public void Rule3AmbiguousCategoryRoutesReturnsAmbiguousRouteError()
    {
        // Example 3 from V0-DOM-011: Duplicate category routes
        var productId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var routes = new List<PrinterRoute>
        {
            PrinterRoute.CreateCategoryRoute(Guid.NewGuid(), categoryId, _printerCategory.Id),
            PrinterRoute.CreateCategoryRoute(Guid.NewGuid(), categoryId, _printerProduct.Id)
        };

        var request = new RoutingEvaluationRequest(productId, categoryId);
        var result = _router.ResolveRoute(request, routes, [_printerCategory, _printerProduct, _printerDefault]);

        result.Resolved.Should().BeFalse();
        result.ErrorCode.Should().Be(RoutingResult.ErrorAmbiguousRoute);
    }

    [Fact]
    public void Rule3AmbiguousDefaultRoutesReturnsAmbiguousRouteError()
    {
        var productId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var routes = new List<PrinterRoute>
        {
            PrinterRoute.CreateDefaultRoute(Guid.NewGuid(), _printerDefault.Id),
            PrinterRoute.CreateDefaultRoute(Guid.NewGuid(), _printerCategory.Id)
        };

        var request = new RoutingEvaluationRequest(productId, categoryId);
        var result = _router.ResolveRoute(request, routes, [_printerCategory, _printerDefault]);

        result.Resolved.Should().BeFalse();
        result.ErrorCode.Should().Be(RoutingResult.ErrorAmbiguousRoute);
    }

    [Fact]
    public void RouterHandlesDuplicatePrinterInstancesGracefully()
    {
        var categoryId = Guid.NewGuid();
        var routes = new List<PrinterRoute>
        {
            PrinterRoute.CreateCategoryRoute(Guid.NewGuid(), categoryId, _printerCategory.Id)
        };

        var request = new RoutingEvaluationRequest(Guid.NewGuid(), categoryId);
        // Duplicate instances in list
        var result = _router.ResolveRoute(request, routes, [_printerCategory, _printerCategory]);

        result.Resolved.Should().BeTrue();
        result.PrinterId.Should().Be(_printerCategory.Id);
    }

    [Fact]
    public void ValidatorRejectsNonExistentPrinterReference()
    {
        var unknownPrinterId = Guid.NewGuid();
        var routes = new List<PrinterRoute>
        {
            PrinterRoute.CreateDefaultRoute(Guid.NewGuid(), unknownPrinterId)
        };

        var act = () => PrinterRoutingConfigurationValidator.ValidateConfiguration(routes, [_printerDefault]);
        act.Should().Throw<InvalidPrinterConfigurationException>()
            .WithMessage($"*{unknownPrinterId}*");
    }

    [Fact]
    public void ValidatorRejectsMissingDefaultRouteWhenRequired()
    {
        var categoryId = Guid.NewGuid();
        var routes = new List<PrinterRoute>
        {
            PrinterRoute.CreateCategoryRoute(Guid.NewGuid(), categoryId, _printerCategory.Id)
        };

        var act = () => PrinterRoutingConfigurationValidator.ValidateConfiguration(routes, [_printerCategory], requireDefaultRoute: true);
        act.Should().Throw<InvalidPrinterConfigurationException>()
            .WithMessage("*Default printer route must be configured*");
    }

    [Fact]
    public void ValidatorRejectsDuplicateCategoryRoutes()
    {
        var categoryId = Guid.NewGuid();
        var routes = new List<PrinterRoute>
        {
            PrinterRoute.CreateDefaultRoute(Guid.NewGuid(), _printerDefault.Id),
            PrinterRoute.CreateCategoryRoute(Guid.NewGuid(), categoryId, _printerCategory.Id),
            PrinterRoute.CreateCategoryRoute(Guid.NewGuid(), categoryId, _printerProduct.Id)
        };

        var act = () => PrinterRoutingConfigurationValidator.ValidateConfiguration(routes, [_printerCategory, _printerProduct, _printerDefault]);
        act.Should().Throw<AmbiguousRouteException>()
            .WithMessage($"*{categoryId}*");
    }

    [Fact]
    public void EntityEnforcesDomainValidationInvariants()
    {
        // Printer invariants
        var actEmptyName = () => new Printer(Guid.NewGuid(), " ", "Station1");
        actEmptyName.Should().Throw<ArgumentException>();

        var actEmptyStation = () => new Printer(Guid.NewGuid(), "Printer1", "");
        actEmptyStation.Should().Throw<ArgumentException>();

        var actInvalidPort = () => new Printer(Guid.NewGuid(), "Printer1", "Station1", port: 99999);
        actInvalidPort.Should().Throw<ArgumentOutOfRangeException>();

        // Route invariants
        var actInvalidItemRoute = () => new PrinterRoute(Guid.NewGuid(), RouteLevel.Item, _printerItem.Id, itemId: null);
        actInvalidItemRoute.Should().Throw<ArgumentException>();

        var actInvalidProductRoute = () => new PrinterRoute(Guid.NewGuid(), RouteLevel.Product, _printerProduct.Id, productId: null);
        actInvalidProductRoute.Should().Throw<ArgumentException>();

        var actInvalidCategoryRoute = () => new PrinterRoute(Guid.NewGuid(), RouteLevel.Category, _printerCategory.Id, categoryId: null);
        actInvalidCategoryRoute.Should().Throw<ArgumentException>();

        var actInvalidDefaultRoute = () => new PrinterRoute(Guid.NewGuid(), RouteLevel.Default, _printerDefault.Id, itemId: Guid.NewGuid());
        actInvalidDefaultRoute.Should().Throw<ArgumentException>();
    }
}
