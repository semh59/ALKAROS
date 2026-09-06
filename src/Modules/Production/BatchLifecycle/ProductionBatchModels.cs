using System;

namespace ALKAROS.Production.BatchLifecycle;

public sealed record CreateProductionBatchCommand(
    string BatchNumber,
    Guid RecipeVersionId,
    decimal PlannedQuantity,
    string PortionUnitCode = "portion",
    Guid? DailyMenuItemId = null,
    Guid? DestinationLocationId = null,
    string? Notes = null,
    Guid? CreatedBy = null,
    Guid? BatchId = null);

public sealed record StartProductionBatchCommand(
    Guid BatchId,
    DateTimeOffset? StartedAt = null);

public sealed record CompleteProductionBatchCommand(
    Guid BatchId,
    decimal ActualQuantity,
    DateTimeOffset? CompletedAt = null);

public sealed record CancelProductionBatchCommand(
    Guid BatchId,
    string? Reason = null,
    DateTimeOffset? CancelledAt = null);

public sealed record ReassignRecipeVersionCommand(
    Guid BatchId,
    Guid NewRecipeVersionId);

public sealed record ProductionBatchFilter(
    ProductionBatchStatus? Status = null,
    Guid? RecipeVersionId = null,
    Guid? DailyMenuItemId = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null);

public sealed record ProductionBatchDto(
    Guid Id,
    string BatchNumber,
    Guid RecipeVersionId,
    Guid? DailyMenuItemId,
    ProductionBatchStatus Status,
    decimal PlannedQuantity,
    decimal ActualQuantity,
    string PortionUnitCode,
    Guid? DestinationLocationId,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? ProducedAt,
    DateTimeOffset? CancelledAt,
    string? CancellationReason,
    string? Notes,
    Guid? CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int RowVersion)
{
    public static ProductionBatchDto FromDomain(ProductionBatch b) =>
        new(
            Id: b.Id,
            BatchNumber: b.BatchNumber,
            RecipeVersionId: b.RecipeVersionId,
            DailyMenuItemId: b.DailyMenuItemId,
            Status: b.Status,
            PlannedQuantity: b.PlannedQuantity,
            ActualQuantity: b.ActualQuantity,
            PortionUnitCode: b.PortionUnitCode,
            DestinationLocationId: b.DestinationLocationId,
            StartedAt: b.StartedAt,
            CompletedAt: b.CompletedAt,
            ProducedAt: b.ProducedAt,
            CancelledAt: b.CancelledAt,
            CancellationReason: b.CancellationReason,
            Notes: b.Notes,
            CreatedBy: b.CreatedBy,
            CreatedAt: b.CreatedAt,
            UpdatedAt: b.UpdatedAt,
            RowVersion: b.RowVersion);
}
