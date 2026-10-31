using Npgsql;

namespace ALKAROS.Menu.DailyMenuLifecycle;

public sealed class PostgresDailyMenuItemLocator : IDailyMenuItemLocator
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IBusinessDateProvider _businessDate;

    public PostgresDailyMenuItemLocator(NpgsqlDataSource dataSource, IBusinessDateProvider businessDate)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _businessDate = businessDate ?? throw new ArgumentNullException(nameof(businessDate));
    }

    public async Task<IReadOnlyDictionary<Guid, Guid>> FindPublishedItemIdsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(productIds);
        if (productIds.Count == 0)
            return new Dictionary<Guid, Guid>();

        const string sql = """
            SELECT dmi.product_id, dmi.daily_menu_item_id
            FROM menu.daily_menu_items dmi
            JOIN menu.daily_menus dm ON dm.daily_menu_id = dmi.daily_menu_id
            WHERE dm.business_date = $1
              AND dm.status = 'Open'
              AND dmi.active
              AND dmi.product_id = ANY($2);
            """;

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(_businessDate.GetCurrentBusinessDate());
        cmd.Parameters.AddWithValue(productIds.ToArray());

        var result = new Dictionary<Guid, Guid>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result[reader.GetGuid(0)] = reader.GetGuid(1);
        return result;
    }
}
