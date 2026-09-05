using FluentAssertions;
using Xunit;

namespace ALKAROS.Menu.DailyMenuLifecycle.Tests;

public sealed class FakeDailyMenuRepository : IDailyMenuRepository
{
    private readonly Dictionary<Guid, DailyMenu> _menus = new();

    public Task InsertAsync(DailyMenu menu, CancellationToken ct = default)
    {
        if (_menus.Values.Any(m => m.BusinessDate == menu.BusinessDate))
            throw new DuplicateDailyMenuBusinessDateException(menu.BusinessDate);

        _menus[menu.Id] = menu;
        return Task.CompletedTask;
    }

    public Task<DailyMenu?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_menus.GetValueOrDefault(id));

    public Task<DailyMenu?> GetByBusinessDateAsync(DateOnly businessDate, CancellationToken ct = default) =>
        Task.FromResult(_menus.Values.FirstOrDefault(m => m.BusinessDate == businessDate));

    public Task UpdateAsync(DailyMenu menu, CancellationToken ct = default)
    {
        if (!_menus.ContainsKey(menu.Id))
            throw new DailyMenuNotFoundException(menu.Id);
        _menus[menu.Id] = menu;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DailyMenu>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<DailyMenu>>(_menus.Values.OrderByDescending(m => m.BusinessDate).ToList());
}

public sealed class FakeDailyMenuItemRepository : IDailyMenuItemRepository
{
    private readonly Dictionary<Guid, DailyMenuItem> _items = new();

    public Task InsertAsync(DailyMenuItem item, CancellationToken ct = default)
    {
        if (_items.Values.Any(i => i.DailyMenuId == item.DailyMenuId && i.ProductId == item.ProductId))
            throw new DuplicateDailyMenuItemException(item.DailyMenuId, item.ProductId);

        _items[item.Id] = item;
        return Task.CompletedTask;
    }

    public Task<DailyMenuItem?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_items.GetValueOrDefault(id));

    public Task<DailyMenuItem?> GetByMenuAndProductAsync(Guid dailyMenuId, Guid productId, CancellationToken ct = default) =>
        Task.FromResult(_items.Values.FirstOrDefault(i => i.DailyMenuId == dailyMenuId && i.ProductId == productId));

    public Task<IReadOnlyList<DailyMenuItem>> GetByDailyMenuIdAsync(Guid dailyMenuId, bool activeOnly = false, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<DailyMenuItem>>(_items.Values
            .Where(i => i.DailyMenuId == dailyMenuId && (!activeOnly || i.IsActive))
            .OrderBy(i => i.CreatedAt)
            .ToList());

    public Task UpdateAsync(DailyMenuItem item, CancellationToken ct = default)
    {
        if (!_items.ContainsKey(item.Id))
            throw new DailyMenuItemNotFoundException(item.Id);
        _items[item.Id] = item;
        return Task.CompletedTask;
    }
}

public sealed class FakeDailyMenuItemHistoryRepository : IDailyMenuItemHistoryRepository
{
    private readonly List<DailyMenuItemHistory> _histories = new();

