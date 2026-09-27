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
