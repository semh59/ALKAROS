namespace ALKAROS.Menu.DailyMenuLifecycle;

public interface IDailyMenuItemHistoryRepository
{
    Task InsertAsync(DailyMenuItemHistory history, CancellationToken ct = default);
    Task<IReadOnlyList<DailyMenuItemHistory>> GetByDailyMenuItemIdAsync(Guid dailyMenuItemId, CancellationToken ct = default);
}
