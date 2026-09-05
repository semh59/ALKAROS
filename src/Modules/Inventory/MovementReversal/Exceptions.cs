using ALKAROS.Inventory.MovementLedger;

namespace ALKAROS.Inventory.MovementReversal;

public class StockMovementReversalException : Exception
{
    public StockMovementReversalException(string message) : base(message) { }
    public StockMovementReversalException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class ReversalNotEligibleException : StockMovementReversalException
{
    public ReversalNotEligibleException(string message) : base(message) { }
}

public sealed class InvalidReversalReasonException : StockMovementReversalException
{
    public InvalidReversalReasonException(string message) : base(message) { }
}
