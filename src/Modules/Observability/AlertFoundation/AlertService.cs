namespace ALKAROS.Observability.AlertFoundation;

/// <summary>
/// Domain service interface for raising and managing alert lifecycle (V1-ALT-001).
/// </summary>
public interface IAlertService
{
    Task<AlertRecord?> GetByIdAsync(Guid alertId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertRecord>> GetActiveAlertsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertRecord>> GetBySourceReferenceAsync(string sourceReferenceType, Guid sourceReferenceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertEventRecord>> GetEventsAsync(Guid alertId, CancellationToken cancellationToken = default);
    Task<AlertRaiseResult> RaiseAlertAsync(RaiseAlertRequest request, CancellationToken cancellationToken = default);
    Task<AlertRecord> AcknowledgeAlertAsync(AcknowledgeAlertRequest request, CancellationToken cancellationToken = default);
    Task<AlertRecord> EscalateAlertAsync(EscalateAlertRequest request, CancellationToken cancellationToken = default);
    Task<AlertRecord> SuppressAlertAsync(SuppressAlertRequest request, CancellationToken cancellationToken = default);
    Task<AlertRecord> ResolveAlertAsync(ResolveAlertRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Domain service implementation for alerts orchestration (V1-ALT-001).
/// </summary>
public sealed class AlertService : IAlertService
{
    private readonly IAlertRepository _repository;

    public AlertService(IAlertRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<AlertRecord?> GetByIdAsync(Guid alertId, CancellationToken cancellationToken = default)
    {
        return _repository.GetByIdAsync(alertId, cancellationToken);
    }

    public Task<IReadOnlyList<AlertRecord>> GetActiveAlertsAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetActiveAlertsAsync(cancellationToken);
    }

    public Task<IReadOnlyList<AlertRecord>> GetBySourceReferenceAsync(string sourceReferenceType, Guid sourceReferenceId, CancellationToken cancellationToken = default)
    {
        return _repository.GetBySourceReferenceAsync(sourceReferenceType, sourceReferenceId, cancellationToken);
    }

    public Task<IReadOnlyList<AlertEventRecord>> GetEventsAsync(Guid alertId, CancellationToken cancellationToken = default)
    {
        return _repository.GetEventsAsync(alertId, cancellationToken);
    }

    public Task<AlertRaiseResult> RaiseAlertAsync(RaiseAlertRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        return _repository.RaiseAlertAsync(request, cancellationToken);
    }

    public Task<AlertRecord> AcknowledgeAlertAsync(AcknowledgeAlertRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        return _repository.AcknowledgeAlertAsync(request, cancellationToken);
    }

    public Task<AlertRecord> EscalateAlertAsync(EscalateAlertRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        return _repository.EscalateAlertAsync(request, cancellationToken);
    }

    public Task<AlertRecord> SuppressAlertAsync(SuppressAlertRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        return _repository.SuppressAlertAsync(request, cancellationToken);
    }

    public Task<AlertRecord> ResolveAlertAsync(ResolveAlertRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        return _repository.ResolveAlertAsync(request, cancellationToken);
    }
}
