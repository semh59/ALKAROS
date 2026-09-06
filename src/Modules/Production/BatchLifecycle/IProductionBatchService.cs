using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ALKAROS.Production.BatchLifecycle;

public interface IProductionBatchService
{
    Task<ProductionBatchDto> CreateBatchAsync(CreateProductionBatchCommand command, CancellationToken ct = default);
    Task<ProductionBatchDto> StartBatchAsync(StartProductionBatchCommand command, CancellationToken ct = default);
    Task<ProductionBatchDto> CompleteBatchAsync(CompleteProductionBatchCommand command, CancellationToken ct = default);
    Task<ProductionBatchDto> CancelBatchAsync(CancelProductionBatchCommand command, CancellationToken ct = default);
    Task ReassignRecipeVersionAsync(ReassignRecipeVersionCommand command, CancellationToken ct = default);
    Task<ProductionBatchDto?> GetBatchAsync(Guid batchId, CancellationToken ct = default);
    Task<ProductionBatchDto?> GetBatchByNumberAsync(string batchNumber, CancellationToken ct = default);
    Task<IReadOnlyList<ProductionBatchDto>> ListBatchesAsync(ProductionBatchFilter? filter = null, CancellationToken ct = default);
}
