namespace ALKAROS.Menu.StaticMenu;

public interface IMenuItemRepository
{
    Task InsertAsync(MenuItem item, CancellationToken ct = default);
    Task<MenuItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<MenuItem?> GetByMenuAndProductAsync(Guid menuId, Guid productId, CancellationToken ct = default);
    Task<IReadOnlyList<MenuItem>> GetByMenuIdAsync(Guid menuId, bool activeOnly = false, CancellationToken ct = default);
    Task UpdateAsync(MenuItem item, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task ReorderAsync(Guid menuId, IReadOnlyList<Guid> orderedMenuItemIds, CancellationToken ct = default);
}
