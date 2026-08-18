namespace ALKAROS.Tables.TableTransfer;

/// <summary>
/// Domain service interface for table transfer operations (V1-TBL-002).
/// </summary>
public interface ITableTransferService
{
    /// <summary>
    /// Executes transactional table transfer from source table to target table.
    /// </summary>
    Task<TableTransferResult> TransferTableAsync(
        TableTransferRequest request,
        CancellationToken cancellationToken = default);
}
