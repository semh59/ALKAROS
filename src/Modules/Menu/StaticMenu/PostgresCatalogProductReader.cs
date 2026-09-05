using Npgsql;

namespace ALKAROS.Menu.StaticMenu;

public sealed class PostgresCatalogProductReader : ICatalogProductReader
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresCatalogProductReader(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<CatalogProductInfo?> GetProductAsync(Guid productId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT product_id, name, active
            FROM catalog.products
            WHERE product_id = $1;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(productId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new CatalogProductInfo(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetBoolean(2));
    }

    public async Task<IReadOnlyDictionary<Guid, CatalogProductInfo>> GetProductsAsync(IEnumerable<Guid> productIds, CancellationToken ct = default)
    {
        var idList = productIds.Distinct().ToList();
        var result = new Dictionary<Guid, CatalogProductInfo>();
        if (idList.Count == 0)
            return result;

        const string sql = @"
            SELECT product_id, name, active
            FROM catalog.products
            WHERE product_id = ANY($1);";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(idList.ToArray());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetGuid(0);
            var name = reader.GetString(1);
            var active = reader.GetBoolean(2);
            result[id] = new CatalogProductInfo(id, name, active);
        }

        return result;
    }
}
