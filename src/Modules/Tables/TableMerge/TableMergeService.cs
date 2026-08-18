namespace ALKAROS.Tables.TableMerge;

/// <summary>
/// Domain service interface for reversible table merge and unmerge orchestration (V1-TBL-003).
/// </summary>
public interface ITableMergeService
{
    /// <summary>
    /// Validates and executes an atomic multi-table merge operation.
    /// </summary>
    Task<TableMergeResult> MergeTablesAsync(
        TableMergeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and executes an atomic multi-table unmerge (undo) operation.
    /// </summary>
    Task<TableUnmergeResult> UnmergeTablesAsync(
        TableUnmergeRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Domain service implementation for table merge orchestration (V1-TBL-003).
/// </summary>
public sealed class TableMergeService : ITableMergeService
{
    private readonly ITableMergeRepository _repository;

    public TableMergeService(ITableMergeRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<TableMergeResult> MergeTablesAsync(
        TableMergeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        return _repository.ExecuteMergeAsync(request, cancellationToken);
    }

    public Task<TableUnmergeResult> UnmergeTablesAsync(
        TableUnmergeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        return _repository.ExecuteUnmergeAsync(request, cancellationToken);
    }
}
