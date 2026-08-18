namespace ALKAROS.Tables.TableTransfer;

/// <summary>
/// An immutable record of a table transfer (table_mgmt.table_transfers, PDF:I.9, PDF:III.5.3).
/// Records historical provenance when operational Order/Bill associations are moved between tables.
/// </summary>
public sealed class TableTransferRecord
{
    public TableTransferRecord(
        Guid id,
        Guid sourceTableId,
        Guid targetTableId,
        Guid? orderId,
        Guid? billId,
        string reason,
        Guid transferredBy,
        DateTimeOffset transferredAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Transfer id cannot be empty.", nameof(id));
        if (sourceTableId == Guid.Empty)
            throw new ArgumentException("Source table id cannot be empty.", nameof(sourceTableId));
        if (targetTableId == Guid.Empty)
            throw new ArgumentException("Target table id cannot be empty.", nameof(targetTableId));
        if (sourceTableId == targetTableId)
            throw new ArgumentException("Source table and target table cannot be the same.", nameof(targetTableId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason cannot be empty.", nameof(reason));
        if (transferredBy == Guid.Empty)
            throw new ArgumentException("TransferredBy user id cannot be empty.", nameof(transferredBy));

        Id = id;
        SourceTableId = sourceTableId;
        TargetTableId = targetTableId;
        OrderId = orderId;
        BillId = billId;
        Reason = reason;
        TransferredBy = transferredBy;
        TransferredAt = transferredAt;
    }

    public Guid Id { get; }

    public Guid SourceTableId { get; }

    public Guid TargetTableId { get; }

    public Guid? OrderId { get; }

    public Guid? BillId { get; }

    public string Reason { get; }

    public Guid TransferredBy { get; }

    public DateTimeOffset TransferredAt { get; }
}
