using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ALKAROS.Production.StockEffects;

public interface IProductionStockEffectService
{
    Task<ProductionStockEffectResult> ExecuteBatchStockEffectsAsync(ExecuteBatchStockEffectsCommand command, CancellationToken ct = default);
    Task<IReadOnlyList<ProductionConsumptionRecord>> GetConsumptionsByBatchIdAsync(Guid batchId, CancellationToken ct = default);
    Task<IReadOnlyList<ProductionOutputRecord>> GetOutputsByBatchIdAsync(Guid batchId, CancellationToken ct = default);
}
