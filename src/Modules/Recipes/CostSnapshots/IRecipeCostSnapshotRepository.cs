using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace ALKAROS.Recipes.CostSnapshots;

public interface IRecipeCostSnapshotRepository
{
    Task<RecipeCostSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<RecipeCostSnapshot?> GetByVersionAndDateAsync(Guid recipeVersionId, DateOnly costBasisDate, CancellationToken ct = default);
    Task<RecipeCostSnapshot?> GetEffectiveSnapshotAsync(Guid recipeVersionId, DateOnly asOfDate, CancellationToken ct = default);
    Task<IReadOnlyList<RecipeCostSnapshot>> ListByVersionAsync(Guid recipeVersionId, CancellationToken ct = default);
    Task SaveAsync(RecipeCostSnapshot snapshot, CancellationToken ct = default);
}

public sealed class PostgresRecipeCostSnapshotRepository : IRecipeCostSnapshotRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresRecipeCostSnapshotRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<RecipeCostSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
SELECT snapshot_id, recipe_version_id, cost_basis_date, calculated_cost, currency, created_at
FROM recipe.recipe_cost_snapshots
WHERE snapshot_id = $1;";

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        var header = MapHeader(reader);
        await reader.CloseAsync();

        var items = await LoadItemsAsync(conn, header.Id, ct);
        return new RecipeCostSnapshot(
            header.Id,
            header.RecipeVersionId,
            header.CostBasisDate,
            header.CalculatedCost,
            header.Currency,
            header.CreatedAt,
            items);
    }

    public async Task<RecipeCostSnapshot?> GetByVersionAndDateAsync(Guid recipeVersionId, DateOnly costBasisDate, CancellationToken ct = default)
    {
        const string sql = @"
SELECT snapshot_id, recipe_version_id, cost_basis_date, calculated_cost, currency, created_at
FROM recipe.recipe_cost_snapshots
WHERE recipe_version_id = $1 AND cost_basis_date = $2;";

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(recipeVersionId);
        cmd.Parameters.AddWithValue(costBasisDate);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        var header = MapHeader(reader);
        await reader.CloseAsync();

        var items = await LoadItemsAsync(conn, header.Id, ct);
        return new RecipeCostSnapshot(
            header.Id,
            header.RecipeVersionId,
            header.CostBasisDate,
            header.CalculatedCost,
            header.Currency,
            header.CreatedAt,
            items);
    }

    public async Task<RecipeCostSnapshot?> GetEffectiveSnapshotAsync(Guid recipeVersionId, DateOnly asOfDate, CancellationToken ct = default)
    {
        const string sql = @"
SELECT snapshot_id, recipe_version_id, cost_basis_date, calculated_cost, currency, created_at
FROM recipe.recipe_cost_snapshots
WHERE recipe_version_id = $1 AND cost_basis_date <= $2
ORDER BY cost_basis_date DESC
LIMIT 1;";

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(recipeVersionId);
        cmd.Parameters.AddWithValue(asOfDate);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        var header = MapHeader(reader);
        await reader.CloseAsync();

        var items = await LoadItemsAsync(conn, header.Id, ct);
        return new RecipeCostSnapshot(
            header.Id,
            header.RecipeVersionId,
            header.CostBasisDate,
            header.CalculatedCost,
            header.Currency,
            header.CreatedAt,
            items);
    }

    public async Task<IReadOnlyList<RecipeCostSnapshot>> ListByVersionAsync(Guid recipeVersionId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT snapshot_id, recipe_version_id, cost_basis_date, calculated_cost, currency, created_at
FROM recipe.recipe_cost_snapshots
WHERE recipe_version_id = $1
ORDER BY cost_basis_date DESC;";

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(recipeVersionId);

        var list = new List<RecipeCostSnapshot>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapHeader(reader));
        }
        await reader.CloseAsync();

        var result = new List<RecipeCostSnapshot>();
        foreach (var h in list)
        {
            var items = await LoadItemsAsync(conn, h.Id, ct);
            result.Add(new RecipeCostSnapshot(
                h.Id,
                h.RecipeVersionId,
                h.CostBasisDate,
                h.CalculatedCost,
                h.Currency,
                h.CreatedAt,
                items));
        }

        return result;
    }

    public async Task SaveAsync(RecipeCostSnapshot snapshot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        const string headerSql = @"
INSERT INTO recipe.recipe_cost_snapshots (snapshot_id, recipe_version_id, cost_basis_date, calculated_cost, currency, created_at)
VALUES ($1, $2, $3, $4, $5, $6);";

        await using var cmd = new NpgsqlCommand(headerSql, conn, tx);
        cmd.Parameters.AddWithValue(snapshot.Id);
        cmd.Parameters.AddWithValue(snapshot.RecipeVersionId);
        cmd.Parameters.AddWithValue(snapshot.CostBasisDate);
        cmd.Parameters.AddWithValue(snapshot.CalculatedCost);
        cmd.Parameters.AddWithValue(snapshot.Currency);
        cmd.Parameters.AddWithValue(snapshot.CreatedAt);

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            throw new DuplicateCostSnapshotException(snapshot.RecipeVersionId, snapshot.CostBasisDate);
        }

        foreach (var item in snapshot.Items)
        {
            const string itemSql = @"
INSERT INTO recipe.recipe_cost_snapshot_items (
    snapshot_item_id, snapshot_id, stock_item_id, raw_quantity, waste_factor,
    effective_native_quantity, native_unit_code, stock_quantity, stock_unit_code,
    unit_cost, line_cost, created_at
) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12);";

            await using var itemCmd = new NpgsqlCommand(itemSql, conn, tx);
            itemCmd.Parameters.AddWithValue(item.Id);
            itemCmd.Parameters.AddWithValue(item.SnapshotId);
            itemCmd.Parameters.AddWithValue(item.StockItemId);
            itemCmd.Parameters.AddWithValue(item.RawQuantity);
            itemCmd.Parameters.AddWithValue(item.WasteFactor);
            itemCmd.Parameters.AddWithValue(item.EffectiveNativeQuantity);
            itemCmd.Parameters.AddWithValue(item.NativeUnitCode);
            itemCmd.Parameters.AddWithValue(item.StockQuantity);
            itemCmd.Parameters.AddWithValue(item.StockUnitCode);
            itemCmd.Parameters.AddWithValue(item.UnitCost);
            itemCmd.Parameters.AddWithValue(item.LineCost);
            itemCmd.Parameters.AddWithValue(item.CreatedAt);

            await itemCmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    private static async Task<List<RecipeCostSnapshotItem>> LoadItemsAsync(NpgsqlConnection conn, Guid snapshotId, CancellationToken ct)
    {
        const string sql = @"
SELECT snapshot_item_id, snapshot_id, stock_item_id, raw_quantity, waste_factor,
       effective_native_quantity, native_unit_code, stock_quantity, stock_unit_code,
       unit_cost, line_cost, created_at
FROM recipe.recipe_cost_snapshot_items
WHERE snapshot_id = $1
ORDER BY created_at ASC;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(snapshotId);

        var items = new List<RecipeCostSnapshotItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetGuid(0);
            var snapId = reader.GetGuid(1);
            var stockItemId = reader.GetGuid(2);
            var rawQty = reader.GetDecimal(3);
            var wasteFactor = reader.GetDecimal(4);
            var effQty = reader.GetDecimal(5);
            var nativeUnit = reader.GetString(6);
            var stockQty = reader.GetDecimal(7);
            var stockUnit = reader.GetString(8);
            var unitCost = reader.GetDecimal(9);
            var lineCost = reader.GetDecimal(10);
            var createdAt = reader.GetFieldValue<DateTimeOffset>(11);

            items.Add(new RecipeCostSnapshotItem(
                id,
                snapId,
                stockItemId,
                rawQty,
                wasteFactor,
                effQty,
                nativeUnit,
                stockQty,
                stockUnit,
                unitCost,
                lineCost,
                createdAt));
        }

        return items;
    }

    private static RecipeCostSnapshot MapHeader(DbDataReader reader)
    {
        var id = reader.GetGuid(0);
        var versionId = reader.GetGuid(1);
        var basisDate = reader.GetFieldValue<DateOnly>(2);
        var cost = reader.GetDecimal(3);
        var currency = reader.GetString(4);
        var createdAt = reader.GetFieldValue<DateTimeOffset>(5);

        return new RecipeCostSnapshot(
            id,
            versionId,
            basisDate,
            cost,
            currency,
            createdAt);
    }
}
