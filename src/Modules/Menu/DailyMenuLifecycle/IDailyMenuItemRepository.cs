namespace ALKAROS.Menu.DailyMenuLifecycle;

public interface IDailyMenuItemRepository
{
    Task InsertAsync(DailyMenuItem item, CancellationToken ct = default);
    Task<DailyMenuItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<DailyMenuItem?> GetByMenuAndProductAsync(Guid dailyMenuId, Guid productId, CancellationToken ct = default);
    Task<IReadOnlyList<DailyMenuItem>> GetByDailyMenuIdAsync(Guid dailyMenuId, bool activeOnly = false, CancellationToken ct = default);
    Task UpdateAsync(DailyMenuItem item, CancellationToken ct = default);
}
