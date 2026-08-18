namespace ALKAROS.Tables.TableTransfer;

/// <summary>
/// Domain service implementing table transfer workflow (V1-TBL-002, PDF:I.9, PDF:II.2.3, PDF:II.5.15).
/// Validates transfer requirements and delegates execution to the transactional repository.
/// </summary>
public sealed class TableTransferService : ITableTransferService
{
    private readonly ITableTransferRepository _repository;

    public TableTransferService(ITableTransferRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<TableTransferResult> TransferTableAsync(
        TableTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        return await _repository.ExecuteTransferAsync(request, cancellationToken);
    }
}