    public Task InsertAsync(DailyMenuItemHistory history, CancellationToken ct = default)
    {
        _histories.Add(history);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DailyMenuItemHistory>> GetByDailyMenuItemIdAsync(Guid dailyMenuItemId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<DailyMenuItemHistory>>(_histories.Where(h => h.DailyMenuItemId == dailyMenuItemId).OrderBy(h => h.ChangedAt).ToList());
}

public sealed class FakeCatalogProductPriceReader : ICatalogProductPriceReader
{
    private readonly Dictionary<Guid, CatalogProductPriceInfo> _catalog = new();

    public void AddProduct(Guid id, string name, decimal price, bool active = true)
    {
        _catalog[id] = new CatalogProductPriceInfo(id, "SKU-" + id.ToString("N")[..6], name, price, active);
    }

    public Task<CatalogProductPriceInfo?> GetProductPriceInfoAsync(Guid productId, CancellationToken ct = default) =>
        Task.FromResult(_catalog.GetValueOrDefault(productId));
}

public sealed class FakeRecipeVersionValidator : IRecipeVersionValidator
{
    private readonly HashSet<Guid> _activeVersions = new();

    public void AddActiveVersion(Guid versionId) => _activeVersions.Add(versionId);

    public Task<bool> IsRecipeVersionActiveAsync(Guid recipeVersionId, CancellationToken ct = default) =>
        Task.FromResult(_activeVersions.Contains(recipeVersionId));
}

public sealed class FakeBusinessDateProvider : IBusinessDateProvider
{
    public DateOnly CurrentDate { get; set; } = new DateOnly(2026, 9, 6);

    public DateOnly GetCurrentBusinessDate(DateTimeOffset? utcNow = null) => CurrentDate;
}

public sealed class DailyMenuLifecycleDomainTests
{
    private readonly FakeDailyMenuRepository _menuRepo;
    private readonly FakeDailyMenuItemRepository _itemRepo;
    private readonly FakeDailyMenuItemHistoryRepository _historyRepo;
    private readonly FakeCatalogProductPriceReader _catalogReader;
    private readonly FakeRecipeVersionValidator _recipeValidator;
    private readonly FakeBusinessDateProvider _dateProvider;
    private readonly DailyMenuService _service;

    public DailyMenuLifecycleDomainTests()
    {
        _menuRepo = new FakeDailyMenuRepository();
        _itemRepo = new FakeDailyMenuItemRepository();
        _historyRepo = new FakeDailyMenuItemHistoryRepository();
        _catalogReader = new FakeCatalogProductPriceReader();
        _recipeValidator = new FakeRecipeVersionValidator();
        _dateProvider = new FakeBusinessDateProvider();

        _service = new DailyMenuService(
            _menuRepo, _itemRepo, _historyRepo, _catalogReader, _recipeValidator, _dateProvider);
    }

    [Fact]
    public async Task CreateDailyMenuDefaultsToConfiguredBusinessDate()
    {
        var menu = await _service.CreateDailyMenuAsync(new CreateDailyMenuCommand(Note: "Sunday Brunch"));

        menu.Should().NotBeNull();
        menu.BusinessDate.Should().Be(new DateOnly(2026, 9, 6));
        menu.Status.Should().Be(DailyMenuStatus.Draft);
        menu.Note.Should().Be("Sunday Brunch");
    }

    [Fact]
    public async Task CreateDailyMenuForDuplicateBusinessDateThrowsDuplicateDailyMenuBusinessDateException()
    {
        await _service.CreateDailyMenuAsync(new CreateDailyMenuCommand(new DateOnly(2026, 9, 6)));

        var act = () => _service.CreateDailyMenuAsync(new CreateDailyMenuCommand(new DateOnly(2026, 9, 6)));
        await act.Should().ThrowAsync<DuplicateDailyMenuBusinessDateException>();
    }

    [Fact]
    public async Task DailyMenuLifecycleTransitionsFromDraftToOpenToClosed()
    {
        var menu = await _service.CreateDailyMenuAsync(new CreateDailyMenuCommand(new DateOnly(2026, 9, 7)));
        menu.Status.Should().Be(DailyMenuStatus.Draft);

        var openTime = DateTimeOffset.UtcNow;
        var opened = await _service.OpenDailyMenuAsync(new OpenDailyMenuCommand(menu.Id, openTime));
        opened.Status.Should().Be(DailyMenuStatus.Open);
        opened.OpenedAt.Should().Be(openTime);

        var staffId = Guid.NewGuid();
        var closeTime = DateTimeOffset.UtcNow.AddHours(8);
        var closed = await _service.CloseDailyMenuAsync(new CloseDailyMenuCommand(menu.Id, staffId, closeTime));
        closed.Status.Should().Be(DailyMenuStatus.Closed);
        closed.ClosedBy.Should().Be(staffId);
        closed.ClosedAt.Should().Be(closeTime);
    }

    [Fact]
    public async Task AddDailyMenuItemInDraftOrOpenMenuPopulatesCatalogSnapshotAndPrice()
    {
        var menu = await _service.CreateDailyMenuAsync(new CreateDailyMenuCommand(new DateOnly(2026, 9, 8)));
        var prodId = Guid.NewGuid();
        _catalogReader.AddProduct(prodId, "Kuzu Tandir", 320m);

        var item = await _service.AddDailyMenuItemAsync(new AddDailyMenuItemCommand(
            DailyMenuId: menu.Id,
            ProductId: prodId,
            PlannedPortions: 50m));

        item.Should().NotBeNull();
        item.ProductNameSnapshot.Should().Be("Kuzu Tandir");
        item.Price.Should().Be(320m);
        item.PlannedPortions.Should().Be(50m);
        item.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task AddDuplicateProductToSameDailyMenuThrowsDuplicateDailyMenuItemException()
    {
        var menu = await _service.CreateDailyMenuAsync(new CreateDailyMenuCommand(new DateOnly(2026, 9, 9)));
        var prodId = Guid.NewGuid();
        _catalogReader.AddProduct(prodId, "Mercimek Corbasi", 85m);

        await _service.AddDailyMenuItemAsync(new AddDailyMenuItemCommand(menu.Id, prodId));

        var act = () => _service.AddDailyMenuItemAsync(new AddDailyMenuItemCommand(menu.Id, prodId));
        await act.Should().ThrowAsync<DuplicateDailyMenuItemException>();
    }

    [Fact]
    public async Task ClosedDailyMenuRejectsAddingNewOperationalItems()
    {
        var menu = await _service.CreateDailyMenuAsync(new CreateDailyMenuCommand(new DateOnly(2026, 9, 10)));
        await _service.OpenDailyMenuAsync(new OpenDailyMenuCommand(menu.Id));
        await _service.CloseDailyMenuAsync(new CloseDailyMenuCommand(menu.Id, Guid.NewGuid()));

        var prodId = Guid.NewGuid();
        _catalogReader.AddProduct(prodId, "Baklava", 150m);

        var act = () => _service.AddDailyMenuItemAsync(new AddDailyMenuItemCommand(menu.Id, prodId));
        await act.Should().ThrowAsync<DailyMenuClosedException>();
    }

    [Fact]
    public async Task ClosedDailyMenuRejectsPriceOrPortionModifications()
    {
        var menu = await _service.CreateDailyMenuAsync(new CreateDailyMenuCommand(new DateOnly(2026, 9, 11)));
        var prodId = Guid.NewGuid();
        _catalogReader.AddProduct(prodId, "Cacik", 60m);
        var item = await _service.AddDailyMenuItemAsync(new AddDailyMenuItemCommand(menu.Id, prodId));

        await _service.OpenDailyMenuAsync(new OpenDailyMenuCommand(menu.Id));
        await _service.CloseDailyMenuAsync(new CloseDailyMenuCommand(menu.Id, Guid.NewGuid()));

        var actPrice = () => _service.UpdateDailyMenuItemPriceAsync(new UpdateDailyMenuItemPriceCommand(item.Id, 75m));
        await actPrice.Should().ThrowAsync<DailyMenuClosedException>();

        var actPortions = () => _service.UpdateDailyMenuItemPlannedPortionsAsync(new UpdateDailyMenuItemPlannedPortionsCommand(item.Id, 30m));
        await actPortions.Should().ThrowAsync<DailyMenuClosedException>();
    }

    [Fact]
    public async Task ModifyingItemPriceAppendsHistoryAuditRecord()
    {
        var menu = await _service.CreateDailyMenuAsync(new CreateDailyMenuCommand(new DateOnly(2026, 9, 12)));
        var prodId = Guid.NewGuid();
        _catalogReader.AddProduct(prodId, "Humus", 100m);
        var item = await _service.AddDailyMenuItemAsync(new AddDailyMenuItemCommand(menu.Id, prodId));

        var staffId = Guid.NewGuid();
        var updated = await _service.UpdateDailyMenuItemPriceAsync(new UpdateDailyMenuItemPriceCommand(item.Id, 115m, staffId));
        updated.Price.Should().Be(115m);

        var history = await _historyRepo.GetByDailyMenuItemIdAsync(item.Id);
        history.Should().HaveCount(1);
        history[0].ChangedBy.Should().Be(staffId);
        history[0].OldValueJson.Should().Contain("100");
        history[0].NewValueJson.Should().Contain("115");
    }

    [Fact]
    public async Task AddingItemWithInactiveRecipeVersionThrowsInvalidDailyMenuOperationException()
    {
        var menu = await _service.CreateDailyMenuAsync(new CreateDailyMenuCommand(new DateOnly(2026, 9, 13)));
        var prodId = Guid.NewGuid();
        var recipeVersionId = Guid.NewGuid();
        _catalogReader.AddProduct(prodId, "Izgara Kofte", 240m);

        // RecipeVersion is not in active set
        var act = () => _service.AddDailyMenuItemAsync(new AddDailyMenuItemCommand(
            menu.Id, prodId, RecipeVersionId: recipeVersionId));

        await act.Should().ThrowAsync<InvalidDailyMenuOperationException>()
            .WithMessage("*not active or does not exist*");
    }

    [Fact]
    public async Task AddingItemWithActiveRecipeVersionSucceeds()
    {
        var menu = await _service.CreateDailyMenuAsync(new CreateDailyMenuCommand(new DateOnly(2026, 9, 14)));
        var prodId = Guid.NewGuid();
        var recipeVersionId = Guid.NewGuid();
        _catalogReader.AddProduct(prodId, "Pide", 200m);
        _recipeValidator.AddActiveVersion(recipeVersionId);

        var item = await _service.AddDailyMenuItemAsync(new AddDailyMenuItemCommand(
            menu.Id, prodId, RecipeVersionId: recipeVersionId));

        item.RecipeVersionId.Should().Be(recipeVersionId);
    }
}
