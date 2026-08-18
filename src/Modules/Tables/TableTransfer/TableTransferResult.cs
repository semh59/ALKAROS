namespace ALKAROS.Tables.TableTransfer;

/// <summary>
/// Result of an executed table transfer (V1-TBL-002).
/// </summary>
public sealed record TableTransferResult(
    Guid TransferId,
    Guid SourceTableId,
    long NewSourceRowVersion,
    Guid TargetTableId,
    long NewTargetRowVersion,
    IReadOnlyList<Guid> TransferredOrderIds,
    IReadOnlyList<Guid> TransferredBillIds,
    DateTimeOffset TransferredAt);
