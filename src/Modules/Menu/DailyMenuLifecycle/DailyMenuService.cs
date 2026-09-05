using System.Text.Json;

namespace ALKAROS.Menu.DailyMenuLifecycle;

public sealed class DailyMenuService : IDailyMenuService
{
    private readonly IDailyMenuRepository _menuRepo;
    private readonly IDailyMenuItemRepository _itemRepo;
    private readonly IDailyMenuItemHistoryRepository _historyRepo;
    private readonly ICatalogProductPriceReader _catalogReader;
    private readonly IRecipeVersionValidator _recipeValidator;
    private readonly IBusinessDateProvider _businessDateProvider;

    public DailyMenuService(
        IDailyMenuRepository menuRepo,
        IDailyMenuItemRepository itemRepo,
        IDailyMenuItemHistoryRepository historyRepo,
        ICatalogProductPriceReader catalogReader,
        IRecipeVersionValidator recipeValidator,
        IBusinessDateProvider businessDateProvider)
    {
        _menuRepo = menuRepo ?? throw new ArgumentNullException(nameof(menuRepo));
        _itemRepo = itemRepo ?? throw new ArgumentNullException(nameof(itemRepo));
        _historyRepo = historyRepo ?? throw new ArgumentNullException(nameof(historyRepo));
        _catalogReader = catalogReader ?? throw new ArgumentNullException(nameof(catalogReader));
        _recipeValidator = recipeValidator ?? throw new ArgumentNullException(nameof(recipeValidator));
        _businessDateProvider = businessDateProvider ?? throw new ArgumentNullException(nameof(businessDateProvider));
    }

    public async Task<DailyMenu> CreateDailyMenuAsync(CreateDailyMenuCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var businessDate = command.BusinessDate ?? _businessDateProvider.GetCurrentBusinessDate();

        var existing = await _menuRepo.GetByBusinessDateAsync(businessDate, ct);
        if (existing != null)
            throw new DuplicateDailyMenuBusinessDateException(businessDate);

        var menu = DailyMenu.Create(businessDate, command.Note);
        await _menuRepo.InsertAsync(menu, ct);
        return menu;
    }

    public async Task<DailyMenu> OpenDailyMenuAsync(OpenDailyMenuCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.DailyMenuId == Guid.Empty)
            throw new InvalidDailyMenuOperationException("DailyMenuId cannot be empty.");

        var menu = await _menuRepo.GetByIdAsync(command.DailyMenuId, ct)
            ?? throw new DailyMenuNotFoundException(command.DailyMenuId);

