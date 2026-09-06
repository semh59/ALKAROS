using System.Globalization;
using Npgsql;

namespace ALKAROS.Recipes.Versioning;

public sealed class PostgresRecipeVersionRepository : IRecipeVersionRepository
{
    private const int MaxUnpagedRows = 5000;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresRecipeVersionRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task AddAsync(RecipeVersion version, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(version);

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        const string versionSql = @"
            INSERT INTO recipe.recipe_versions (
                id, recipe_id, version_number, status, effective_from, effective_to,
                yield_quantity, yield_unit_code, preparation_minutes, instructions,
                is_locked, created_at, activated_at, row_version
            ) VALUES (
                $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14
            );";

        await using (var cmd = new NpgsqlCommand(versionSql, conn, tx))
        {
            cmd.Parameters.AddWithValue(version.Id);
            cmd.Parameters.AddWithValue(version.RecipeId);
            cmd.Parameters.AddWithValue(version.VersionNumber);
            cmd.Parameters.AddWithValue(version.Status.ToString());
            cmd.Parameters.AddWithValue((object?)version.EffectiveFrom ?? DBNull.Value);
            cmd.Parameters.AddWithValue((object?)version.EffectiveTo ?? DBNull.Value);
            cmd.Parameters.AddWithValue(version.YieldQuantity);
            cmd.Parameters.AddWithValue(version.YieldUnitCode);
            cmd.Parameters.AddWithValue(version.PreparationMinutes);
            cmd.Parameters.AddWithValue((object?)version.Instructions ?? DBNull.Value);
            cmd.Parameters.AddWithValue(version.IsLocked);
            cmd.Parameters.AddWithValue(version.CreatedAt);
            cmd.Parameters.AddWithValue((object?)version.ActivatedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue(version.RowVersion);

            await cmd.ExecuteNonQueryAsync(ct);
        }

        const string ingredientSql = @"
            INSERT INTO recipe.recipe_ingredients (
                id, recipe_version_id, ingredient_item_id, quantity, unit_code,
                loss_percentage, sort_order, notes, created_at
            ) VALUES (
                $1, $2, $3, $4, $5, $6, $7, $8, $9
            );";

        foreach (var ingredient in version.Ingredients)
        {
            await using var ingCmd = new NpgsqlCommand(ingredientSql, conn, tx);
            ingCmd.Parameters.AddWithValue(ingredient.Id);
            ingCmd.Parameters.AddWithValue(version.Id);
            ingCmd.Parameters.AddWithValue(ingredient.IngredientItemId);
            ingCmd.Parameters.AddWithValue(ingredient.Quantity);
            ingCmd.Parameters.AddWithValue(ingredient.UnitCode);
            ingCmd.Parameters.AddWithValue(ingredient.LossPercentage);
            ingCmd.Parameters.AddWithValue(ingredient.SortOrder);
            ingCmd.Parameters.AddWithValue((object?)ingredient.Notes ?? DBNull.Value);
            ingCmd.Parameters.AddWithValue(ingredient.CreatedAt);

            await ingCmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task<RecipeVersion?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string versionSql = @"
            SELECT id, recipe_id, version_number, status, effective_from, effective_to,
                   yield_quantity, yield_unit_code, preparation_minutes, instructions,
                   is_locked, created_at, activated_at, row_version
            FROM recipe.recipe_versions
            WHERE id = $1;";

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(versionSql, conn);
        cmd.Parameters.AddWithValue(id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        var partialVersion = MapVersionRow(reader);
        await reader.CloseAsync();

        var ingredients = await LoadIngredientsAsync(conn, id, ct);
        return new RecipeVersion(
            id: partialVersion.Id,
            recipeId: partialVersion.RecipeId,
            versionNumber: partialVersion.VersionNumber,
            status: partialVersion.Status,
            yieldQuantity: partialVersion.YieldQuantity,
            yieldUnitCode: partialVersion.YieldUnitCode,
            preparationMinutes: partialVersion.PreparationMinutes,
            instructions: partialVersion.Instructions,
            isLocked: partialVersion.IsLocked,
            effectiveFrom: partialVersion.EffectiveFrom,
            effectiveTo: partialVersion.EffectiveTo,
            createdAt: partialVersion.CreatedAt,
            activatedAt: partialVersion.ActivatedAt,
            rowVersion: partialVersion.RowVersion,
            ingredients: ingredients);
    }

    public async Task<RecipeVersion?> GetByRecipeAndVersionAsync(Guid recipeId, int versionNumber, CancellationToken ct = default)
    {
        const string sql = "SELECT id FROM recipe.recipe_versions WHERE recipe_id = $1 AND version_number = $2;";
        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(recipeId);
        cmd.Parameters.AddWithValue(versionNumber);

        var idObj = await cmd.ExecuteScalarAsync(ct);
        if (idObj is null or DBNull)
            return null;

        return await GetByIdAsync((Guid)idObj, ct);
    }

    public async Task<RecipeVersion?> GetActiveVersionAsync(Guid recipeId, CancellationToken ct = default)
    {
        const string sql = "SELECT id FROM recipe.recipe_versions WHERE recipe_id = $1 AND status = 'Active';";
        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(recipeId);

        var idObj = await cmd.ExecuteScalarAsync(ct);
        if (idObj is null or DBNull)
            return null;

        return await GetByIdAsync((Guid)idObj, ct);
    }

    public async Task<IReadOnlyList<RecipeVersion>> GetAllVersionsAsync(Guid recipeId, CancellationToken ct = default)
    {
        string sql = $@"
            SELECT id
            FROM recipe.recipe_versions
            WHERE recipe_id = $1
            ORDER BY version_number ASC
            LIMIT {MaxUnpagedRows + 1};";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(recipeId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var ids = new List<Guid>();
        while (await reader.ReadAsync(ct))
        {
            ids.Add(reader.GetGuid(0));
        }
        await reader.CloseAsync();

        if (ids.Count > MaxUnpagedRows)
        {
            throw new InvalidOperationException(
                $"GetAllVersionsAsync returned more than {MaxUnpagedRows} rows; narrow the filter or paginate.");
        }

        var list = new List<RecipeVersion>();
        foreach (var id in ids)
        {
            var v = await GetByIdAsync(id, ct);
            if (v != null) list.Add(v);
        }

        return list;
    }

    public async Task<int> GetMaxVersionNumberAsync(Guid recipeId, CancellationToken ct = default)
    {
        const string sql = "SELECT COALESCE(MAX(version_number), 0) FROM recipe.recipe_versions WHERE recipe_id = $1;";
        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(recipeId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    public async Task UpdateAsync(RecipeVersion version, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(version);

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        const string updateSql = @"
            UPDATE recipe.recipe_versions
            SET yield_quantity = $2,
                yield_unit_code = $3,
                preparation_minutes = $4,
                instructions = $5,
                is_locked = $6,
                status = $7,
                row_version = row_version + 1
            WHERE id = $1 AND row_version = $8
            RETURNING row_version;";

        await using (var cmd = new NpgsqlCommand(updateSql, conn, tx))
        {
            cmd.Parameters.AddWithValue(version.Id);
            cmd.Parameters.AddWithValue(version.YieldQuantity);
            cmd.Parameters.AddWithValue(version.YieldUnitCode);
            cmd.Parameters.AddWithValue(version.PreparationMinutes);
            cmd.Parameters.AddWithValue((object?)version.Instructions ?? DBNull.Value);
            cmd.Parameters.AddWithValue(version.IsLocked);
            cmd.Parameters.AddWithValue(version.Status.ToString());
            cmd.Parameters.AddWithValue(version.RowVersion);

            var newRowVersion = await cmd.ExecuteScalarAsync(ct);
            if (newRowVersion is null or DBNull)
            {
                throw new RecipeVersionConflictException(
                    $"Optimistic concurrency violation: RecipeVersion {version.Id} (v{version.VersionNumber}) was modified concurrently.");
            }

            version.RowVersion = Convert.ToInt32(newRowVersion, CultureInfo.InvariantCulture);
        }

        // Replace ingredients
        const string deleteIngredientsSql = "DELETE FROM recipe.recipe_ingredients WHERE recipe_version_id = $1;";
        await using (var delCmd = new NpgsqlCommand(deleteIngredientsSql, conn, tx))
        {
            delCmd.Parameters.AddWithValue(version.Id);
            await delCmd.ExecuteNonQueryAsync(ct);
        }

        const string insertIngredientSql = @"
            INSERT INTO recipe.recipe_ingredients (
                id, recipe_version_id, ingredient_item_id, quantity, unit_code,
                loss_percentage, sort_order, notes, created_at
            ) VALUES (
                $1, $2, $3, $4, $5, $6, $7, $8, $9
            );";

        foreach (var ingredient in version.Ingredients)
        {
            await using var ingCmd = new NpgsqlCommand(insertIngredientSql, conn, tx);
            ingCmd.Parameters.AddWithValue(ingredient.Id);
            ingCmd.Parameters.AddWithValue(version.Id);
            ingCmd.Parameters.AddWithValue(ingredient.IngredientItemId);
            ingCmd.Parameters.AddWithValue(ingredient.Quantity);
            ingCmd.Parameters.AddWithValue(ingredient.UnitCode);
            ingCmd.Parameters.AddWithValue(ingredient.LossPercentage);
            ingCmd.Parameters.AddWithValue(ingredient.SortOrder);
            ingCmd.Parameters.AddWithValue((object?)ingredient.Notes ?? DBNull.Value);
            ingCmd.Parameters.AddWithValue(ingredient.CreatedAt);

            await ingCmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task ActivateVersionAsync(Guid recipeId, int versionNumber, DateTimeOffset activatedAt, CancellationToken ct = default)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // Check target version
        const string targetSql = @"
            SELECT id, status, yield_quantity
            FROM recipe.recipe_versions
            WHERE recipe_id = $1 AND version_number = $2
            FOR UPDATE;";

        Guid targetId;
        string targetStatus;
        decimal yieldQty;
        await using (var cmd = new NpgsqlCommand(targetSql, conn, tx))
        {
            cmd.Parameters.AddWithValue(recipeId);
            cmd.Parameters.AddWithValue(versionNumber);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                throw new InvalidRecipeVersionException($"Recipe version {versionNumber} for recipe {recipeId} does not exist.");
            }
            targetId = reader.GetGuid(0);
            targetStatus = reader.GetString(1);
            yieldQty = reader.GetDecimal(2);
        }

        if (targetStatus == "Active")
        {
            await tx.CommitAsync(ct);
            return;
        }

        if (targetStatus != "Draft")
        {
            throw new InvalidRecipeVersionException(
                $"Cannot activate version {versionNumber} because status is '{targetStatus}'. Only Draft versions can be activated.");
        }

        // Verify ingredient count
        const string countSql = "SELECT COUNT(*) FROM recipe.recipe_ingredients WHERE recipe_version_id = $1;";
        await using (var countCmd = new NpgsqlCommand(countSql, conn, tx))
        {
            countCmd.Parameters.AddWithValue(targetId);
            var count = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
            if (count == 0)
            {
                throw new InvalidRecipeVersionException(
                    $"Cannot activate recipe version {versionNumber} because it has zero ingredients.");
            }
        }

        // Archive current active version if any
        const string archiveSql = @"
            UPDATE recipe.recipe_versions
            SET status = 'Archived',
                effective_to = $2,
                is_locked = true,
                row_version = row_version + 1
            WHERE recipe_id = $1 AND status = 'Active';";

        await using (var archiveCmd = new NpgsqlCommand(archiveSql, conn, tx))
        {
            archiveCmd.Parameters.AddWithValue(recipeId);
            archiveCmd.Parameters.AddWithValue(activatedAt);
            await archiveCmd.ExecuteNonQueryAsync(ct);
        }

        // Activate target version
        const string activateSql = @"
            UPDATE recipe.recipe_versions
            SET status = 'Active',
                effective_from = $2,
                activated_at = $2,
                is_locked = true,
                row_version = row_version + 1
            WHERE id = $1;";

        await using (var actCmd = new NpgsqlCommand(activateSql, conn, tx))
        {
            actCmd.Parameters.AddWithValue(targetId);
            actCmd.Parameters.AddWithValue(activatedAt);
            await actCmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task LockVersionAsync(Guid versionId, CancellationToken ct = default)
    {
        const string sql = "UPDATE recipe.recipe_versions SET is_locked = true WHERE id = $1;";
        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(versionId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<List<RecipeIngredientItem>> LoadIngredientsAsync(
        NpgsqlConnection conn, Guid versionId, CancellationToken ct)
    {
        const string sql = @"
            SELECT id, recipe_version_id, ingredient_item_id, quantity, unit_code,
                   loss_percentage, sort_order, notes, created_at
            FROM recipe.recipe_ingredients
            WHERE recipe_version_id = $1
            ORDER BY sort_order ASC, created_at ASC;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(versionId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<RecipeIngredientItem>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new RecipeIngredientItem(
                id: reader.GetGuid(0),
                recipeVersionId: reader.GetGuid(1),
                ingredientItemId: reader.GetGuid(2),
                quantity: reader.GetDecimal(3),
                unitCode: reader.GetString(4),
                lossPercentage: reader.GetDecimal(5),
                sortOrder: reader.GetInt32(6),
                notes: reader.IsDBNull(7) ? null : reader.GetString(7),
                createdAt: reader.GetFieldValue<DateTimeOffset>(8)));
        }

        return list;
    }

    private static RecipeVersion MapVersionRow(NpgsqlDataReader reader)
    {
        return new RecipeVersion(
            id: reader.GetGuid(0),
            recipeId: reader.GetGuid(1),
            versionNumber: reader.GetInt32(2),
            status: Enum.Parse<RecipeVersionStatus>(reader.GetString(3), ignoreCase: true),
            yieldQuantity: reader.GetDecimal(6),
            yieldUnitCode: reader.GetString(7),
            preparationMinutes: reader.GetInt32(8),
            instructions: reader.IsDBNull(9) ? null : reader.GetString(9),
            isLocked: reader.GetBoolean(10),
            effectiveFrom: reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
            effectiveTo: reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
            createdAt: reader.GetFieldValue<DateTimeOffset>(11),
            activatedAt: reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12),
            rowVersion: reader.GetInt32(13));
    }
}
