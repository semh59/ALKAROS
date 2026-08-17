namespace ALKAROS.Kitchen.Routing.Tests;

using ALKAROS.Kitchen.Routing;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

public sealed class KitchenRoutingTestDatabase : PgTestDatabase
{
    public KitchenRoutingTestDatabase()
        : base("alkaros_kit002_")
    {
    }

    public Task ExecuteSqlAsync(string sql) => RunAsync(DataSource, sql);

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        var upFiles = Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f);
        foreach (var file in upFiles)
        {
            await RunAsync(DataSource, await File.ReadAllTextAsync(file)).ConfigureAwait(false);
        }
    }
}

public sealed class PostgresRoutingIntegrationTests : IAsyncLifetime
{
    private readonly KitchenRoutingTestDatabase _db = new();
    private PostgresPrinterRepository _printerRepo = null!;
    private PostgresPrinterRouteRepository _routeRepo = null!;
    private KitchenRoutingService _routingService = null!;

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
        _printerRepo = new PostgresPrinterRepository(_db.DataSource);
        _routeRepo = new PostgresPrinterRouteRepository(_db.DataSource);
        _routingService = new KitchenRoutingService(_printerRepo, _routeRepo);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task PrinterRepositoryCrudOperationsSucceed()
    {
        var printer = new Printer(
            Guid.NewGuid(),
            "MainKitchenPrinter",
            "Station-Hot",
            "192.168.1.50",
            9100,
            isActive: true);

        await _printerRepo.SaveAsync(printer);

        var loaded = await _printerRepo.GetByIdAsync(printer.Id);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("MainKitchenPrinter");
        loaded.StationId.Should().Be("Station-Hot");
        loaded.IpAddress.Should().Be("192.168.1.50");
        loaded.Port.Should().Be(9100);
        loaded.IsActive.Should().BeTrue();

        // Update active state
        var updated = printer.SetActive(false);
        await _printerRepo.SaveAsync(updated);

        var reloaded = await _printerRepo.GetByIdAsync(printer.Id);
        reloaded!.IsActive.Should().BeFalse();

        var activeList = await _printerRepo.GetActiveAsync();
        activeList.Should().NotContain(p => p.Id == printer.Id);

        var allList = await _printerRepo.GetAllAsync();
        allList.Should().Contain(p => p.Id == printer.Id);

        // Delete
        await _printerRepo.DeleteAsync(printer.Id);
        var deleted = await _printerRepo.GetByIdAsync(printer.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task PrinterRouteRepositoryAtomicSaveAndResolutionSucceed()
    {
        var defaultPrinter = new Printer(Guid.NewGuid(), "DefaultP", "StMain");
        var catPrinter = new Printer(Guid.NewGuid(), "CatP", "StCat");
        await _printerRepo.SaveAsync(defaultPrinter);
        await _printerRepo.SaveAsync(catPrinter);

        var categoryId = Guid.NewGuid();
        var defaultRoute = PrinterRoute.CreateDefaultRoute(Guid.NewGuid(), defaultPrinter.Id);
        var catRoute = PrinterRoute.CreateCategoryRoute(Guid.NewGuid(), categoryId, catPrinter.Id);

        // Atomic save
        await _routeRepo.SaveRoutesAtomicallyAsync([defaultRoute, catRoute]);

        var activeRoutes = await _routeRepo.GetActiveRoutesAsync();
        activeRoutes.Should().HaveCount(2);

        // Resolve matching category
        var request = new RoutingEvaluationRequest(Guid.NewGuid(), categoryId);
        var result = await _routingService.ResolveItemRouteAsync(request);

        result.Resolved.Should().BeTrue();
        result.PrinterId.Should().Be(catPrinter.Id);
        result.RouteLevel.Should().Be(RouteLevel.Category);
    }

    [Fact]
    public async Task PostgresUniquePartialIndexEnforcesAmbiguityRejectionAtDatabaseLevel()
    {
        var printer1 = new Printer(Guid.NewGuid(), "P1", "St1");
        var printer2 = new Printer(Guid.NewGuid(), "P2", "St2");
        await _printerRepo.SaveAsync(printer1);
        await _printerRepo.SaveAsync(printer2);

        var categoryId = Guid.NewGuid();
        var route1 = PrinterRoute.CreateCategoryRoute(Guid.NewGuid(), categoryId, printer1.Id);
        var route2 = PrinterRoute.CreateCategoryRoute(Guid.NewGuid(), categoryId, printer2.Id);

        await _routeRepo.SaveRouteAsync(route1);

        // Attempting to insert a duplicate active category route should fail via partial unique index
        var act = async () => await _routeRepo.SaveRouteAsync(route2);
        await act.Should().ThrowAsync<PostgresException>()
            .Where(e => e.SqlState == "23505"); // unique_violation
    }

    [Fact]
    public async Task PostgresUniquePartialIndexEnforcesSingleDefaultRouteAtDatabaseLevel()
    {
        var printer1 = new Printer(Guid.NewGuid(), "DefP1", "St1");
        var printer2 = new Printer(Guid.NewGuid(), "DefP2", "St2");
        await _printerRepo.SaveAsync(printer1);
        await _printerRepo.SaveAsync(printer2);

        var route1 = PrinterRoute.CreateDefaultRoute(Guid.NewGuid(), printer1.Id);
        var route2 = PrinterRoute.CreateDefaultRoute(Guid.NewGuid(), printer2.Id);

        await _routeRepo.SaveRouteAsync(route1);

        // Duplicate active default route rejected by DB
        var act = async () => await _routeRepo.SaveRouteAsync(route2);
        await act.Should().ThrowAsync<PostgresException>()
            .Where(e => e.SqlState == "23505"); // unique_violation
    }

    [Fact]
    public async Task KitchenRoutingServiceApplyConfigurationValidatesAndSavesAtomically()
    {
        var pDefault = new Printer(Guid.NewGuid(), "PDefault", "Station0");
        var pItem = new Printer(Guid.NewGuid(), "PItem", "Station1");
        var pProduct = new Printer(Guid.NewGuid(), "PProduct", "Station2");
        await _printerRepo.SaveAsync(pDefault);
        await _printerRepo.SaveAsync(pItem);
        await _printerRepo.SaveAsync(pProduct);

        var itemId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var routes = new List<PrinterRoute>
        {
            PrinterRoute.CreateDefaultRoute(Guid.NewGuid(), pDefault.Id),
            PrinterRoute.CreateItemRoute(Guid.NewGuid(), itemId, pItem.Id),
            PrinterRoute.CreateProductRoute(Guid.NewGuid(), productId, pProduct.Id)
        };

        await _routingService.ApplyConfigurationAsync(routes, requireDefaultRoute: true);

        // Evaluate item
        var reqItem = new RoutingEvaluationRequest(productId, categoryId, itemId: itemId);
        var resItem = await _routingService.ResolveItemRouteAsync(reqItem);
        resItem.Resolved.Should().BeTrue();
        resItem.PrinterId.Should().Be(pItem.Id);
        resItem.RouteLevel.Should().Be(RouteLevel.Item);

        // Evaluate other item with same product
        var reqProduct = new RoutingEvaluationRequest(productId, categoryId, itemId: Guid.NewGuid());
        var resProduct = await _routingService.ResolveItemRouteAsync(reqProduct);
        resProduct.Resolved.Should().BeTrue();
        resProduct.PrinterId.Should().Be(pProduct.Id);
        resProduct.RouteLevel.Should().Be(RouteLevel.Product);
    }
}
