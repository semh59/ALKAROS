namespace ALKAROS.Menu.StaticMenu;

public interface IStaticMenuService
{
    Task<Menu> CreateMenuAsync(CreateMenuCommand command, CancellationToken ct = default);
    Task<Menu> UpdateMenuAsync(UpdateMenuCommand command, CancellationToken ct = default);
    Task<MenuItem> AddMenuItemAsync(AddMenuItemCommand command, CancellationToken ct = default);
    Task<MenuItem> UpdateMenuItemAsync(UpdateMenuItemCommand command, CancellationToken ct = default);
    Task ReorderMenuItemsAsync(ReorderMenuItemsCommand command, CancellationToken ct = default);
    Task<MenuComposition> GetMenuCompositionAsync(Guid menuId, bool activeOnly = false, CancellationToken ct = default);
    Task<IReadOnlyList<Menu>> GetAllMenusAsync(bool activeOnly = false, CancellationToken ct = default);
}
