namespace ALKAROS.Observability.Foundation;

/// <summary>
/// Domain service interface for correlation context, redaction, and health status auditing (V1-OBS-001).
/// </summary>
public interface IObservabilityService
{
    IDisposable BeginCorrelationScope(string? correlationId = null, string? requestId = null, Guid? userId = null, string? initialStep = null);
    void AddTraceStep(string stepName);
    string RedactPayload(string? json);
    Task<HealthCheckRecord> RecordHealthCheckAsync(RecordHealthCheckRequest request, CancellationToken cancellationToken = default);
    Task<HealthCheckRecord?> GetHealthCheckByIdAsync(Guid healthCheckId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HealthCheckRecord>> GetLatestHealthChecksByTargetAsync(string target, int limit = 10, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HealthCheckRecord>> GetUnhealthyChecksAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Domain service implementation for Observability Foundation (V1-OBS-001).
/// </summary>
public sealed class ObservabilityService : IObservabilityService
{
    private readonly IHealthCheckRepository _repository;
    private readonly IRedactionHook _redactionHook;

    public ObservabilityService(
        IHealthCheckRepository repository,
        IRedactionHook redactionHook)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _redactionHook = redactionHook ?? throw new ArgumentNullException(nameof(redactionHook));
    }

    public IDisposable BeginCorrelationScope(string? correlationId = null, string? requestId = null, Guid? userId = null, string? initialStep = null)
    {
        return CorrelationContext.BeginScope(correlationId, requestId, userId, initialStep);
    }

    public void AddTraceStep(string stepName)
    {
        CorrelationContext.AddTraceStep(stepName);
    }

    public string RedactPayload(string? json)
    {
        return _redactionHook.RedactJson(json);
    }

    public Task<HealthCheckRecord> RecordHealthCheckAsync(RecordHealthCheckRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        return _repository.RecordHealthCheckAsync(request, cancellationToken);
    }

    public Task<HealthCheckRecord?> GetHealthCheckByIdAsync(Guid healthCheckId, CancellationToken cancellationToken = default)
    {
        return _repository.GetByIdAsync(healthCheckId, cancellationToken);
    }

    public Task<IReadOnlyList<HealthCheckRecord>> GetLatestHealthChecksByTargetAsync(string target, int limit = 10, CancellationToken cancellationToken = default)
    {
        return _repository.GetLatestByTargetAsync(target, limit, cancellationToken);
    }

    public Task<IReadOnlyList<HealthCheckRecord>> GetUnhealthyChecksAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetUnhealthyChecksAsync(cancellationToken);
    }
}
