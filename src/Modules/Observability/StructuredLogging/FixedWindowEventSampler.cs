namespace ALKAROS.Observability.StructuredLogging;

/// <summary>
/// Caps a given event name to at most <c>maxPerWindow</c> emissions per
/// rolling fixed <c>window</c>; the window resets the first time the name is
/// seen after the previous window has elapsed. Excess occurrences within the
/// same window are dropped — the caller's own work is never blocked or
/// failed by sampling (V15-OBS-001).
/// </summary>
public sealed class FixedWindowEventSampler : IEventSampler
{
    private readonly TimeSpan _window;
    private readonly int _maxPerWindow;
    private readonly Dictionary<string, (DateTimeOffset WindowStart, int Count)> _state = new();
    private readonly object _lock = new();

    public FixedWindowEventSampler(TimeSpan window, int maxPerWindow)
    {
        if (window <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(window), "Sampling window must be positive.");
        if (maxPerWindow <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPerWindow), "Sampling limit must be positive.");

        _window = window;
        _maxPerWindow = maxPerWindow;
    }

    public bool ShouldEmit(string eventName, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

        lock (_lock)
        {
            if (!_state.TryGetValue(eventName, out var entry) || now - entry.WindowStart >= _window)
            {
                _state[eventName] = (now, 1);
                return true;
            }

            if (entry.Count >= _maxPerWindow)
                return false;

            _state[eventName] = (entry.WindowStart, entry.Count + 1);
            return true;
        }
    }
}
