using System.Text.Json;

namespace ALKAROS.Host.Observability.CrashChannel;

/// <summary>Fixed-token notice only: no free text, so nothing personal can leave the system through it.</summary>
public sealed record CriticalNotice(string Layer, string Category, DateTimeOffset At);

public interface ICriticalNotifier
{
    /// <summary>Returns false when the channel did not accept the notice.</summary>
    Task<bool> SendAsync(CriticalNotice notice, CancellationToken cancellationToken);
}

/// <summary>Size-bounded line file; when full the oldest lines are dropped and counted.</summary>
public sealed class LocalFileBuffer(string path, long maxBytes = 5 * 1024 * 1024)
{
    private readonly object _gate = new();

    public long Dropped { get; private set; }

    public void Append(CriticalNotice notice)
    {
        var line = JsonSerializer.Serialize(notice);
        lock (_gate)
        {
            var lines = ReadLines();
            lines.Add(line);
            while (lines.Count > 1 && lines.Sum(l => l.Length + 2L) > maxBytes)
            {
                lines.RemoveAt(0);
                Dropped++;
            }

            WriteAtomically(lines);
        }
    }

    /// <summary>Unreadable lines (for example a write cut short by a crash) are skipped, never fatal.</summary>
    public IReadOnlyList<CriticalNotice> ReadAll()
    {
        lock (_gate)
        {
            var notices = new List<CriticalNotice>();
            foreach (var line in ReadLines())
            {
                try
                {
                    if (JsonSerializer.Deserialize<CriticalNotice>(line) is { } notice)
                        notices.Add(notice);
                }
                catch (JsonException)
                {
                    Dropped++;
                }
            }

            return notices;
        }
    }

    /// <summary>Removes only the oldest lines that were actually delivered, so notices appended meanwhile survive.</summary>
    public void RemoveFirst(int count)
    {
        lock (_gate)
        {
            var lines = ReadLines();
            WriteAtomically(lines.Skip(Math.Min(count, lines.Count)).ToList());
        }
    }

    private void WriteAtomically(List<string> lines)
    {
        var temp = path + ".tmp";
        File.WriteAllLines(temp, lines);
        File.Move(temp, path, overwrite: true);
    }

    public void Clear()
    {
        lock (_gate)
            File.Delete(path);
    }

    private List<string> ReadLines() => File.Exists(path) ? [.. File.ReadAllLines(path)] : [];
}

/// <summary>Opens after consecutive failures and lets one attempt through after the cool-down.</summary>
public sealed class CircuitBreaker(int failureThreshold, TimeSpan coolDown, Func<DateTimeOffset>? clock = null)
{
    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);
    private int _failures;
    private DateTimeOffset _openedAt;

    private readonly object _gate = new();

    public bool IsOpen
    {
        get
        {
            lock (_gate)
                return _failures >= failureThreshold && _clock() - _openedAt < coolDown;
        }
    }

    public void Success()
    {
        lock (_gate)
            _failures = 0;
    }

    public void Failure()
    {
        lock (_gate)
        {
            _failures++;
            if (_failures >= failureThreshold)
                _openedAt = _clock();
        }
    }
}

/// <summary>Critical events reach the notifier directly, bypassing the database queue; failures land in the file buffer.</summary>
public sealed class CriticalOutageChannel(ICriticalNotifier notifier, LocalFileBuffer buffer, CircuitBreaker breaker)
{
    public async Task<bool> ReportAsync(CriticalNotice notice, CancellationToken cancellationToken = default)
    {
        if (!breaker.IsOpen && await TrySendAsync(notice, cancellationToken))
            return true;

        buffer.Append(notice);
        return false;
    }

    /// <summary>Resends buffered notices once the channel works again; returns how many went out.</summary>
    public async Task<int> DrainAsync(CancellationToken cancellationToken = default)
    {
        if (breaker.IsOpen)
            return 0;

        var pending = buffer.ReadAll();
        var sent = 0;
        foreach (var notice in pending)
        {
            if (!await TrySendAsync(notice, cancellationToken))
                break;
            sent++;
        }

        if (sent > 0)
            buffer.RemoveFirst(sent);
        return sent;
    }

    internal static bool IsTransportFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException or TimeoutException
        || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);

    private async Task<bool> TrySendAsync(CriticalNotice notice, CancellationToken cancellationToken)
    {
        bool accepted;
        try
        {
            accepted = await notifier.SendAsync(notice, cancellationToken);
        }
        catch (Exception exception) when (IsTransportFailure(exception, cancellationToken))
        {
            accepted = false;
        }

        if (accepted)
            breaker.Success();
        else
            breaker.Failure();
        return accepted;
    }
}

/// <summary>Out-of-process watcher logic: one alarm per outage after consecutive failed probes, a second one on recovery is not sent.</summary>
public sealed class OutageWatchdog(
    Func<CancellationToken, Task<bool>> probe, CriticalOutageChannel channel, int failuresBeforeAlarm = 2, Func<DateTimeOffset>? clock = null)
{
    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);
    private int _failures;
    private bool _alarmed;

    public long Heartbeats { get; private set; }

    public async Task CheckOnceAsync(CancellationToken cancellationToken = default)
    {
        Heartbeats++;
        bool healthy;
        try
        {
            healthy = await probe(cancellationToken);
        }
        catch (Exception exception) when (CriticalOutageChannel.IsTransportFailure(exception, cancellationToken))
        {
            healthy = false;
        }

        if (healthy)
        {
            _failures = 0;
            _alarmed = false;
            await channel.DrainAsync(cancellationToken);
            return;
        }

        _failures++;
        if (_failures >= failuresBeforeAlarm && !_alarmed)
        {
            _alarmed = true;
            await channel.ReportAsync(new CriticalNotice("Api", "system.unreachable", _clock()), cancellationToken);
        }
    }

    public static Func<CancellationToken, Task<bool>> HttpProbe(HttpClient client, Uri healthUrl) =>
        async token =>
        {
            using var response = await client.GetAsync(healthUrl, token);
            return response.IsSuccessStatusCode;
        };
}
