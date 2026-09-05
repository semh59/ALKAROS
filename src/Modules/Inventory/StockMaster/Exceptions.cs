namespace ALKAROS.Inventory.StockMaster;

public sealed class InactiveStockItemException : Exception
{
    public InactiveStockItemException(string message) : base(message) { }
}

public sealed class InactiveStockLocationException : Exception
{
    public InactiveStockLocationException(string message) : base(message) { }
}

public sealed class DuplicateStockItemException : Exception
{
    public DuplicateStockItemException(string message) : base(message) { }
}

public sealed class DuplicateStockLocationException : Exception
{
    public DuplicateStockLocationException(string message) : base(message) { }
}

public sealed class InvalidStockItemException : Exception
{
    public InvalidStockItemException(string message) : base(message) { }
}

public sealed class InvalidStockLocationException : Exception
{
    public InvalidStockLocationException(string message) : base(message) { }
}

public sealed class InvalidProductStockMappingException : Exception
{
    public InvalidProductStockMappingException(string message) : base(message) { }
}

public sealed class StockItemNotFoundException : Exception
{
    public StockItemNotFoundException(Guid id) : base($"Stock item {id} was not found.") { }
}

public sealed class StockLocationNotFoundException : Exception
{
    public StockLocationNotFoundException(Guid id) : base($"Stock location {id} was not found.") { }
}

public sealed class StockMasterConcurrencyException : Exception
{
    public StockMasterConcurrencyException(string message) : base(message) { }
}
