using ALKAROS.Production.BatchLifecycle;
using ALKAROS.Production.StockEffects;

namespace ALKAROS.Host.Experience.Production;

public sealed record CreateProductionBatchV1(
    string BatchNumber, Guid RecipeVersionId, decimal PlannedQuantity,
    string PortionUnitCode = "portion", Guid? DailyMenuItemId = null,
    Guid? DestinationLocationId = null, string? Notes = null);

public sealed record StartProductionBatchV1(DateTimeOffset? StartedAt);

/// <summary>
/// V1-RMD-133: completing a batch always executes its real stock effects
/// (consumption of recipe ingredients + output of finished portions) —
/// see the endpoint's own doc comment for why ProductionBatchService's
/// own, separate CompleteBatchAsync (no stock movement at all) is
/// deliberately never exposed here.
/// </summary>
public sealed record CompleteProductionBatchV1(
    decimal ActualQuantity, Guid SourceLocationId, Guid? DestinationLocationId, Guid? OutputStockItemId);

public sealed record CancelProductionBatchV1(string? Reason);

public sealed record ReassignRecipeVersionV1(Guid NewRecipeVersionId);

public sealed record ProductionBatchV1(
    Guid Id, string BatchNumber, Guid RecipeVersionId, Guid? DailyMenuItemId, string Status,
    decimal PlannedQuantity, decimal ActualQuantity, string PortionUnitCode, Guid? DestinationLocationId,
    DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, DateTimeOffset? ProducedAt,
    DateTimeOffset? CancelledAt, string? CancellationReason, string? Notes,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int RowVersion)
{
    public static ProductionBatchV1 From(ProductionBatchDto value)
        => new(value.Id, value.BatchNumber, value.RecipeVersionId, value.DailyMenuItemId, value.Status.ToString(),
            value.PlannedQuantity, value.ActualQuantity, value.PortionUnitCode, value.DestinationLocationId,
            value.StartedAt, value.CompletedAt, value.ProducedAt, value.CancelledAt, value.CancellationReason,
            value.Notes, value.CreatedAt, value.UpdatedAt, value.RowVersion);
}

public sealed record ProductionConsumptionV1(
    Guid Id, Guid StockItemId, Guid StockLocationId, decimal Quantity, string UnitCode,
    decimal WasteFactor, decimal NativeQuantity, string NativeUnitCode)
{
    public static ProductionConsumptionV1 From(ProductionConsumptionRecord value)
        => new(value.Id, value.StockItemId, value.StockLocationId, value.Quantity, value.UnitCode,
            value.WasteFactor, value.NativeQuantity, value.NativeUnitCode);
}

public sealed record ProductionOutputV1(
    Guid Id, Guid? StockItemId, Guid StockLocationId, decimal Quantity, string UnitCode)
{
    public static ProductionOutputV1 From(ProductionOutputRecord value)
        => new(value.Id, value.StockItemId, value.StockLocationId, value.Quantity, value.UnitCode);
}

public sealed record ProductionApiErrorV1(string Code, string Message, int Status, string TraceId);

public sealed record ProductionApiErrorEnvelopeV1(ProductionApiErrorV1 Error);
