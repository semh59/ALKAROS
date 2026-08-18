namespace ALKAROS.Reconciliation.CaseFoundation;

/// <summary>
/// Domain service interface for discrepancy case lifecycle, deduplication, and resolution (V1-REC-001).
/// </summary>
public interface IReconciliationService
{
    Task<ReconciliationCaseRecord> CreateOrDeduplicateCaseAsync(CreateCaseRequest request, CancellationToken cancellationToken = default);
    Task<ReconciliationCaseRecord> TransitionCaseStatusAsync(TransitionCaseStatusRequest request, CancellationToken cancellationToken = default);
    Task AddCaseNoteAsync(AddCaseNoteRequest request, CancellationToken cancellationToken = default);
    Task<ReconciliationCaseRecord?> GetCaseByIdAsync(Guid caseId, CancellationToken cancellationToken = default);
    Task<ReconciliationCaseRecord?> GetActiveCaseByDedupKeyAsync(string deduplicationKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CaseActionRecord>> GetCaseActionsAsync(Guid caseId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReconciliationCaseRecord>> GetCasesByStatusAsync(CaseStatus status, int limit = 50, CancellationToken cancellationToken = default);
}

/// <summary>
/// Domain service implementation for ReconciliationCase foundation (V1-REC-001).
/// </summary>
public sealed class ReconciliationService : IReconciliationService
{
    private readonly IReconciliationRepository _repository;

    public ReconciliationService(IReconciliationRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<ReconciliationCaseRecord> CreateOrDeduplicateCaseAsync(CreateCaseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        return _repository.CreateOrDeduplicateCaseAsync(request, cancellationToken);
    }

    public Task<ReconciliationCaseRecord> TransitionCaseStatusAsync(TransitionCaseStatusRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _repository.TransitionCaseStatusAsync(request, cancellationToken);
    }

    public Task AddCaseNoteAsync(AddCaseNoteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _repository.AddCaseNoteAsync(request, cancellationToken);
    }

    public Task<ReconciliationCaseRecord?> GetCaseByIdAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        return _repository.GetCaseByIdAsync(caseId, cancellationToken);
    }

    public Task<ReconciliationCaseRecord?> GetActiveCaseByDedupKeyAsync(string deduplicationKey, CancellationToken cancellationToken = default)
    {
        return _repository.GetActiveCaseByDedupKeyAsync(deduplicationKey, cancellationToken);
    }

    public Task<IReadOnlyList<CaseActionRecord>> GetCaseActionsAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        return _repository.GetCaseActionsAsync(caseId, cancellationToken);
    }

    public Task<IReadOnlyList<ReconciliationCaseRecord>> GetCasesByStatusAsync(CaseStatus status, int limit = 50, CancellationToken cancellationToken = default)
    {
        return _repository.GetCasesByStatusAsync(status, limit, cancellationToken);
    }
}
