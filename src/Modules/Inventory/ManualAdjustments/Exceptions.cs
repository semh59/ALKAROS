namespace ALKAROS.Inventory.ManualAdjustments;

public class InventoryAdjustmentException : Exception
{
    public InventoryAdjustmentException(string message) : base(message) { }
    public InventoryAdjustmentException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class NegativeInventoryResultException : InventoryAdjustmentException
{
    public NegativeInventoryResultException(string message) : base(message) { }
}

public sealed class InvalidAdjustmentReasonException : InventoryAdjustmentException
{
    public InvalidAdjustmentReasonException(string message) : base(message) { }
}

public sealed class UnauthorizedAdjustmentException : InventoryAdjustmentException
{
    public UnauthorizedAdjustmentException(string message) : base(message) { }
}

public sealed class InvalidAdjustmentQuantityException : InventoryAdjustmentException
{
    public InvalidAdjustmentQuantityException(string message) : base(message) { }
}

public sealed class DuplicateAdjustmentException : InventoryAdjustmentException
{
    public DuplicateAdjustmentException(string message) : base(message) { }
}
