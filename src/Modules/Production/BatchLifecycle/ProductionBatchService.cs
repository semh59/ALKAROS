using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ALKAROS.Production.BatchLifecycle;

public sealed class ProductionBatchService : IProductionBatchService
{
    private readonly IProductionBatchRepository _repository;

    public ProductionBatchService(IProductionBatchRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<ProductionBatchDto> CreateBatchAsync(CreateProductionBatchCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var batchId = command.BatchId ?? Guid.NewGuid();
        var batch = ProductionBatch.Create(
            id: batchId,
            batchNumber: command.BatchNumber,
            recipeVersionId: command.RecipeVersionId,
            plannedQuantity: command.PlannedQuantity,
            portionUnitCode: command.PortionUnitCode,
            dailyMenuItemId: command.DailyMenuItemId,
            destinationLocationId: command.DestinationLocationId,
            notes: command.Notes,
            createdBy: command.CreatedBy);

        var created = await _repository.CreateAsync(batch, ct);
        return ProductionBatchDto.FromDomain(created);
    }

    public async Task<ProductionBatchDto> StartBatchAsync(StartProductionBatchCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var batch = await _repository.GetByIdAsync(command.BatchId, ct)
            ?? throw new ProductionBatchNotFoundException(command.BatchId);

        batch.Start(command.StartedAt);
        var updated = await _repository.UpdateAsync(batch, ct);
        return ProductionBatchDto.FromDomain(updated);
    }

    public async Task<ProductionBatchDto> CompleteBatchAsync(CompleteProductionBatchCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var batch = await _repository.GetByIdAsync(command.BatchId, ct)
            ?? throw new ProductionBatchNotFoundException(command.BatchId);

        batch.Complete(command.ActualQuantity, command.CompletedAt);
        var updated = await _repository.UpdateAsync(batch, ct);
        return ProductionBatchDto.FromDomain(updated);
    }

    public async Task<ProductionBatchDto> CancelBatchAsync(CancelProductionBatchCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var batch = await _repository.GetByIdAsync(command.BatchId, ct)
            ?? throw new ProductionBatchNotFoundException(command.BatchId);

        batch.Cancel(command.Reason, command.CancelledAt);
        var updated = await _repository.UpdateAsync(batch, ct);
        return ProductionBatchDto.FromDomain(updated);
    }

    public async Task ReassignRecipeVersionAsync(ReassignRecipeVersionCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var batch = await _repository.GetByIdAsync(command.BatchId, ct)
            ?? throw new ProductionBatchNotFoundException(command.BatchId);

        batch.ReassignRecipeVersion(command.NewRecipeVersionId);
    }

    public async Task<ProductionBatchDto?> GetBatchAsync(Guid batchId, CancellationToken ct = default)
    {
        var batch = await _repository.GetByIdAsync(batchId, ct);
        return batch == null ? null : ProductionBatchDto.FromDomain(batch);
    }

    public async Task<ProductionBatchDto?> GetBatchByNumberAsync(string batchNumber, CancellationToken ct = default)
    {
        var batch = await _repository.GetByBatchNumberAsync(batchNumber, ct);
        return batch == null ? null : ProductionBatchDto.FromDomain(batch);
    }

    public async Task<IReadOnlyList<ProductionBatchDto>> ListBatchesAsync(ProductionBatchFilter? filter = null, CancellationToken ct = default)
    {
        var batches = await _repository.ListAsync(filter, ct);
        return batches.Select(ProductionBatchDto.FromDomain).ToList();
    }
}
