namespace ALKAROS.Kitchen.PhysicalPrintRecovery;

/// <summary>
/// Thrown when optimistic concurrency conflict occurs on a physical print delivery record.
/// </summary>
public sealed class PhysicalPrintDeliveryConcurrencyException : InvalidOperationException
{
    public PhysicalPrintDeliveryConcurrencyException(string message)
        : base(message)
    {
    }

    public PhysicalPrintDeliveryConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when an illegal state transition is attempted on a physical print delivery.
/// </summary>
public sealed class InvalidPhysicalPrintTransitionException : InvalidOperationException
{
    public InvalidPhysicalPrintTransitionException(string message)
        : base(message)
    {
    }

    public InvalidPhysicalPrintTransitionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when a reprint attempt is unauthorized or lacks operator approval.
/// </summary>
public sealed class UnauthorizedReprintException : InvalidOperationException
{
    public UnauthorizedReprintException(string message)
        : base(message)
    {
    }

    public UnauthorizedReprintException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
