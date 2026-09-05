namespace ALKAROS.Inventory.MovementLedger;

public sealed class InvalidStockMovementException : Exception
{
    public InvalidStockMovementException(string message) : base(message) { }
}

public sealed class StockMovementImmutableException : Exception
{
    public StockMovementImmutableException(string message) : base(message) { }
}

public sealed class DuplicateReversalException : Exception
{
    public DuplicateReversalException(string message) : base(message) { }
}

public sealed class StockMovementNotFoundException : Exception
{
    public StockMovementNotFoundException(Guid id) : base($"Stock movement '{id}' was not found.") { }
}
