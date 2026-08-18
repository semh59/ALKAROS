namespace ALKAROS.Reporting.V1Operations;

/// <summary>
/// Base exception for reporting domain (V1-RPT-001).
/// </summary>
public abstract class ReportingException : Exception
{
    protected ReportingException(string message) : base(message) { }
    protected ReportingException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when attempting to open a business day when one is already open or for an existing date.
/// </summary>
public sealed class BusinessDayAlreadyOpenException : ReportingException
{
    public BusinessDayAlreadyOpenException(DateOnly businessDate)
        : base($"Business day for date '{businessDate:yyyy-MM-dd}' is already open.")
    {
        BusinessDate = businessDate;
    }

    public DateOnly BusinessDate { get; }
}

/// <summary>
/// Thrown when a business day record for a given date or ID is not found.
/// </summary>
public sealed class BusinessDayNotFoundException : ReportingException
{
    public BusinessDayNotFoundException(DateOnly businessDate)
        : base($"Business day for date '{businessDate:yyyy-MM-dd}' was not found.")
    {
        BusinessDate = businessDate;
    }

    public DateOnly BusinessDate { get; }
}

/// <summary>
/// Thrown when an invalid operational report action is performed (e.g. closing an already closed day).
/// </summary>
public sealed class InvalidBusinessDayOperationException : ReportingException
{
    public InvalidBusinessDayOperationException(string message) : base(message) { }
}
