using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Production.BatchLifecycle;
using Npgsql;

namespace ALKAROS.Production.StockEffects;

public sealed class ProductionStockEffectService : IProductionStockEffectService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IStockBalanceRepository _balanceRepo;
    private readonly IStockMovementRepository _movementRepo;
    private readonly ALKAROS.Measurements.IUnitConverter _unitConverter;

    public ProductionStockEffectService(
        NpgsqlDataSource dataSource,
        IStockBalanceRepository balanceRepo,
        IStockMovementRepository movementRepo,
        ALKAROS.Measurements.IUnitConverter unitConverter)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _balanceRepo = balanceRepo ?? throw new ArgumentNullException(nameof(balanceRepo));
        _movementRepo = movementRepo ?? throw new ArgumentNullException(nameof(movementRepo));
        // V1-RMD-344 (independent 2026-09-26 audit, orta seviye bulgu): replaces this class's own
        // duplicated ResolveConversionFactorAsync (a hardcoded metric-only shortlist plus a raw
        // recipe.unit_conversions query) with the shared, Singleton IUnitConverter every other
        // module already injects (V1-RMD-319/K7) - this module was the one caller still bypassing
        // it, with its own copy that could silently drift from the central built-in table and
        // never saw a custom conversion registered only in memory (RegisterConversion), only
        // ones already persisted to the same table.
        _unitConverter = unitConverter ?? throw new ArgumentNullException(nameof(unitConverter));
    }

    public async Task<ProductionStockEffectResult> ExecuteBatchStockEffectsAsync(
        ExecuteBatchStockEffectsCommand command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.BatchId == Guid.Empty)
            throw new ArgumentException("BatchId cannot be empty.", nameof(command));

        if (command.ActualQuantity <= 0)
            throw new InvalidProductionStockEffectException($"Actual quantity must be greater than zero, got {command.ActualQuantity}.");

        if (command.SourceLocationId == Guid.Empty)
            throw new ArgumentException("SourceLocationId cannot be empty.", nameof(command));

        var now = command.ExecutedAt ?? DateTimeOffset.UtcNow;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // 1. Load batch for update
        const string batchSql = """
            SELECT
                production_batch_id,
                recipe_version_id,
                batch_number,
                status,
                portion_unit_code,
                destination_location_id,
                actual_quantity
            FROM production.production_batches
            WHERE production_batch_id = @id
            FOR UPDATE;
            """;

        Guid recipeVersionId;
        string batchNumber;
        string status;
        string portionUnitCode;
        Guid? defaultDestLocId;
        decimal existingActualQty;

        await using (var bCmd = new NpgsqlCommand(batchSql, conn, tx))
        {
            bCmd.Parameters.AddWithValue("id", command.BatchId);
            await using var bReader = await bCmd.ExecuteReaderAsync(ct);
            if (!await bReader.ReadAsync(ct))
            {
                throw new InvalidProductionStockEffectException($"Production batch '{command.BatchId}' does not exist.");
            }

            recipeVersionId = bReader.GetGuid(1);
            batchNumber = bReader.GetString(2);
            status = bReader.GetString(3);
            portionUnitCode = bReader.GetString(4);
            defaultDestLocId = bReader.IsDBNull(5) ? null : bReader.GetGuid(5);
            existingActualQty = bReader.GetDecimal(6);
        }

        if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidProductionStockEffectException($"Cannot execute stock effects for cancelled batch '{command.BatchId}'.");
        }

        // Idempotency check: if batch is already Completed and consumptions exist, do not recreate movements!
        if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            var existingConsumptions = await LoadConsumptionsAsync(conn, tx, command.BatchId, ct);
            var existingOutputs = await LoadOutputsAsync(conn, tx, command.BatchId, ct);

            if (existingConsumptions.Count > 0 || existingOutputs.Count > 0)
            {
                await tx.RollbackAsync(ct);
                return new ProductionStockEffectResult(
                    BatchId: command.BatchId,
                    ActualQuantity: existingActualQty,
                    Consumptions: existingConsumptions,
                    Output: existingOutputs.Count > 0 ? existingOutputs[0] : null,
                    WasAlreadyExecuted: true);
            }
        }

        // 2. Load RecipeVersion details
        const string rcpSql = """
            SELECT yield_quantity, yield_unit_code
            FROM recipe.recipe_versions
            WHERE id = @id;
            """;

        decimal recipeYieldQty;
        string recipeYieldUnitCode;
        await using (var rCmd = new NpgsqlCommand(rcpSql, conn, tx))
        {
            rCmd.Parameters.AddWithValue("id", recipeVersionId);
            await using var rReader = await rCmd.ExecuteReaderAsync(ct);
            if (!await rReader.ReadAsync(ct))
            {
                throw new InvalidProductionStockEffectException($"Recipe version '{recipeVersionId}' was not found.");
            }
            recipeYieldQty = rReader.GetDecimal(0);
            recipeYieldUnitCode = rReader.GetString(1);
        }

        if (recipeYieldQty <= 0)
        {
            throw new InvalidProductionStockEffectException($"Recipe version yield quantity must be greater than zero, got {recipeYieldQty}.");
        }

        // 3. Load RecipeIngredients
        const string ingSql = """
            SELECT
                ri.ingredient_item_id,
                ri.quantity,
                ri.unit_code,
                ri.loss_percentage,
                si.tracking_unit_code
            FROM recipe.recipe_ingredients ri
            JOIN inventory.stock_items si ON ri.ingredient_item_id = si.id
            WHERE ri.recipe_version_id = @id
            ORDER BY ri.sort_order, ri.created_at, ri.id;
            """;

        var ingredients = new List<IngredientRequirement>();
        await using (var iCmd = new NpgsqlCommand(ingSql, conn, tx))
        {
            iCmd.Parameters.AddWithValue("id", recipeVersionId);
            await using var iReader = await iCmd.ExecuteReaderAsync(ct);
            while (await iReader.ReadAsync(ct))
            {
                var stockItemId = iReader.GetGuid(0);
                var qty = iReader.GetDecimal(1);
                var unitCode = iReader.GetString(2);
                var lossPct = iReader.GetDecimal(3);
                var trackingUnit = iReader.GetString(4);

                ingredients.Add(new IngredientRequirement(stockItemId, qty, unitCode, lossPct, trackingUnit));
            }
        }

        // 4. Calculate requirements according to V0-DOM-010 and CORR:C9
        // Quantity order-of-operations:
        // effectiveNativeQuantity = quantity * scale * (1 + waste_factor) in recipe's native unit,
        // then converted to the stock item's tracked unit as the final step.
        var scale = ActualQuantityInYieldUnit(command.BatchId, command.ActualQuantity, portionUnitCode, recipeYieldUnitCode)
            / recipeYieldQty;
        var plannedConsumptions = new List<PlannedConsumption>();

        foreach (var ing in ingredients)
        {
            var nativeQuantity = Math.Round(ing.RecipeQuantity * scale, 4, MidpointRounding.AwayFromZero);
            var wasteFactor = ing.LossPercentage / 100.0m;
            var effectiveNativeQuantity = Math.Round(nativeQuantity * (1.0m + wasteFactor), 4, MidpointRounding.AwayFromZero);

            decimal effectiveStockQuantity;
            try
            {
                effectiveStockQuantity = Math.Round(
                    _unitConverter.Convert(effectiveNativeQuantity, ing.RecipeUnitCode, ing.StockTrackingUnitCode),
                    4, MidpointRounding.AwayFromZero);
            }
            catch (Exception exception) when (
                exception is ALKAROS.Measurements.UnknownUnitException
                or ALKAROS.Measurements.IncompatibleUnitDimensionException)
            {
                throw new InvalidProductionStockEffectException(
                    $"No unit conversion factor found between '{ing.RecipeUnitCode}' and '{ing.StockTrackingUnitCode}'.", exception);
            }

            plannedConsumptions.Add(new PlannedConsumption(
                StockItemId: ing.StockItemId,
                NativeQuantity: effectiveNativeQuantity,
                NativeUnitCode: ing.RecipeUnitCode,
                WasteFactor: wasteFactor,
                StockQuantity: effectiveStockQuantity,
                StockUnitCode: ing.StockTrackingUnitCode));
        }

        // V1-RMD-344 (independent 2026-09-26 audit, orta seviye bulgu): before this, the FOR
        // UPDATE lock below was taken in whatever order the recipe's own ingredient rows
        // happen to list (ri.sort_order) - the same location for every ingredient here, but a
        // different recipe consuming an overlapping set of stock items in a different
        // sort_order sequence could lock them in the opposite order, deadlocking two
        // concurrent batches against each other. Sorted into one global order first, matching
        // OrderStockConsumptionService.LockStockRowsAsync's own established pattern (V12-RMD-003)
        // for the exact same class of risk.
        var lockOrderedConsumptions = plannedConsumptions.OrderBy(pc => pc.StockItemId).ToList();

        // 5. Atomic Stock Verification: check available_quantity for all ingredients
        foreach (var pc in lockOrderedConsumptions)
        {
            const string balanceSql = """
                SELECT available_quantity
                FROM inventory.stock_balances
                WHERE stock_item_id = @item_id AND stock_location_id = @loc_id
                FOR UPDATE;
                """;

            decimal availableQuantity = 0m;
            bool found = false;

            await using (var balCmd = new NpgsqlCommand(balanceSql, conn, tx))
            {
                balCmd.Parameters.AddWithValue("item_id", pc.StockItemId);
                balCmd.Parameters.AddWithValue("loc_id", command.SourceLocationId);
                var val = await balCmd.ExecuteScalarAsync(ct);
                if (val != null && val != DBNull.Value)
                {
                    availableQuantity = Convert.ToDecimal(val, CultureInfo.InvariantCulture);
                    found = true;
                }
            }

            if (!found || availableQuantity < pc.StockQuantity)
            {
                await tx.RollbackAsync(ct);
                throw new InsufficientProductionStockException(
                    stockItemId: pc.StockItemId,
                    stockLocationId: command.SourceLocationId,
                    requiredQuantity: pc.StockQuantity,
                    availableQuantity: availableQuantity);
            }
        }

        // 6. Post Stock Movements & Consumptions
        var consumptionsResult = new List<ProductionConsumptionRecord>();

        foreach (var pc in plannedConsumptions)
        {
            // Deduct stock balance and post the consumption movement through
            // Inventory's own contract, using this batch's connection and
            // transaction so the stock effect commits or rolls back with the
            // rest of the batch completion (V0-ARC-001 row 11).
            await _balanceRepo.ApplyOnHandDeltaAsync(
                pc.StockItemId, command.SourceLocationId, -pc.StockQuantity, conn, tx, ct);

            var movementId = Guid.NewGuid();
            var consumptionMovement = new StockMovement(
                id: movementId,
                stockItemId: pc.StockItemId,
                stockLocationId: command.SourceLocationId,
                movementType: StockMovementType.Consumption,
                direction: MovementDirection.Out,
                quantity: pc.StockQuantity,
                unitCode: pc.StockUnitCode,
                sourceType: StockMovementSourceType.ProductionOrder,
                sourceReferenceId: command.BatchId,
                reason: $"Production consumption for batch {batchNumber}",
                createdBy: command.ExecutedBy,
                createdAt: now);
            await _movementRepo.AppendAsync(consumptionMovement, conn, tx, ct);

            // Insert production.production_consumptions
            var consumptionId = Guid.NewGuid();
            const string consSql = """
                INSERT INTO production.production_consumptions (
                    production_consumption_id,
                    production_batch_id,
                    stock_item_id,
                    stock_location_id,
                    quantity,
                    unit_code,
                    waste_factor,
                    native_quantity,
                    native_unit_code,
                    stock_movement_id,
                    created_at
                ) VALUES (
                    @consumption_id,
                    @batch_id,
                    @item_id,
                    @loc_id,
                    @qty,
                    @unit_code,
                    @waste_factor,
                    @native_qty,
                    @native_unit_code,
                    @movement_id,
                    @now
                );
                """;

            await using (var cCmd = new NpgsqlCommand(consSql, conn, tx))
            {
                cCmd.Parameters.AddWithValue("consumption_id", consumptionId);
                cCmd.Parameters.AddWithValue("batch_id", command.BatchId);
                cCmd.Parameters.AddWithValue("item_id", pc.StockItemId);
                cCmd.Parameters.AddWithValue("loc_id", command.SourceLocationId);
                cCmd.Parameters.AddWithValue("qty", pc.StockQuantity);
                cCmd.Parameters.AddWithValue("unit_code", pc.StockUnitCode);
                cCmd.Parameters.AddWithValue("waste_factor", pc.WasteFactor);
                cCmd.Parameters.AddWithValue("native_qty", pc.NativeQuantity);
                cCmd.Parameters.AddWithValue("native_unit_code", pc.NativeUnitCode);
                cCmd.Parameters.AddWithValue("movement_id", movementId);
                cCmd.Parameters.AddWithValue("now", now);
                await cCmd.ExecuteNonQueryAsync(ct);
            }

            consumptionsResult.Add(new ProductionConsumptionRecord(
                Id: consumptionId,
                BatchId: command.BatchId,
                StockItemId: pc.StockItemId,
                StockLocationId: command.SourceLocationId,
                Quantity: pc.StockQuantity,
                UnitCode: pc.StockUnitCode,
                WasteFactor: pc.WasteFactor,
                NativeQuantity: pc.NativeQuantity,
                NativeUnitCode: pc.NativeUnitCode,
                StockMovementId: movementId,
                CreatedAt: now));
        }

        // 7. Post Production Output
        ProductionOutputRecord? outputRecord = null;
        var destinationLocationId = command.DestinationLocationId ?? defaultDestLocId ?? command.SourceLocationId;
        Guid? outputMovementId = null;

        if (command.OutputStockItemId.HasValue)
        {
            // Increment stock balance and post the output movement through
            // Inventory's own contract, same connection/transaction as the
            // rest of this batch completion (V0-ARC-001 row 11).
            await _balanceRepo.ApplyOnHandDeltaAsync(
                command.OutputStockItemId.Value, destinationLocationId, command.ActualQuantity, conn, tx, ct);

            outputMovementId = Guid.NewGuid();
            var outputMovement = new StockMovement(
                id: outputMovementId.Value,
                stockItemId: command.OutputStockItemId.Value,
                stockLocationId: destinationLocationId,
                movementType: StockMovementType.ProductionOutput,
                direction: MovementDirection.In,
                quantity: command.ActualQuantity,
                unitCode: portionUnitCode,
                sourceType: StockMovementSourceType.ProductionOrder,
                sourceReferenceId: command.BatchId,
                reason: $"Production output for batch {batchNumber}",
                createdBy: command.ExecutedBy,
                createdAt: now);
            await _movementRepo.AppendAsync(outputMovement, conn, tx, ct);
        }

        var outputId = Guid.NewGuid();
        const string insertOutputSql = """
            INSERT INTO production.production_outputs (
                production_output_id,
                production_batch_id,
                stock_item_id,
                stock_location_id,
                quantity,
                unit_code,
                stock_movement_id,
                created_at
            ) VALUES (
                @output_id,
                @batch_id,
                @stock_item_id,
                @loc_id,
                @qty,
                @unit_code,
                @movement_id,
                @now
            );
            """;

        await using (var outCmd = new NpgsqlCommand(insertOutputSql, conn, tx))
        {
            outCmd.Parameters.AddWithValue("output_id", outputId);
            outCmd.Parameters.AddWithValue("batch_id", command.BatchId);
            outCmd.Parameters.AddWithValue("stock_item_id", (object?)command.OutputStockItemId ?? DBNull.Value);
            outCmd.Parameters.AddWithValue("loc_id", destinationLocationId);
            outCmd.Parameters.AddWithValue("qty", command.ActualQuantity);
            outCmd.Parameters.AddWithValue("unit_code", portionUnitCode);
            outCmd.Parameters.AddWithValue("movement_id", (object?)outputMovementId ?? DBNull.Value);
            outCmd.Parameters.AddWithValue("now", now);
            await outCmd.ExecuteNonQueryAsync(ct);
        }

        outputRecord = new ProductionOutputRecord(
            Id: outputId,
            BatchId: command.BatchId,
            StockItemId: command.OutputStockItemId,
            StockLocationId: destinationLocationId,
            Quantity: command.ActualQuantity,
            UnitCode: portionUnitCode,
            StockMovementId: outputMovementId,
            CreatedAt: now);

        // 8. Update batch status to Completed
        const string updateBatchSql = """
            UPDATE production.production_batches
            SET
                status = 'Completed',
                actual_quantity = @actual_quantity,
                started_at = COALESCE(started_at, @now),
                completed_at = @now,
                produced_at = @now,
                updated_at = @now,
                row_version = row_version + 1
            WHERE production_batch_id = @id;
            """;

        await using (var uCmd = new NpgsqlCommand(updateBatchSql, conn, tx))
        {
            uCmd.Parameters.AddWithValue("id", command.BatchId);
            uCmd.Parameters.AddWithValue("actual_quantity", command.ActualQuantity);
            uCmd.Parameters.AddWithValue("now", now);
            await uCmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);

        return new ProductionStockEffectResult(
            BatchId: command.BatchId,
            ActualQuantity: command.ActualQuantity,
            Consumptions: consumptionsResult,
            Output: outputRecord,
            WasAlreadyExecuted: false);
    }

    public async Task<IReadOnlyList<ProductionConsumptionRecord>> GetConsumptionsByBatchIdAsync(
        Guid batchId,
        CancellationToken ct = default)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        return await LoadConsumptionsAsync(conn, null, batchId, ct);
    }

    public async Task<IReadOnlyList<ProductionOutputRecord>> GetOutputsByBatchIdAsync(
        Guid batchId,
        CancellationToken ct = default)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        return await LoadOutputsAsync(conn, null, batchId, ct);
    }

    private static async Task<List<ProductionConsumptionRecord>> LoadConsumptionsAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction? tx,
        Guid batchId,
        CancellationToken ct)
    {
        const string sql = """
            SELECT
                production_consumption_id,
                production_batch_id,
                stock_item_id,
                stock_location_id,
                quantity,
                unit_code,
                waste_factor,
                native_quantity,
                native_unit_code,
                stock_movement_id,
                created_at
            FROM production.production_consumptions
            WHERE production_batch_id = @batch_id
            ORDER BY created_at ASC;
            """;

        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("batch_id", batchId);

        var list = new List<ProductionConsumptionRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ProductionConsumptionRecord(
                Id: reader.GetGuid(0),
                BatchId: reader.GetGuid(1),
                StockItemId: reader.GetGuid(2),
                StockLocationId: reader.GetGuid(3),
                Quantity: reader.GetDecimal(4),
                UnitCode: reader.GetString(5),
                WasteFactor: reader.GetDecimal(6),
                NativeQuantity: reader.GetDecimal(7),
                NativeUnitCode: reader.GetString(8),
                StockMovementId: reader.IsDBNull(9) ? null : reader.GetGuid(9),
                CreatedAt: reader.GetFieldValue<DateTimeOffset>(10)));
        }

        return list;
    }

    private static async Task<List<ProductionOutputRecord>> LoadOutputsAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction? tx,
        Guid batchId,
        CancellationToken ct)
    {
        const string sql = """
            SELECT
                production_output_id,
                production_batch_id,
                stock_item_id,
                stock_location_id,
                quantity,
                unit_code,
                stock_movement_id,
                created_at
            FROM production.production_outputs
            WHERE production_batch_id = @batch_id
            ORDER BY created_at ASC;
            """;

        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("batch_id", batchId);

        var list = new List<ProductionOutputRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ProductionOutputRecord(
                Id: reader.GetGuid(0),
                BatchId: reader.GetGuid(1),
                StockItemId: reader.IsDBNull(2) ? null : reader.GetGuid(2),
                StockLocationId: reader.GetGuid(3),
                Quantity: reader.GetDecimal(4),
                UnitCode: reader.GetString(5),
                StockMovementId: reader.IsDBNull(6) ? null : reader.GetGuid(6),
                CreatedAt: reader.GetFieldValue<DateTimeOffset>(7)));
        }

        return list;
    }

    private sealed record IngredientRequirement(
        Guid StockItemId,
        decimal RecipeQuantity,
        string RecipeUnitCode,
        decimal LossPercentage,
        string StockTrackingUnitCode);

    private sealed record PlannedConsumption(
        Guid StockItemId,
        decimal NativeQuantity,
        string NativeUnitCode,
        decimal WasteFactor,
        decimal StockQuantity,
        string StockUnitCode);

    /// <summary>
    /// V1-RMD-419 (V1-RMD-398 G-04): the batch's quantity expressed in the recipe's yield unit. The scale used to be
    /// <c>actual / yield</c> with the two units never compared, so a 4 "portion" batch of a recipe yielding 2 kg
    /// consumed ingredients for 4 kg. A unit the converter cannot turn into the yield unit refuses the batch.
    /// </summary>
    private decimal ActualQuantityInYieldUnit(Guid batchId, decimal actualQuantity, string batchUnitCode, string yieldUnitCode)
    {
        if (string.Equals(batchUnitCode, yieldUnitCode, StringComparison.OrdinalIgnoreCase))
            return actualQuantity;

        try
        {
            return _unitConverter.Convert(actualQuantity, batchUnitCode, yieldUnitCode);
        }
        catch (Exception exception) when (
            exception is ALKAROS.Measurements.UnknownUnitException
            or ALKAROS.Measurements.IncompatibleUnitDimensionException)
        {
            throw new ProductionBatchUnitMismatchException(batchId, batchUnitCode, yieldUnitCode, exception);
        }
    }
}
