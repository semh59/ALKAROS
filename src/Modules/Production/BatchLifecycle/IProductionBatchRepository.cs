using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ALKAROS.Production.BatchLifecycle;

public interface IProductionBatchRepository
{
    Task<ProductionBatch> CreateAsync(ProductionBatch batch, CancellationToken ct = default);
    Task<ProductionBatch?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProductionBatch?> GetByBatchNumberAsync(string batchNumber, CancellationToken ct = default);
    Task<ProductionBatch> UpdateAsync(ProductionBatch batch, CancellationToken ct = default);
    Task<IReadOnlyList<ProductionBatch>> ListAsync(ProductionBatchFilter? filter = null, CancellationToken ct = default);
}
