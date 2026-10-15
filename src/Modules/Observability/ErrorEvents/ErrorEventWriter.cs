using System.Threading.Channels;
using ALKAROS.Observability.TelemetryPrivacy;
using Npgsql;

namespace ALKAROS.Observability.ErrorEvents;

/// <summary>Told about fingerprints that are new or got more severe, so an alert can be raised once.</summary>
public interface IErrorEventChangeSink
{
    Task OnChangesAsync(IReadOnlyList<ErrorEventChange> changes, CancellationToken cancellationToken);
}

/// <summary>
/// Collects errors in a bounded in-memory queue and writes them in batches, merged by fingerprint: a storm of
/// one error becomes one upsert, not one row per occurrence. Writing never throws into the caller; events that
/// cannot be written are counted, never silently lost.
/// </summary>
public sealed class ErrorEventWriter : IAsyncDisposable
{
    public const string DroppedCounter = "dropped_events";
    public const string FailedWritesCounter = "failed_write_events";

    private readonly NpgsqlDataSource _dataSource;
    private readonly Channel<ErrorEventInput> _queue;
    private readonly IErrorEventChangeSink? _sink;
    private readonly int _maxBatch;
    private readonly TimeSpan _flushInterval;
    private readonly Func<DateTimeOffset> _clock;
    private long _dropped;
    private long _failed;
    private CancellationTokenSource? _loopCts;
    private Task? _loop;

