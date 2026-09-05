namespace ALKAROS.Menu.DailyMenuLifecycle;

public interface IDailyMenuRepository
{
    Task InsertAsync(DailyMenu menu, CancellationToken ct = default);
    Task<DailyMenu?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<DailyMenu?> GetByBusinessDateAsync(DateOnly businessDate, CancellationToken ct = default);
    Task UpdateAsync(DailyMenu menu, CancellationToken ct = default);
    Task<IReadOnlyList<DailyMenu>> GetAllAsync(CancellationToken ct = default);
}