        menu.Open(command.OpenedAt ?? DateTimeOffset.UtcNow);
        await _menuRepo.UpdateAsync(menu, ct);
        return menu;
    }

    public async Task<DailyMenu> CloseDailyMenuAsync(CloseDailyMenuCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.DailyMenuId == Guid.Empty)
            throw new InvalidDailyMenuOperationException("DailyMenuId cannot be empty.");

        if (command.ClosedBy == Guid.Empty)
            throw new InvalidDailyMenuOperationException("ClosedBy staff ID cannot be empty.");

        var menu = await _menuRepo.GetByIdAsync(command.DailyMenuId, ct)
            ?? throw new DailyMenuNotFoundException(command.DailyMenuId);

        menu.Close(command.ClosedBy, command.ClosedAt ?? DateTimeOffset.UtcNow);
        await _menuRepo.UpdateAsync(menu, ct);
        return menu;
    }

    public async Task<DailyMenuItem> AddDailyMenuItemAsync(AddDailyMenuItemCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.DailyMenuId == Guid.Empty)
            throw new InvalidDailyMenuOperationException("DailyMenuId cannot be empty.");

        if (command.ProductId == Guid.Empty)
            throw new InvalidDailyMenuOperationException("ProductId cannot be empty.");

        if (command.PlannedPortions < 0m)
            throw new InvalidDailyMenuOperationException("PlannedPortions cannot be negative.");

        var menu = await _menuRepo.GetByIdAsync(command.DailyMenuId, ct)
            ?? throw new DailyMenuNotFoundException(command.DailyMenuId);

        menu.AssertModifiable();

        var existing = await _itemRepo.GetByMenuAndProductAsync(command.DailyMenuId, command.ProductId, ct);
        if (existing != null)
            throw new DuplicateDailyMenuItemException(command.DailyMenuId, command.ProductId);

        var product = await _catalogReader.GetProductPriceInfoAsync(command.ProductId, ct)
            ?? throw new InvalidDailyMenuOperationException($"Product '{command.ProductId}' does not exist in catalog.");

        var resolvedPrice = command.Price ?? product.CurrentPrice
            ?? throw new InvalidDailyMenuOperationException($"No price specified for product '{command.ProductId}' and catalog has no current price.");

        if (resolvedPrice < 0m)
            throw new InvalidDailyMenuOperationException("Price cannot be negative.");

        if (command.RecipeVersionId.HasValue && command.RecipeVersionId.Value != Guid.Empty)
        {
            var isRecipeActive = await _recipeValidator.IsRecipeVersionActiveAsync(command.RecipeVersionId.Value, ct);
            if (!isRecipeActive)
                throw new InvalidDailyMenuOperationException($"Recipe version '{command.RecipeVersionId.Value}' is not active or does not exist.");
        }

        var item = DailyMenuItem.Create(
            dailyMenuId: command.DailyMenuId,
            productId: command.ProductId,
            productNameSnapshot: product.Name,
            price: resolvedPrice,
            recipeVersionId: command.RecipeVersionId,
            plannedPortions: command.PlannedPortions,
            printerRoutePolicy: command.PrinterRoutePolicy);

        await _itemRepo.InsertAsync(item, ct);
        return item;
    }

    public async Task<DailyMenuItem> UpdateDailyMenuItemPriceAsync(UpdateDailyMenuItemPriceCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.DailyMenuItemId == Guid.Empty)
            throw new InvalidDailyMenuOperationException("DailyMenuItemId cannot be empty.");

        if (command.NewPrice < 0m)
            throw new InvalidDailyMenuOperationException("NewPrice cannot be negative.");

        var item = await _itemRepo.GetByIdAsync(command.DailyMenuItemId, ct)
            ?? throw new DailyMenuItemNotFoundException(command.DailyMenuItemId);

        var menu = await _menuRepo.GetByIdAsync(item.DailyMenuId, ct)
            ?? throw new DailyMenuNotFoundException(item.DailyMenuId);

        menu.AssertModifiable();

        var oldPrice = item.Price;
        item.UpdatePrice(command.NewPrice);
        await _itemRepo.UpdateAsync(item, ct);

        var history = new DailyMenuItemHistory(
            Id: Guid.NewGuid(),
            DailyMenuItemId: item.Id,
            OldValueJson: JsonSerializer.Serialize(new { Price = oldPrice }),
            NewValueJson: JsonSerializer.Serialize(new { Price = item.Price }),
            ChangedBy: command.ChangedBy,
            ChangedAt: DateTimeOffset.UtcNow);

        await _historyRepo.InsertAsync(history, ct);
        return item;
    }

    public async Task<DailyMenuItem> UpdateDailyMenuItemPlannedPortionsAsync(UpdateDailyMenuItemPlannedPortionsCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.DailyMenuItemId == Guid.Empty)
            throw new InvalidDailyMenuOperationException("DailyMenuItemId cannot be empty.");

        if (command.NewPlannedPortions < 0m)
            throw new InvalidDailyMenuOperationException("NewPlannedPortions cannot be negative.");

        var item = await _itemRepo.GetByIdAsync(command.DailyMenuItemId, ct)
            ?? throw new DailyMenuItemNotFoundException(command.DailyMenuItemId);

        var menu = await _menuRepo.GetByIdAsync(item.DailyMenuId, ct)
            ?? throw new DailyMenuNotFoundException(item.DailyMenuId);

        menu.AssertModifiable();

        var oldPortions = item.PlannedPortions;
        item.UpdatePlannedPortions(command.NewPlannedPortions);
        await _itemRepo.UpdateAsync(item, ct);

        var history = new DailyMenuItemHistory(
            Id: Guid.NewGuid(),
            DailyMenuItemId: item.Id,
            OldValueJson: JsonSerializer.Serialize(new { PlannedPortions = oldPortions }),
            NewValueJson: JsonSerializer.Serialize(new { PlannedPortions = item.PlannedPortions }),
            ChangedBy: command.ChangedBy,
            ChangedAt: DateTimeOffset.UtcNow);

        await _historyRepo.InsertAsync(history, ct);
        return item;
    }

    public async Task<DailyMenuItem> UpdateDailyMenuItemStatusAsync(UpdateDailyMenuItemStatusCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.DailyMenuItemId == Guid.Empty)
            throw new InvalidDailyMenuOperationException("DailyMenuItemId cannot be empty.");

        var item = await _itemRepo.GetByIdAsync(command.DailyMenuItemId, ct)
            ?? throw new DailyMenuItemNotFoundException(command.DailyMenuItemId);

        var menu = await _menuRepo.GetByIdAsync(item.DailyMenuId, ct)
            ?? throw new DailyMenuNotFoundException(item.DailyMenuId);

        menu.AssertModifiable();

        var oldStatus = item.IsActive;
        item.UpdateStatus(command.IsActive);
        await _itemRepo.UpdateAsync(item, ct);

        var history = new DailyMenuItemHistory(
            Id: Guid.NewGuid(),
            DailyMenuItemId: item.Id,
            OldValueJson: JsonSerializer.Serialize(new { IsActive = oldStatus }),
            NewValueJson: JsonSerializer.Serialize(new { IsActive = item.IsActive }),
            ChangedBy: command.ChangedBy,
            ChangedAt: DateTimeOffset.UtcNow);

        await _historyRepo.InsertAsync(history, ct);
        return item;
    }

    public async Task<DailyMenuDetails> GetDailyMenuDetailsAsync(Guid dailyMenuId, bool activeOnly = false, CancellationToken ct = default)
    {
        if (dailyMenuId == Guid.Empty)
            throw new InvalidDailyMenuOperationException("DailyMenuId cannot be empty.");

        var menu = await _menuRepo.GetByIdAsync(dailyMenuId, ct)
            ?? throw new DailyMenuNotFoundException(dailyMenuId);

        var items = await _itemRepo.GetByDailyMenuIdAsync(dailyMenuId, activeOnly, ct);
        return new DailyMenuDetails(menu, items);
    }

    public async Task<DailyMenuDetails?> GetDailyMenuByDateAsync(DateOnly businessDate, bool activeOnly = false, CancellationToken ct = default)
    {
        var menu = await _menuRepo.GetByBusinessDateAsync(businessDate, ct);
        if (menu == null)
            return null;

        var items = await _itemRepo.GetByDailyMenuIdAsync(menu.Id, activeOnly, ct);
        return new DailyMenuDetails(menu, items);
    }
}
