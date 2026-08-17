namespace ALKAROS.Kitchen.Routing;

/// <summary>
/// Thrown when no active or configured printer can be reached through the fallback chain (V0-DOM-011 Rule 2).
/// </summary>
public sealed class NoAvailablePrinterException : InvalidOperationException
{
    public NoAvailablePrinterException(string message)
        : base(message)
    {
    }

    public NoAvailablePrinterException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when multiple routes match an item at the exact same specificity level (V0-DOM-011 Rule 3).
/// </summary>
public sealed class AmbiguousRouteException : InvalidOperationException
{
    public AmbiguousRouteException(string message)
        : base(message)
    {
    }

    public AmbiguousRouteException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when printer routing configuration fails static or atomic validation (V0-DOM-011 Section 4).
/// </summary>
public sealed class InvalidPrinterConfigurationException : InvalidOperationException
{
    public InvalidPrinterConfigurationException(string message)
        : base(message)
    {
    }

    public InvalidPrinterConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
