namespace ALKAROS.Tables.TableMerge;

/// <summary>
/// Base exception for table merge domain errors.
/// </summary>
public abstract class TableMergeException : Exception
{
    protected TableMergeException(string message) : base(message) { }
    protected TableMergeException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when attempting to merge a table with itself.
/// </summary>
public sealed class SameTableMergeException : TableMergeException
{
    public SameTableMergeException(Guid tableId)
        : base($"Primary table and participant table cannot be the same ({tableId}).")
    {
        TableId = tableId;
    }

    public Guid TableId { get; }
}

/// <summary>
/// Thrown when duplicate participant tables are provided in a merge request.
/// </summary>
public sealed class DuplicateMergeParticipantException : TableMergeException
{
    public DuplicateMergeParticipantException(Guid tableId)
        : base($"Duplicate participant table ID in merge request ({tableId}).")
    {
        TableId = tableId;
    }

    public Guid TableId { get; }
}

/// <summary>
/// Thrown when a table is not found.
/// </summary>
public sealed class TableNotFoundException : TableMergeException
{
    public TableNotFoundException(Guid tableId, string message) : base(message)
    {
        TableId = tableId;
    }

    public Guid TableId { get; }
}

/// <summary>
/// Thrown when a merge record or group is not found.
/// </summary>
public sealed class MergeRecordNotFoundException : TableMergeException
{
    public MergeRecordNotFoundException(Guid mergeGroupId, string message) : base(message)
    {
        MergeGroupId = mergeGroupId;
    }

    public Guid MergeGroupId { get; }
}

/// <summary>
/// Thrown when a table is in an invalid state for merge (e.g. Reserved, Cleaning, OutOfService, Inactive, or already merged).
/// </summary>
public sealed class InvalidTableMergeStateException : TableMergeException
{
    public InvalidTableMergeStateException(Guid tableId, string actualState, string reason)
        : base($"Table {tableId} is in '{actualState}' state; cannot be merged: {reason}")
    {
        TableId = tableId;
        ActualState = actualState;
        Reason = reason;
    }

    public Guid TableId { get; }
    public string ActualState { get; }
    public string Reason { get; }
}

/// <summary>
/// Thrown when an active bill on any participating table has payment progress (allocated, partially paid, paid, or non-Open status).
/// Merging tables with payment data requires V1.2 payment-aware table topology (V12-TBL-001).
/// </summary>
public sealed class PaymentPolicyRequiredException : TableMergeException
{
    public PaymentPolicyRequiredException(Guid billId, string message)
        : base(message)
    {
        BillId = billId;
    }

    public Guid BillId { get; }
}

/// <summary>
/// Thrown when an optimistic concurrency check fails on table row versions during merge or unmerge.
/// </summary>
public sealed class TableMergeConcurrencyException : TableMergeException
{
    public TableMergeConcurrencyException(Guid tableId, long expectedVersion, long actualVersion)
        : base($"Concurrency conflict on table {tableId}: expected row version {expectedVersion}, actual {actualVersion}.")
    {
        TableId = tableId;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }

    public Guid TableId { get; }
    public long ExpectedVersion { get; }
    public long ActualVersion { get; }
}