    public ErrorEventWriter(
        NpgsqlDataSource dataSource,
        int capacity = 5000,
        int maxBatch = 500,
        TimeSpan? flushInterval = null,
        IErrorEventChangeSink? sink = null,
        Func<DateTimeOffset>? clock = null)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _queue = Channel.CreateBounded<ErrorEventInput>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });
        _maxBatch = maxBatch;
        _flushInterval = flushInterval ?? TimeSpan.FromSeconds(2);
        _sink = sink;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Events dropped because the queue was full, not yet written to the counters table.</summary>
    public long DroppedPending => Interlocked.Read(ref _dropped);

    /// <summary>Events lost to a failed database write, not yet written to the counters table.</summary>
    public long FailedPending => Interlocked.Read(ref _failed);

    /// <summary>Queues one event; false (and a counted drop) when the queue is full.</summary>
    public bool TryEnqueue(ErrorEventInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (_queue.Writer.TryWrite(input))
            return true;
        Interlocked.Increment(ref _dropped);
        return false;
    }

    public void Start()
    {
        if (_loop is not null)
            return;
        _loopCts = new CancellationTokenSource();
        var token = _loopCts.Token;
        _loop = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_flushInterval, token).ConfigureAwait(false);
                    await FlushAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        });
    }

    /// <summary>Writes everything currently queued (in batches). Never throws; failures are counted.</summary>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var batch = new List<ErrorEventInput>(_maxBatch);
            while (batch.Count < _maxBatch && _queue.Reader.TryRead(out var item))
                batch.Add(item);

            var dropped = Interlocked.Exchange(ref _dropped, 0);
            if (batch.Count == 0 && dropped == 0)
                return;

            try
            {
                var changes = await WriteBatchAsync(batch, dropped, cancellationToken).ConfigureAwait(false);
                if (_sink is not null && changes.Count > 0)
                    await _sink.OnChangesAsync(changes, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                Interlocked.Add(ref _failed, batch.Count);
                Interlocked.Add(ref _dropped, dropped);
            }

            if (batch.Count < _maxBatch)
                return;
        }
    }

    private async Task<IReadOnlyList<ErrorEventChange>> WriteBatchAsync(
        List<ErrorEventInput> batch, long dropped, CancellationToken cancellationToken)
    {
        var merged = new Dictionary<string, Merged>(StringComparer.Ordinal);
        foreach (var input in batch)
        {
            var detail = Clean(input.Detail);
            var key = ErrorFingerprint.Compute(input.Layer, input.Category, detail);
            var at = input.OccurredAt ?? _clock();
            if (merged.TryGetValue(key, out var m))
                m.Add(input, at);
            else
                merged[key] = new Merged(input, detail, at);
        }

        var changes = new List<ErrorEventChange>();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        foreach (var (fingerprint, m) in merged)
        {
            string? previousSeverity = null;
            await using (var read = new NpgsqlCommand(
                "SELECT severity FROM observability.error_events WHERE fingerprint = $1;", connection, transaction))
            {
                read.Parameters.AddWithValue(fingerprint);
                previousSeverity = (string?)await read.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var upsert = new NpgsqlCommand(
                """
                INSERT INTO observability.error_events
                    (fingerprint, layer, severity, category, detail, first_seen_at, last_seen_at, occurrence_count,
                     last_trace_id, last_terminal_label, synthetic)
                VALUES ($1, $2, $3, $4, $5, $6, $6, $7, $8, $9, $10)
                ON CONFLICT (fingerprint) DO UPDATE SET
                    occurrence_count = observability.error_events.occurrence_count + EXCLUDED.occurrence_count,
                    last_seen_at = GREATEST(observability.error_events.last_seen_at, EXCLUDED.last_seen_at),
                    severity = CASE
                        WHEN array_position(ARRAY['Info','Warning','Error','Critical'], EXCLUDED.severity)
                           > array_position(ARRAY['Info','Warning','Error','Critical'], observability.error_events.severity)
                        THEN EXCLUDED.severity ELSE observability.error_events.severity END,
                    status = CASE WHEN observability.error_events.status = 'Resolved' THEN 'Open'
                                  ELSE observability.error_events.status END,
                    resolved_at = CASE WHEN observability.error_events.status = 'Resolved' THEN NULL
                                       ELSE observability.error_events.resolved_at END,
                    last_trace_id = COALESCE(EXCLUDED.last_trace_id, observability.error_events.last_trace_id),
                    last_terminal_label = COALESCE(EXCLUDED.last_terminal_label, observability.error_events.last_terminal_label);
                """, connection, transaction))
            {
                upsert.Parameters.AddWithValue(fingerprint);
                upsert.Parameters.AddWithValue(m.Layer.ToString());
                upsert.Parameters.AddWithValue(m.Severity.ToString());
                upsert.Parameters.AddWithValue(m.Category);
                upsert.Parameters.AddWithValue((object?)m.Detail ?? DBNull.Value).NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Text;
                upsert.Parameters.AddWithValue(m.LastSeen.UtcDateTime);
                upsert.Parameters.AddWithValue(m.Count);
                upsert.Parameters.AddWithValue((object?)m.TraceId ?? DBNull.Value).NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Text;
                upsert.Parameters.AddWithValue((object?)m.TerminalLabel ?? DBNull.Value).NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Text;
                upsert.Parameters.AddWithValue(m.Synthetic);
                await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var occurrence = new NpgsqlCommand(
                """
                INSERT INTO observability.error_occurrences (fingerprint, occurred_at, severity, occurrences, trace_id)
                VALUES ($1, $2, $3, $4, $5);
                """, connection, transaction))
            {
                occurrence.Parameters.AddWithValue(fingerprint);
                occurrence.Parameters.AddWithValue(m.LastSeen.UtcDateTime);
                occurrence.Parameters.AddWithValue(m.Severity.ToString());
                occurrence.Parameters.AddWithValue(m.Count);
                occurrence.Parameters.AddWithValue((object?)m.TraceId ?? DBNull.Value).NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Text;
                await occurrence.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            var isNew = previousSeverity is null;
            var escalated = !isNew && Enum.Parse<ErrorSeverity>(previousSeverity!) < m.Severity;
            if (isNew || escalated)
                changes.Add(new ErrorEventChange(fingerprint, m.Layer, m.Severity, m.Category, m.Detail, isNew, escalated, m.Count));
        }

        var failed = Interlocked.Exchange(ref _failed, 0);
        await AddCounterAsync(connection, transaction, DroppedCounter, dropped, cancellationToken).ConfigureAwait(false);
        await AddCounterAsync(connection, transaction, FailedWritesCounter, failed, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return changes;
    }

    private static async Task AddCounterAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string name, long value, CancellationToken cancellationToken)
    {
        if (value <= 0)
            return;
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO observability.error_event_counters (name, value) VALUES ($1, $2)
            ON CONFLICT (name) DO UPDATE SET value = observability.error_event_counters.value + EXCLUDED.value;
            """, connection, transaction);
        command.Parameters.AddWithValue(name);
        command.Parameters.AddWithValue(value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string? Clean(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
            return null;
        var cleaned = TelemetryPrivacyGuard.Sanitize(new Dictionary<string, string?> { ["error_type"] = detail });
        return cleaned.TryGetValue("error_type", out var value) ? value : null;
    }

    public async ValueTask DisposeAsync()
    {
        _loopCts?.Cancel();
        if (_loop is not null)
            await Task.WhenAny(_loop).ConfigureAwait(false);

        _loopCts?.Dispose();
    }

    private sealed class Merged
    {
        public Merged(ErrorEventInput first, string? detail, DateTimeOffset at)
        {
            Layer = first.Layer;
            Category = first.Category;
            Detail = detail;
            Severity = first.Severity;
            LastSeen = at;
            TraceId = first.TraceId;
            TerminalLabel = first.TerminalLabel;
            Synthetic = first.Synthetic;
            Count = 1;
        }

        public ErrorLayer Layer { get; }
        public string Category { get; }
        public string? Detail { get; }
        public ErrorSeverity Severity { get; private set; }
        public DateTimeOffset LastSeen { get; private set; }
        public string? TraceId { get; private set; }
        public string? TerminalLabel { get; private set; }
        public bool Synthetic { get; private set; }
        public int Count { get; private set; }

        public void Add(ErrorEventInput input, DateTimeOffset at)
        {
            Count++;
            if (input.Severity > Severity) Severity = input.Severity;
            if (at > LastSeen) LastSeen = at;
            TraceId = input.TraceId ?? TraceId;
            TerminalLabel = input.TerminalLabel ?? TerminalLabel;
            Synthetic &= input.Synthetic;
        }
    }
}
