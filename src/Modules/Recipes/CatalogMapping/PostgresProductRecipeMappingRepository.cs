using Npgsql;

namespace ALKAROS.Recipes.CatalogMapping;

public sealed class PostgresProductRecipeMappingRepository : IProductRecipeMappingRepository
{
    private const int MaxUnpagedRows = 5000;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresProductRecipeMappingRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task AddOrUpdateAsync(ProductRecipeMapping mapping, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        const string sql = @"
            INSERT INTO recipe.product_recipe_mappings (product_id, recipe_id, is_active, notes, created_at)
            VALUES ($1, $2, $3, $4, $5)
            ON CONFLICT (product_id) DO UPDATE
            SET recipe_id = EXCLUDED.recipe_id,
                is_active = EXCLUDED.is_active,
                notes = EXCLUDED.notes;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(mapping.ProductId);
        cmd.Parameters.AddWithValue(mapping.RecipeId);
        cmd.Parameters.AddWithValue(mapping.IsActive);
        cmd.Parameters.AddWithValue((object?)mapping.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue(mapping.CreatedAt);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ProductRecipeMapping?> GetByProductIdAsync(Guid productId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT product_id, recipe_id, is_active, notes, created_at
            FROM recipe.product_recipe_mappings
            WHERE product_id = $1;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(productId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return Map(reader);
    }

    public async Task<IReadOnlyList<ProductRecipeMapping>> GetByProductIdsAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(productIds);
        if (productIds.Count == 0) return [];

        string sql = $@"
            SELECT product_id, recipe_id, is_active, notes, created_at
            FROM recipe.product_recipe_mappings
            WHERE product_id = ANY($1)
            ORDER BY product_id
            LIMIT {MaxUnpagedRows + 1};";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(productIds.ToArray());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<ProductRecipeMapping>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(Map(reader));
        }

        if (list.Count > MaxUnpagedRows)
        {
            throw new InvalidOperationException(
                $"GetByProductIdsAsync returned more than {MaxUnpagedRows} rows; narrow the filter or paginate.");
        }

        return list;
    }

    public async Task RemoveAsync(Guid productId, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM recipe.product_recipe_mappings WHERE product_id = $1;";
        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(productId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static ProductRecipeMapping Map(NpgsqlDataReader reader)
        => new(
            productId: reader.GetGuid(0),
            recipeId: reader.GetGuid(1),
            isActive: reader.GetBoolean(2),
            notes: reader.IsDBNull(3) ? null : reader.GetString(3),
            createdAt: reader.GetFieldValue<DateTimeOffset>(4));
}
