using System.Globalization;
using Npgsql;

namespace ALKAROS.Recipes.Versioning;

public sealed class PostgresRecipeRepository : IRecipeRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresRecipeRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task AddAsync(Recipe recipe, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        const string sql = @"
            INSERT INTO recipe.recipes (id, code, name, description, created_at, row_version)
            VALUES ($1, $2, $3, $4, $5, $6);";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(recipe.Id);
        cmd.Parameters.AddWithValue(recipe.Code);
        cmd.Parameters.AddWithValue(recipe.Name);
        cmd.Parameters.AddWithValue((object?)recipe.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue(recipe.CreatedAt);
        cmd.Parameters.AddWithValue(recipe.RowVersion);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<Recipe?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, code, name, description, created_at, row_version
            FROM recipe.recipes
            WHERE id = $1;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return MapRecipe(reader);
    }

    public async Task<Recipe?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        const string sql = @"
            SELECT id, code, name, description, created_at, row_version
            FROM recipe.recipes
            WHERE code = $1;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(code.Trim().ToUpperInvariant());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return MapRecipe(reader);
    }

    public async Task<IReadOnlyList<Recipe>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, code, name, description, created_at, row_version
            FROM recipe.recipes
            ORDER BY code ASC;";

        await using var cmd = _dataSource.CreateCommand(sql);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var list = new List<Recipe>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapRecipe(reader));
        }

        return list;
    }

    public async Task UpdateAsync(Recipe recipe, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        const string sql = @"
            UPDATE recipe.recipes
            SET name = $2,
                description = $3,
                row_version = row_version + 1
            WHERE id = $1 AND row_version = $4
            RETURNING row_version;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(recipe.Id);
        cmd.Parameters.AddWithValue(recipe.Name);
        cmd.Parameters.AddWithValue((object?)recipe.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue(recipe.RowVersion);

        var newVersion = await cmd.ExecuteScalarAsync(ct);
        if (newVersion is null or DBNull)
        {
            throw new RecipeVersionConflictException(
                $"Optimistic concurrency violation: Recipe {recipe.Id} was modified by another transaction.");
        }

        recipe.RowVersion = Convert.ToInt32(newVersion, CultureInfo.InvariantCulture);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"DELETE FROM recipe.recipes WHERE id = $1;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static Recipe MapRecipe(NpgsqlDataReader reader)
    {
        return new Recipe(
            id: reader.GetGuid(0),
            code: reader.GetString(1),
            name: reader.GetString(2),
            description: reader.IsDBNull(3) ? null : reader.GetString(3),
            createdAt: reader.GetFieldValue<DateTimeOffset>(4),
            rowVersion: reader.GetInt32(5));
    }
}
