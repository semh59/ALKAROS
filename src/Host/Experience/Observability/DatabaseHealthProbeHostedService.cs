using ALKAROS.Observability.Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ALKAROS.Host.Experience.Observability;

/// <summary>
/// V1-RMD-326 (independent 2026-09-26 audit, finding K14): <c>POST /health-checks</c> only ever records
/// whatever <see cref="ALKAROS.Observability.Foundation.HealthStatus"/> the CALLER claims - nothing in the
/// running Host ever independently probed anything real before this fix. This is the first genuine probe:
/// a periodic, real Postgres round trip (<c>SELECT 1</c>), recorded through the exact same
/// <see cref="IObservabilityService.RecordHealthCheckAsync"/> path a caller would use, so it appears in
/// <c>GET /health-checks/unhealthy</c> and <c>GET /health-checks/by-target</c> exactly like any other
/// check. Deliberately narrow in scope (database connectivity only, not disk/external services) - a real,
/// working probe for the single most load-bearing dependency, not a claim to have solved observability
/// end to end.
/// </summary>
public sealed class DatabaseHealthProbeHostedService : BackgroundService
{
    public const string CheckType = "Database";
    public const string Target = "postgres";

    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private static readonly Action<ILogger, Exception> LogProbeFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(5920, nameof(LogProbeFailed)),
            "Database health probe failed to even record its own result; retrying after the interval.");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<DatabaseHealthProbeHostedService> _logger;

    public DatabaseHealthProbeHostedService(
        IServiceScopeFactory scopeFactory, NpgsqlDataSource dataSource, ILogger<DatabaseHealthProbeHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProbeAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Recording the probe's OWN failure to record must never crash this loop - the next tick
                // tries again regardless (same resilience shape LowStockAlertHostedService's own loop uses).
                LogProbeFailed(_logger, ex);
            }

            try
            {
                await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Public so a test can invoke one probe deterministically instead of waiting on <see cref="Interval"/>.</summary>
    public async Task ProbeAsync(CancellationToken cancellationToken)
    {
        var (status, detailsJson) = await RunProbeAsync(cancellationToken).ConfigureAwait(false);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var observability = scope.ServiceProvider.GetRequiredService<IObservabilityService>();
        await observability.RecordHealthCheckAsync(
            new RecordHealthCheckRequest(CheckType, Target, status, RetentionPolicyCatalog.HotOperational7D, detailsJson),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<(HealthStatus Status, string? DetailsJson)> RunProbeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return (HealthStatus.Healthy, null);
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
        {
            // The exception's own message, not its full ToString() - a real probe result should say WHY
            // it failed without carrying arbitrary connection-string/stack-trace detail into a persisted row.
            return (HealthStatus.Unhealthy, $$"""{"error":"{{ex.Message.Replace("\"", "'")}}"}""");
        }
    }
}
