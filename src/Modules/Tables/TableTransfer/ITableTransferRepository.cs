namespace ALKAROS.Tables.TableTransfer;

/// <summary>
/// Data access contract for table transfers (V1-TBL-002, PDF:III.5.3).
/// </summary>
public interface ITableTransferRepository
{
    /// <summary>
    /// Retrieves a historical table transfer record by ID.
    /// </summary>
    Task<TableTransferRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all historical transfers from the specified source table.
    /// </summary>
    Task<IReadOnlyList<TableTransferRecord>> GetBySourceTableAsync(Guid sourceTableId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all historical transfers into the specified target table.
    /// </summary>
    Task<IReadOnlyList<TableTransferRecord>> GetByTargetTableAsync(Guid targetTableId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes atomic table transfer in a single database transaction:
    /// validates source/target tables, verifies no payment data on bills,
    /// reparents open orders and unpaid bills, updates table states and pointers,
    /// and logs the immutable transfer record and audit event.
    /// </summary>
    Task<TableTransferResult> ExecuteTransferAsync(
        TableTransferRequest request,
        CancellationToken cancellationToken = default);
}
