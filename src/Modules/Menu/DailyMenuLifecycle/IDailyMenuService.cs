namespace ALKAROS.Menu.DailyMenuLifecycle;

public interface IDailyMenuService
{
    Task<DailyMenu> CreateDailyMenuAsync(CreateDailyMenuCommand command, CancellationToken ct = default);
    Task<DailyMenu> OpenDailyMenuAsync(OpenDailyMenuCommand command, CancellationToken ct = default);
    Task<DailyMenu> CloseDailyMenuAsync(CloseDailyMenuCommand command, CancellationToken ct = default);
    Task<DailyMenuItem> AddDailyMenuItemAsync(AddDailyMenuItemCommand command, CancellationToken ct = default);
    Task<DailyMenuItem> UpdateDailyMenuItemPriceAsync(UpdateDailyMenuItemPriceCommand command, CancellationToken ct = default);
    Task<DailyMenuItem> UpdateDailyMenuItemPlannedPortionsAsync(UpdateDailyMenuItemPlannedPortionsCommand command, CancellationToken ct = default);
    Task<DailyMenuItem> UpdateDailyMenuItemStatusAsync(UpdateDailyMenuItemStatusCommand command, CancellationToken ct = default);
    Task<DailyMenuDetails> GetDailyMenuDetailsAsync(Guid dailyMenuId, bool activeOnly = false, CancellationToken ct = default);
    Task<DailyMenuDetails?> GetDailyMenuByDateAsync(DateOnly businessDate, bool activeOnly = false, CancellationToken ct = default);
}
