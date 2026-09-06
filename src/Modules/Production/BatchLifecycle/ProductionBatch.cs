using System;

namespace ALKAROS.Production.BatchLifecycle;

public enum ProductionBatchStatus
{
    Planned,
    InProgress,
    Completed,
    Cancelled
}

public sealed class ProductionBatch
{
    public Guid Id { get; }
    public string BatchNumber { get; }
    public Guid RecipeVersionId { get; }
    public Guid? DailyMenuItemId { get; }
    public ProductionBatchStatus Status { get; private set; }
    public decimal PlannedQuantity { get; }
    public decimal ActualQuantity { get; private set; }
    public string PortionUnitCode { get; }
    public Guid? DestinationLocationId { get; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? ProducedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public string? Notes { get; }
    public Guid? CreatedBy { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public int RowVersion { get; private set; }

    private ProductionBatch(
        Guid id,
        string batchNumber,
        Guid recipeVersionId,
        Guid? dailyMenuItemId,
        ProductionBatchStatus status,
        decimal plannedQuantity,
        decimal actualQuantity,
        string portionUnitCode,
        Guid? destinationLocationId,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt,
        DateTimeOffset? producedAt,
        DateTimeOffset? cancelledAt,
        string? cancellationReason,
        string? notes,
        Guid? createdBy,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        int rowVersion)
    {
        Id = id;
        BatchNumber = batchNumber;
        RecipeVersionId = recipeVersionId;
        DailyMenuItemId = dailyMenuItemId;
        Status = status;
        PlannedQuantity = plannedQuantity;
        ActualQuantity = actualQuantity;
        PortionUnitCode = portionUnitCode;
        DestinationLocationId = destinationLocationId;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        ProducedAt = producedAt;
        CancelledAt = cancelledAt;
        CancellationReason = cancellationReason;
        Notes = notes;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        RowVersion = rowVersion;
    }

    public static ProductionBatch Create(
        Guid id,
        string batchNumber,
        Guid recipeVersionId,
        decimal plannedQuantity,
        string portionUnitCode = "portion",
        Guid? dailyMenuItemId = null,
        Guid? destinationLocationId = null,
        string? notes = null,
        Guid? createdBy = null,
        DateTimeOffset? createdAt = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Batch ID cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(batchNumber))
        {
            throw new ArgumentException("Batch number cannot be empty.", nameof(batchNumber));
        }

        if (recipeVersionId == Guid.Empty)
        {
            throw new ArgumentException("RecipeVersion ID cannot be empty.", nameof(recipeVersionId));
        }

        if (plannedQuantity <= 0)
        {
            throw new InvalidProductionBatchQuantityException(
                $"Planned quantity must be greater than zero. Given: {plannedQuantity}");
        }

        if (string.IsNullOrWhiteSpace(portionUnitCode))
        {
            throw new ArgumentException("Portion unit code cannot be empty.", nameof(portionUnitCode));
        }

        var now = createdAt ?? DateTimeOffset.UtcNow;

        return new ProductionBatch(
            id: id,
            batchNumber: batchNumber.Trim(),
            recipeVersionId: recipeVersionId,
            dailyMenuItemId: dailyMenuItemId,
            status: ProductionBatchStatus.Planned,
            plannedQuantity: plannedQuantity,
            actualQuantity: 0,
            portionUnitCode: portionUnitCode.Trim(),
            destinationLocationId: destinationLocationId,
            startedAt: null,
            completedAt: null,
            producedAt: null,
            cancelledAt: null,
            cancellationReason: null,
            notes: notes?.Trim(),
            createdBy: createdBy,
            createdAt: now,
            updatedAt: now,
            rowVersion: 1);
    }

    public static ProductionBatch Reconstitute(
        Guid id,
        string batchNumber,
        Guid recipeVersionId,
        Guid? dailyMenuItemId,
        ProductionBatchStatus status,
        decimal plannedQuantity,
        decimal actualQuantity,
        string portionUnitCode,
        Guid? destinationLocationId,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt,
        DateTimeOffset? producedAt,
        DateTimeOffset? cancelledAt,
        string? cancellationReason,
        string? notes,
        Guid? createdBy,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        int rowVersion)
    {
        return new ProductionBatch(
            id,
            batchNumber,
            recipeVersionId,
            dailyMenuItemId,
            status,
            plannedQuantity,
            actualQuantity,
            portionUnitCode,
            destinationLocationId,
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

    public void Start(DateTimeOffset? startedAt = null)
    {
        if (Status != ProductionBatchStatus.Planned)
        {
            throw new InvalidProductionBatchTransitionException(Status, ProductionBatchStatus.InProgress);
        }

        var now = startedAt ?? DateTimeOffset.UtcNow;
        Status = ProductionBatchStatus.InProgress;
        StartedAt = now;
        UpdatedAt = now;
    }

    public void Complete(decimal actualQuantity, DateTimeOffset? completedAt = null)
    {
        if (Status != ProductionBatchStatus.InProgress)
        {
            throw new InvalidProductionBatchTransitionException(Status, ProductionBatchStatus.Completed);
        }

        if (actualQuantity <= 0)
        {
            throw new InvalidProductionBatchQuantityException(
                $"Actual quantity must be greater than zero upon batch completion. Given: {actualQuantity}");
        }

        var now = completedAt ?? DateTimeOffset.UtcNow;
        ActualQuantity = actualQuantity;
        Status = ProductionBatchStatus.Completed;
        CompletedAt = now;
        ProducedAt = now;
        UpdatedAt = now;
    }

    public void Cancel(string? reason = null, DateTimeOffset? cancelledAt = null)
    {
        if (Status == ProductionBatchStatus.Completed || Status == ProductionBatchStatus.Cancelled)
        {
            throw new InvalidProductionBatchTransitionException(Status, ProductionBatchStatus.Cancelled);
        }

        var now = cancelledAt ?? DateTimeOffset.UtcNow;
        Status = ProductionBatchStatus.Cancelled;
        CancelledAt = now;
        CancellationReason = reason?.Trim();
        UpdatedAt = now;
    }

    public void ReassignRecipeVersion(Guid attemptedRecipeVersionId)
    {
        throw new RecipeVersionImmutableException(Id, RecipeVersionId, attemptedRecipeVersionId);
    }

    public void IncrementRowVersion()
    {
        RowVersion++;
    }
}
