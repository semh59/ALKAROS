namespace ALKAROS.Reconciliation.CaseFoundation;

/// <summary>
/// Base exception for Reconciliation domain (V1-REC-001).
/// </summary>
public abstract class ReconciliationException : Exception
{
    protected ReconciliationException(string message) : base(message) { }
    protected ReconciliationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when a specified reconciliation case is not found.
/// </summary>
public sealed class CaseNotFoundException : ReconciliationException
{
    public CaseNotFoundException(Guid caseId)
        : base($"Reconciliation case '{caseId}' was not found.")
    {
        CaseId = caseId;
    }

    public Guid CaseId { get; }
}

/// <summary>
/// Thrown when an illegal state transition is attempted on a case.
/// </summary>
public sealed class InvalidCaseStatusTransitionException : ReconciliationException
{
    public InvalidCaseStatusTransitionException(CaseStatus fromStatus, CaseStatus toStatus)
        : base($"Invalid reconciliation case transition from '{fromStatus}' to '{toStatus}'.")
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
    }

    public CaseStatus FromStatus { get; }
    public CaseStatus ToStatus { get; }
}

/// <summary>
/// Thrown when a concurrent update causes a row_version mismatch.
/// </summary>
public sealed class ReconciliationConcurrencyException : ReconciliationException
{
    public ReconciliationConcurrencyException(Guid caseId, int expectedVersion)
        : base($"Concurrency conflict on reconciliation case '{caseId}'. Expected version: {expectedVersion}.")
    {
        CaseId = caseId;
        ExpectedVersion = expectedVersion;
    }

    public Guid CaseId { get; }
    public int ExpectedVersion { get; }
}
