using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace ALKAROS.Production.BatchLifecycle;

public sealed class PostgresProductionBatchRepository : IProductionBatchRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresProductionBatchRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<ProductionBatch> CreateAsync(ProductionBatch batch, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO production.production_batches (
                production_batch_id,
                batch_number,
                recipe_version_id,
                daily_menu_item_id,
                status,
                planned_quantity,
                actual_quantity,
                portion_unit_code,
                destination_location_id,
                started_at,
                completed_at,
                produced_at,
                cancelled_at,
                cancellation_reason,
                notes,
                created_by,
                created_at,
                updated_at,
                row_version
            ) VALUES (
                @id,
                @batch_number,
                @recipe_version_id,
                @daily_menu_item_id,
                @status,
                @planned_quantity,
                @actual_quantity,
                @portion_unit_code,
                @destination_location_id,
                @started_at,
                @completed_at,
                @produced_at,
                @cancelled_at,
                @cancellation_reason,
                @notes,
                @created_by,
                @created_at,
                @updated_at,
                @row_version
            );
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("id", batch.Id);
        cmd.Parameters.AddWithValue("batch_number", batch.BatchNumber);
        cmd.Parameters.AddWithValue("recipe_version_id", batch.RecipeVersionId);
        cmd.Parameters.AddWithValue("daily_menu_item_id", (object?)batch.DailyMenuItemId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", batch.Status.ToString());
        cmd.Parameters.AddWithValue("planned_quantity", batch.PlannedQuantity);
        cmd.Parameters.AddWithValue("actual_quantity", batch.ActualQuantity);
        cmd.Parameters.AddWithValue("portion_unit_code", batch.PortionUnitCode);
        cmd.Parameters.AddWithValue("destination_location_id", (object?)batch.DestinationLocationId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("started_at", (object?)batch.StartedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("completed_at", (object?)batch.CompletedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("produced_at", (object?)batch.ProducedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cancelled_at", (object?)batch.CancelledAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cancellation_reason", (object?)batch.CancellationReason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("notes", (object?)batch.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("created_by", (object?)batch.CreatedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("created_at", batch.CreatedAt);
        cmd.Parameters.AddWithValue("updated_at", batch.UpdatedAt);
        cmd.Parameters.AddWithValue("row_version", batch.RowVersion);

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
            return batch;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new ProductionBatchDuplicateNumberException(batch.BatchNumber);
        }
    }

    public async Task<ProductionBatch?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                production_batch_id,
                batch_number,
                recipe_version_id,
                daily_menu_item_id,
                status,
                planned_quantity,
                actual_quantity,
                portion_unit_code,
                destination_location_id,
                started_at,
                completed_at,
                produced_at,
                cancelled_at,
                cancellation_reason,
                notes,
                created_by,
                created_at,
                updated_at,
                row_version
            FROM production.production_batches
            WHERE production_batch_id = @id;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return MapFromReader(reader);
    }

    public async Task<ProductionBatch?> GetByBatchNumberAsync(string batchNumber, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                production_batch_id,
                batch_number,
                recipe_version_id,
                daily_menu_item_id,
                status,
                planned_quantity,
                actual_quantity,
                portion_unit_code,
                destination_location_id,
                started_at,
                completed_at,
                produced_at,
                cancelled_at,
                cancellation_reason,
                notes,
                created_by,
                created_at,
                updated_at,
                row_version
            FROM production.production_batches
            WHERE batch_number = @batch_number;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("batch_number", batchNumber.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return MapFromReader(reader);
    }

    public async Task<ProductionBatch> UpdateAsync(ProductionBatch batch, CancellationToken ct = default)
    {
        const string updateSql = """
            UPDATE production.production_batches
            SET
                status = @status,
                actual_quantity = @actual_quantity,
                started_at = @started_at,
                completed_at = @completed_at,
                produced_at = @produced_at,
                cancelled_at = @cancelled_at,
                cancellation_reason = @cancellation_reason,
                updated_at = @updated_at,
                row_version = row_version + 1
            WHERE production_batch_id = @id AND row_version = @row_version
            RETURNING row_version;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(updateSql, conn);

        cmd.Parameters.AddWithValue("id", batch.Id);
        cmd.Parameters.AddWithValue("status", batch.Status.ToString());
        cmd.Parameters.AddWithValue("actual_quantity", batch.ActualQuantity);
        cmd.Parameters.AddWithValue("started_at", (object?)batch.StartedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("completed_at", (object?)batch.CompletedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("produced_at", (object?)batch.ProducedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cancelled_at", (object?)batch.CancelledAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cancellation_reason", (object?)batch.CancellationReason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("updated_at", batch.UpdatedAt);
        cmd.Parameters.AddWithValue("row_version", batch.RowVersion);

        var newVersionObj = await cmd.ExecuteScalarAsync(ct);
        if (newVersionObj != null && newVersionObj != DBNull.Value)
        {
            var newVersion = Convert.ToInt32(newVersionObj, System.Globalization.CultureInfo.InvariantCulture);
            return ProductionBatch.Reconstitute(
                batch.Id,
                batch.BatchNumber,
                batch.RecipeVersionId,
                batch.DailyMenuItemId,
                batch.Status,
                batch.PlannedQuantity,
                batch.ActualQuantity,
                batch.PortionUnitCode,
                batch.DestinationLocationId,
                batch.StartedAt,
                batch.CompletedAt,
                batch.ProducedAt,
                batch.CancelledAt,
                batch.CancellationReason,
                batch.Notes,
                batch.CreatedBy,
                batch.CreatedAt,
                batch.UpdatedAt,
                newVersion);
        }

        // Row was not updated. Determine whether it was not found or concurrency conflict.
        var existing = await GetByIdAsync(batch.Id, ct);
        if (existing == null)
        {
            throw new ProductionBatchNotFoundException(batch.Id);
        }

        throw new ProductionBatchConcurrencyException(batch.Id, batch.RowVersion);
    }

    public async Task<IReadOnlyList<ProductionBatch>> ListAsync(ProductionBatchFilter? filter = null, CancellationToken ct = default)
    {
        var sql = """
            SELECT
                production_batch_id,
                batch_number,
                recipe_version_id,
                daily_menu_item_id,
                status,
                planned_quantity,
                actual_quantity,
                portion_unit_code,
                destination_location_id,
                started_at,
                completed_at,
                produced_at,
                cancelled_at,
                cancellation_reason,
                notes,
                created_by,
                created_at,
                updated_at,
                row_version
            FROM production.production_batches
            WHERE 1=1
            """;

        var parameters = new List<NpgsqlParameter>();

        if (filter?.Status.HasValue == true)
        {
            sql += " AND status = @status";
            parameters.Add(new NpgsqlParameter("status", filter.Status.Value.ToString()));
        }

        if (filter?.RecipeVersionId.HasValue == true)
        {
            sql += " AND recipe_version_id = @recipe_version_id";
            parameters.Add(new NpgsqlParameter("recipe_version_id", filter.RecipeVersionId.Value));
        }

        if (filter?.DailyMenuItemId.HasValue == true)
        {
            sql += " AND daily_menu_item_id = @daily_menu_item_id";
            parameters.Add(new NpgsqlParameter("daily_menu_item_id", filter.DailyMenuItemId.Value));
        }

        if (filter?.FromDate.HasValue == true)
        {
            sql += " AND created_at >= @from_date";
            parameters.Add(new NpgsqlParameter("from_date", filter.FromDate.Value));
        }

        if (filter?.ToDate.HasValue == true)
        {
            sql += " AND created_at <= @to_date";
            parameters.Add(new NpgsqlParameter("to_date", filter.ToDate.Value));
        }

        sql += " ORDER BY created_at DESC;";

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        foreach (var p in parameters)
        {
            cmd.Parameters.Add(p);
        }

        var results = new List<ProductionBatch>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(MapFromReader(reader));
        }

        return results;
    }

    private static ProductionBatch MapFromReader(NpgsqlDataReader reader)
    {
        var id = reader.GetGuid(reader.GetOrdinal("production_batch_id"));
        var batchNumber = reader.GetString(reader.GetOrdinal("batch_number"));
        var recipeVersionId = reader.GetGuid(reader.GetOrdinal("recipe_version_id"));
        var dailyMenuItemId = reader.IsDBNull(reader.GetOrdinal("daily_menu_item_id"))
            ? (Guid?)null
            : reader.GetGuid(reader.GetOrdinal("daily_menu_item_id"));
        var statusStr = reader.GetString(reader.GetOrdinal("status"));
        var status = Enum.Parse<ProductionBatchStatus>(statusStr, ignoreCase: true);
        var plannedQty = reader.GetDecimal(reader.GetOrdinal("planned_quantity"));
        var actualQty = reader.GetDecimal(reader.GetOrdinal("actual_quantity"));
        var portionUnitCode = reader.GetString(reader.GetOrdinal("portion_unit_code"));
        var destLocationId = reader.IsDBNull(reader.GetOrdinal("destination_location_id"))
            ? (Guid?)null
            : reader.GetGuid(reader.GetOrdinal("destination_location_id"));
        var startedAt = reader.IsDBNull(reader.GetOrdinal("started_at"))
            ? (DateTimeOffset?)null
            : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("started_at"));
        var completedAt = reader.IsDBNull(reader.GetOrdinal("completed_at"))
            ? (DateTimeOffset?)null
            : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("completed_at"));
        var producedAt = reader.IsDBNull(reader.GetOrdinal("produced_at"))
            ? (DateTimeOffset?)null
            : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("produced_at"));
        var cancelledAt = reader.IsDBNull(reader.GetOrdinal("cancelled_at"))
            ? (DateTimeOffset?)null
            : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("cancelled_at"));
        var cancellationReason = reader.IsDBNull(reader.GetOrdinal("cancellation_reason"))
            ? null
            : reader.GetString(reader.GetOrdinal("cancellation_reason"));
        var notes = reader.IsDBNull(reader.GetOrdinal("notes"))
            ? null
            : reader.GetString(reader.GetOrdinal("notes"));
        var createdBy = reader.IsDBNull(reader.GetOrdinal("created_by"))
            ? (Guid?)null
            : reader.GetGuid(reader.GetOrdinal("created_by"));
        var createdAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at"));
        var updatedAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("updated_at"));
        var rowVersion = reader.GetInt32(reader.GetOrdinal("row_version"));

        return ProductionBatch.Reconstitute(
            id,
            batchNumber,
            recipeVersionId,
            dailyMenuItemId,
            status,
            plannedQty,
            actualQty,
            portionUnitCode,
            destLocationId,
            startedAt,
            completedAt,
            producedAt,
            cancelledAt,
            cancellationReason,
            notes,
            createdBy,
            createdAt,
            updatedAt,
            rowVersion);
    }
}
