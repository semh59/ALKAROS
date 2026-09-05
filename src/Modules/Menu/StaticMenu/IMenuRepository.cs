namespace ALKAROS.Menu.StaticMenu;

public interface IMenuRepository
{
    Task InsertAsync(Menu menu, CancellationToken ct = default);
    Task<Menu?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Menu?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task UpdateAsync(Menu menu, CancellationToken ct = default);
    Task<IReadOnlyList<Menu>> GetAllAsync(bool activeOnly = false, CancellationToken ct = default);
}
