namespace ALKAROS.Inventory.BalanceProjection;

public sealed class StockBalanceNotFoundException : Exception
{
    public StockBalanceNotFoundException(Guid stockItemId, Guid stockLocationId)
        : base($"Stock balance not found for Item '{stockItemId}' at Location '{stockLocationId}'.") { }
}

public sealed class StockBalanceConcurrencyException : Exception
{
    public StockBalanceConcurrencyException(string message) : base(message) { }
}

public sealed class InvalidStockBalanceException : Exception
{
    public InvalidStockBalanceException(string message) : base(message) { }
}
