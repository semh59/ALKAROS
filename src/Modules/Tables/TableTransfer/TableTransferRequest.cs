namespace ALKAROS.Tables.TableTransfer;

/// <summary>
/// Command request to transfer orders and unpaid bills from a source table to a target table (V1-TBL-002).
/// </summary>
public sealed record TableTransferRequest(
    Guid SourceTableId,
    long ExpectedSourceRowVersion,
    Guid TargetTableId,
    long ExpectedTargetRowVersion,
    string Reason,
    Guid TransferredBy,
    DateTimeOffset? TransferredAt = null)
{
    public void Validate()
    {
        if (SourceTableId == Guid.Empty)
            throw new ArgumentException("Source table ID cannot be empty.", nameof(SourceTableId));
        if (TargetTableId == Guid.Empty)
            throw new ArgumentException("Target table ID cannot be empty.", nameof(TargetTableId));
        if (SourceTableId == TargetTableId)
            throw new SameTableTransferException(SourceTableId);
        if (ExpectedSourceRowVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(ExpectedSourceRowVersion), "Expected source row version must be positive.");
        if (ExpectedTargetRowVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(ExpectedTargetRowVersion), "Expected target row version must be positive.");
        if (string.IsNullOrWhiteSpace(Reason))
            throw new ArgumentException("Transfer reason cannot be empty.", nameof(Reason));
        if (TransferredBy == Guid.Empty)
            throw new ArgumentException("TransferredBy user ID cannot be empty.", nameof(TransferredBy));
    }
}
