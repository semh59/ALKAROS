namespace ALKAROS.Tables.TableTransfer;

/// <summary>
/// Base exception for table transfer domain failures.
/// </summary>
public abstract class TableTransferException : Exception
{
    protected TableTransferException(string message) : base(message) { }
    protected TableTransferException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when source table and target table are identical.
/// </summary>
public sealed class SameTableTransferException : TableTransferException
{
    public SameTableTransferException(Guid tableId)
        : base($"Source table and target table cannot be the same ({tableId}).")
    {
        TableId = tableId;
    }

    public Guid TableId { get; }
}

/// <summary>
/// Thrown when source or target table is not found.
/// </summary>
public sealed class TableNotFoundException : TableTransferException
{
    public TableNotFoundException(Guid tableId, string message) : base(message)
    {
        TableId = tableId;
    }

    public Guid TableId { get; }
}

/// <summary>
/// Thrown when source table is not in the required Occupied state.
/// </summary>
public sealed class InvalidSourceTableStateException : TableTransferException
{
    public InvalidSourceTableStateException(Guid tableId, string actualState)
        : base($"Source table {tableId} is in '{actualState}' state; transfer requires 'Occupied' state.")
    {
        TableId = tableId;
        ActualState = actualState;
    }

    public Guid TableId { get; }
    public string ActualState { get; }
}

/// <summary>
/// Thrown when target table is not in the required Available state (e.g. Occupied, Reserved, Cleaning, OutOfService, or Inactive).
/// </summary>
public sealed class InvalidTargetTableStateException : TableTransferException
{
    public InvalidTargetTableStateException(Guid tableId, string actualState)
        : base($"Target table {tableId} is in '{actualState}' state; transfer requires 'Available' state.")
    {
        TableId = tableId;
        ActualState = actualState;
    }

    public Guid TableId { get; }
    public string ActualState { get; }
}

/// <summary>
/// Thrown when a Bill associated with the source table has payment data (allocations, partial payments, or non-Open status).
/// Moving partially-paid or allocated bills is deferred to V1.2 payment-aware table topology (V12-TBL-001).
/// </summary>
public sealed class PaymentPolicyRequiredException : TableTransferException
{
    public PaymentPolicyRequiredException(Guid billId, string message)
        : base(message)
    {
        BillId = billId;
    }

    public Guid BillId { get; }
}

/// <summary>
/// Thrown when optimistic concurrency fails on source or target table row version.
/// </summary>
public sealed class TableTransferConcurrencyException : TableTransferException
{
    public TableTransferConcurrencyException(Guid tableId, long expectedVersion, long actualVersion)
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
