using System;
using System.Collections.Generic;

namespace ALKAROS.Production.StockEffects;

public sealed record ExecuteBatchStockEffectsCommand(
    Guid BatchId,
    decimal ActualQuantity,
    Guid SourceLocationId,
    Guid? DestinationLocationId = null,
    Guid? OutputStockItemId = null,
    DateTimeOffset? ExecutedAt = null,
    Guid? ExecutedBy = null);

public sealed record ProductionConsumptionRecord(
    Guid Id,
    Guid BatchId,
    Guid StockItemId,
    Guid StockLocationId,
    decimal Quantity,
    string UnitCode,
    decimal WasteFactor,
    decimal NativeQuantity,
    string NativeUnitCode,
    Guid? StockMovementId,
    DateTimeOffset CreatedAt);

public sealed record ProductionOutputRecord(
    Guid Id,
    Guid BatchId,
    Guid? StockItemId,
    Guid StockLocationId,
    decimal Quantity,
    string UnitCode,
    Guid? StockMovementId,
    DateTimeOffset CreatedAt);

public sealed record ProductionStockEffectResult(
    Guid BatchId,
    decimal ActualQuantity,
    IReadOnlyList<ProductionConsumptionRecord> Consumptions,
    ProductionOutputRecord? Output,
    bool WasAlreadyExecuted);
