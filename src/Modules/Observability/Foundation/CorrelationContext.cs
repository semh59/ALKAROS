namespace ALKAROS.Observability.Foundation;

/// <summary>
/// Ambient async-local correlation context for distributed tracing and request flow tracking (V1-OBS-001, PDF:II.2.25).
/// </summary>
public static class CorrelationContext
{
    private static readonly AsyncLocal<CorrelationData?> CurrentData = new();

    /// <summary>
    /// Gets the current ambient correlation data, or null if no scope is active.
    /// </summary>
    public static CorrelationData? Current => CurrentData.Value;

    /// <summary>
    /// Gets the active CorrelationId or generates a new one if unset.
    /// </summary>
    public static string CorrelationId => CurrentData.Value?.CorrelationId ?? Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets the active RequestId or generates a new one if unset.
    /// </summary>
    public static string RequestId => CurrentData.Value?.RequestId ?? Guid.NewGuid().ToString("N");

    /// <summary>
    /// Begins a new correlation scope, preserving parent context and restoring it upon disposal.
    /// </summary>
    public static IDisposable BeginScope(
        string? correlationId = null,
        string? requestId = null,
        Guid? userId = null,
        string? initialStep = null)
    {
        var parent = CurrentData.Value;
        var effectiveCorrelationId = !string.IsNullOrWhiteSpace(correlationId)
            ? correlationId
            : parent?.CorrelationId ?? Guid.NewGuid().ToString("N");

        var effectiveRequestId = !string.IsNullOrWhiteSpace(requestId)
            ? requestId
            : Guid.NewGuid().ToString("N");

        var effectiveUserId = userId ?? parent?.UserId;

        var chain = new List<string>(parent?.TraceChain ?? Array.Empty<string>());
        if (!string.IsNullOrWhiteSpace(initialStep))
        {
            chain.Add(initialStep);
        }

        var newData = new CorrelationData(effectiveCorrelationId, effectiveRequestId, effectiveUserId, chain);
        CurrentData.Value = newData;

        return new CorrelationScope(parent);
    }

    /// <summary>
    /// Appends a workflow step to the trace chain of the current correlation context.
    /// </summary>
    public static void AddTraceStep(string stepName)
    {
        if (string.IsNullOrWhiteSpace(stepName)) return;

        var current = CurrentData.Value;
        if (current is not null)
        {
            var updatedChain = new List<string>(current.TraceChain) { stepName };
            CurrentData.Value = current with { TraceChain = updatedChain };
        }
    }

    private sealed class CorrelationScope : IDisposable
    {
        private readonly CorrelationData? _parent;
        private bool _disposed;

        public CorrelationScope(CorrelationData? parent)
        {
            _parent = parent;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                CurrentData.Value = _parent;
                _disposed = true;
            }
        }
    }
}

/// <summary>
/// Immutable correlation data snapshot (V1-OBS-001).
/// </summary>
public sealed record CorrelationData(
    string CorrelationId,
    string RequestId,
    Guid? UserId,
    IReadOnlyList<string> TraceChain);
