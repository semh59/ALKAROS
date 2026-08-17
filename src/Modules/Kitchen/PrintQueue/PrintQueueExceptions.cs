namespace ALKAROS.Kitchen.PrintQueue;

/// <summary>
/// Thrown when optimistic concurrency check fails on a print job row.
/// </summary>
public sealed class PrintJobConcurrencyException : InvalidOperationException
{
    public PrintJobConcurrencyException(string message)
        : base(message)
    {
    }

    public PrintJobConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when an invalid lifecycle state transition is attempted on a print job.
/// </summary>
public sealed class InvalidPrintJobTransitionException : InvalidOperationException
{
    public InvalidPrintJobTransitionException(string message)
        : base(message)
    {
    }

    public InvalidPrintJobTransitionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when lease acquisition or fence validation fails.
/// </summary>
public sealed class PrintJobLeaseException : InvalidOperationException
{
    public PrintJobLeaseException(string message)
        : base(message)
    {
    }

    public PrintJobLeaseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
