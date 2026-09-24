using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ALKAROS.Host.Experience.SecurityAdministration.Maintenance;

public enum MaintenanceJobStatusKind
{
    NeverRun,
    Succeeded,
    Failed,
    Skipped,
}

/// <summary>What a single maintenance job execution reports back.</summary>
public sealed record MaintenanceJobOutcome(MaintenanceJobStatusKind Status, string Summary);

/// <summary>
/// V1-RMD-268: a scheduled, manager-triggerable operational job (retention
/// sweep, off-site backup upload, restore verification). Each was built and
/// DI-registered by a Faz 1 task but nothing in the running host ever called it.
/// A job is a singleton; it resolves everything it needs from the scoped
/// provider handed to <see cref="ExecuteAsync"/>.
/// </summary>
public interface IMaintenanceJob
{
    /// <summary>Stable kebab-case identity used in the API route and in log lines.</summary>
    string Name { get; }

    /// <summary>Turkish, human readable description shown to the manager.</summary>
    string Description { get; }

    /// <summary>How often the background timer runs the job.</summary>
    TimeSpan Interval { get; }

    /// <summary>
    /// Null when the job can run; otherwise the reason (Turkish) it cannot,
    /// e.g. a required setting is missing. A job that cannot run reports itself
    /// as such instead of silently succeeding.
    /// </summary>
    string? DisabledReason { get; }

    Task<MaintenanceJobOutcome> ExecuteAsync(IServiceProvider scopedServices, CancellationToken cancellationToken);
}

public sealed record MaintenanceJobStatusV1(
    string Name,
    string Description,
    int IntervalSeconds,
    bool Enabled,
    string? DisabledReason,
    string LastStatus,
    DateTimeOffset? LastRunAt,
    long? LastDurationMilliseconds,
    string? LastSummary);

/// <summary>
/// Runs jobs (from the timer or from the manager API), never overlapping a job
/// with itself, and remembers the last outcome per job. State is in memory: the
/// jobs themselves write their durable evidence (audit events, receipts).
/// </summary>
public sealed class MaintenanceJobRunner
{
    private static readonly Action<ILogger, string, string, Exception?> LogOutcome =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(5820, nameof(LogOutcome)),
            "Maintenance job {Job} finished: {Summary}");

    private static readonly Action<ILogger, string, Exception?> LogFault =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(5821, nameof(LogFault)),
            "Maintenance job {Job} failed.");

    private readonly IReadOnlyDictionary<string, IMaintenanceJob> _jobs;
    private readonly Dictionary<string, SemaphoreSlim> _gates;
    private readonly Dictionary<string, JobRecord> _records;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MaintenanceJobRunner> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly object _recordLock = new();

    public MaintenanceJobRunner(
        IEnumerable<IMaintenanceJob> jobs,
        IServiceScopeFactory scopeFactory,
        ILogger<MaintenanceJobRunner> logger,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _jobs = jobs.ToDictionary(job => job.Name, StringComparer.Ordinal);
        _gates = _jobs.Keys.ToDictionary(name => name, _ => new SemaphoreSlim(1, 1), StringComparer.Ordinal);
        _records = _jobs.Keys.ToDictionary(name => name, _ => new JobRecord(), StringComparer.Ordinal);
    }

    public IReadOnlyCollection<IMaintenanceJob> Jobs => _jobs.Values.ToList();

    public IReadOnlyList<MaintenanceJobStatusV1> GetStatuses()
    {
        lock (_recordLock)
        {
            return _jobs.Values
                .OrderBy(job => job.Name, StringComparer.Ordinal)
                .Select(job =>
                {
                    var record = _records[job.Name];
                    var disabled = job.DisabledReason;
                    return new MaintenanceJobStatusV1(
                        job.Name, job.Description, (int)job.Interval.TotalSeconds,
                        disabled is null, disabled,
                        record.Status.ToString(), record.LastRunAt, record.DurationMilliseconds, record.Summary);
                })
                .ToList();
        }
    }

    /// <summary>Runs one job now. Returns null for an unknown job name.</summary>
    public async Task<MaintenanceJobStatusV1?> RunAsync(string name, CancellationToken cancellationToken)
    {
        if (!_jobs.TryGetValue(name, out var job))
            return null;

        var gate = _gates[name];
        if (!await gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            Record(job, MaintenanceJobStatusKind.Skipped, "Bu iş zaten çalışıyor; ikinci bir çalıştırma başlatılmadı.", 0);
            return GetStatuses().First(status => status.Name == name);
        }

        var started = _timeProvider.GetTimestamp();
        try
        {
            var disabled = job.DisabledReason;
            if (disabled is not null)
            {
                Record(job, MaintenanceJobStatusKind.Skipped, disabled, 0);
            }
            else
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var outcome = await job.ExecuteAsync(scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
                    Record(job, outcome.Status, outcome.Summary, ElapsedMilliseconds(started));
                    LogOutcome(_logger, job.Name, outcome.Summary, null);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    // The message is recorded for the manager; the stack goes to the log only.
                    Record(job, MaintenanceJobStatusKind.Failed, $"İş başarısız oldu: {exception.GetType().Name}.", ElapsedMilliseconds(started));
                    LogFault(_logger, job.Name, exception);
                }
            }
        }
        finally
        {
            gate.Release();
        }

        return GetStatuses().First(status => status.Name == name);
    }

    private long ElapsedMilliseconds(long startedTimestamp)
        => (long)_timeProvider.GetElapsedTime(startedTimestamp).TotalMilliseconds;

    private void Record(IMaintenanceJob job, MaintenanceJobStatusKind status, string summary, long durationMilliseconds)
    {
        lock (_recordLock)
        {
            var record = _records[job.Name];
            record.Status = status;
            record.Summary = summary;
            record.LastRunAt = _timeProvider.GetUtcNow();
            record.DurationMilliseconds = durationMilliseconds;
        }
    }

    private sealed class JobRecord
    {
        public MaintenanceJobStatusKind Status { get; set; } = MaintenanceJobStatusKind.NeverRun;
        public string? Summary { get; set; }
        public DateTimeOffset? LastRunAt { get; set; }
        public long? DurationMilliseconds { get; set; }
    }
}

/// <summary>
/// Runs every enabled job on its own interval. The first run of each job waits
/// <see cref="InitialDelay"/> so a restart never fires all jobs at boot.
/// </summary>
public sealed class MaintenanceJobHostedService : BackgroundService
{
    public static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(10);

    private readonly MaintenanceJobRunner _runner;

    public MaintenanceJobHostedService(MaintenanceJobRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.WhenAll(_runner.Jobs.Select(job => LoopAsync(job, stoppingToken)));

    private async Task LoopAsync(IMaintenanceJob job, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, stoppingToken).ConfigureAwait(false);
            while (!stoppingToken.IsCancellationRequested)
            {
                await _runner.RunAsync(job.Name, stoppingToken).ConfigureAwait(false);
                await Task.Delay(job.Interval, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
