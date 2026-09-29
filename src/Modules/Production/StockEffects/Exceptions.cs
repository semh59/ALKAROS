using System;

namespace ALKAROS.Production.StockEffects;

public class ProductionStockEffectException : Exception
{
    public ProductionStockEffectException(string message) : base(message) { }
    public ProductionStockEffectException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class InsufficientProductionStockException : ProductionStockEffectException
{
    public Guid StockItemId { get; }
    public Guid StockLocationId { get; }
    public decimal RequiredQuantity { get; }
    public decimal AvailableQuantity { get; }

    public InsufficientProductionStockException(
        Guid stockItemId,
        Guid stockLocationId,
        decimal requiredQuantity,
        decimal availableQuantity)
        : base($"Insufficient stock for item '{stockItemId}' at location '{stockLocationId}'. Required: {requiredQuantity}, Available: {availableQuantity}.")
    {
        StockItemId = stockItemId;
        StockLocationId = stockLocationId;
        RequiredQuantity = requiredQuantity;
        AvailableQuantity = availableQuantity;
    }
}

public sealed class InvalidProductionStockEffectException : ProductionStockEffectException
{
    public InvalidProductionStockEffectException(string message) : base(message) { }
    public InvalidProductionStockEffectException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// V1-RMD-419: a batch's quantity unit cannot be converted into its recipe's yield unit, so the ingredient scale is
/// undefined.
/// </summary>
public sealed class ProductionBatchUnitMismatchException : ProductionStockEffectException
{
    public ProductionBatchUnitMismatchException(Guid batchId, string batchUnitCode, string yieldUnitCode, Exception innerException)
        : base($"Batch '{batchId}' is measured in '{batchUnitCode}', which cannot be converted to the recipe yield unit '{yieldUnitCode}'.", innerException)
    {
        BatchUnitCode = batchUnitCode;
        YieldUnitCode = yieldUnitCode;
    }

    public string BatchUnitCode { get; }

    public string YieldUnitCode { get; }
}
