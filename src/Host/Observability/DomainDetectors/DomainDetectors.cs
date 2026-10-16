using ALKAROS.Host.Observability.Wiring;
using ALKAROS.Observability.ErrorAnalysis;
using ALKAROS.Observability.ErrorEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace ALKAROS.Host.Observability.DomainDetectors;

/// <summary>Thresholds in minutes unless named otherwise; bound from "Monitoring:DomainDetectors".</summary>
public sealed class DomainDetectorOptions
{
    public int PrintFailureWindowMinutes { get; set; } = 15;
    public int PrintStuckMinutes { get; set; } = 5;
    public int PhysicalPrintUnknownMinutes { get; set; } = 5;
    public int PaymentUnknownCriticalMinutes { get; set; } = 5;
    public int KitchenQueuedMinutes { get; set; } = 5;
    public int ProviderFailureStreakForError { get; set; } = 5;
    public int DeliveryStuckMinutes { get; set; } = 10;
    public int BackupMaxAgeHours { get; set; } = 26;
    public int FiscalRpoMinutes { get; set; } = 5;
    public int OrdersInventoryRpoMinutes { get; set; } = 60;
    public int SettingsRpoMinutes { get; set; } = 1440;
}

/// <summary>
/// One read-only check: each returned row (severity, detail) is an active condition; when a check returns nothing,
/// its open events are resolved. Details are fixed phrases (plus a provider or data-class name), never row data.
/// </summary>
internal sealed record DomainCheck(ErrorLayer Layer, string Category, string Sql, Action<NpgsqlCommand, DomainDetectorOptions> Bind);

public sealed class DomainDetectorRunner(
    NpgsqlDataSource dataSource, ErrorEventWriter writer, DomainDetectorOptions? options = null,
    DetectorHeartbeats? heartbeats = null, TimeSpan? interval = null) : BackgroundService
{
    public const string HeartbeatName = "domain_detectors";

    private readonly DomainDetectorOptions _options = options ?? new DomainDetectorOptions();

    public long Failures { get; private set; }

    private static void NoBinding(NpgsqlCommand command, DomainDetectorOptions options)
    {
    }

    private static Action<NpgsqlCommand, DomainDetectorOptions> Minutes(string name, Func<DomainDetectorOptions, int> value) =>
        (command, options) => command.Parameters.AddWithValue(name, value(options));

    private static readonly DomainCheck[] Checks =
    [
        new(ErrorLayer.Printer, "printer.job_failed",
            """
            SELECT 2, 'print job failed' FROM kitchen.print_jobs
            WHERE status IN ('Failed', 'DeadLetter') AND coalesce(failed_at, updated_at, created_at) > now() - make_interval(mins => @m)
            LIMIT 1;
            """, Minutes("m", o => o.PrintFailureWindowMinutes)),
        new(ErrorLayer.Printer, "printer.queue_stuck",
            """
            SELECT 1, 'print queue stuck' FROM kitchen.print_jobs
            WHERE status IN ('Pending', 'Leased', 'Printing') AND created_at < now() - make_interval(mins => @m)
            LIMIT 1;
            """, Minutes("m", o => o.PrintStuckMinutes)),
        new(ErrorLayer.Printer, "printer.delivery_unknown",
            """
            SELECT 2, 'physical print outcome unknown' FROM kitchen.physical_print_deliveries
            WHERE status = 'Unknown' AND resolved_at IS NULL AND created_at < now() - make_interval(mins => @m)
            LIMIT 1;
            """, Minutes("m", o => o.PhysicalPrintUnknownMinutes)),
        new(ErrorLayer.Payment, "payment.unknown_outcome",
            """
            SELECT CASE WHEN min(updated_at) < now() - make_interval(mins => @m) THEN 3 ELSE 1 END, 'payment outcome unknown'
            FROM payments.payments WHERE status = 'Unknown' HAVING count(*) > 0;
            """, Minutes("m", o => o.PaymentUnknownCriticalMinutes)),
        new(ErrorLayer.Payment, "payment.reconciliation_required",
            """
            SELECT 1, 'payment needs reconciliation' FROM payments.payments WHERE status = 'ReconciliationRequired' LIMIT 1;
            """, NoBinding),
        new(ErrorLayer.Kitchen, "kitchen.ticket_not_accepted",
            """
            SELECT 2, 'kitchen ticket waiting' FROM kitchen.kitchen_tickets
            WHERE status = 'Queued' AND created_at < now() - make_interval(mins => @m) LIMIT 1;
            """, Minutes("m", o => o.KitchenQueuedMinutes)),
        new(ErrorLayer.Integration, "integration.provider_poll_failing",
            """
            SELECT CASE WHEN consecutive_failures >= @streak THEN 2 ELSE 1 END, 'provider=' || provider
            FROM online_ordering.provider_poll_state WHERE consecutive_failures > 0;
            """, (command, o) => command.Parameters.AddWithValue("streak", o.ProviderFailureStreakForError)),
        new(ErrorLayer.Integration, "integration.store_status_undelivered",
            """
            SELECT 1, 'provider=' || provider FROM online_ordering.platform_store_status
            WHERE delivered_at IS NULL AND last_error IS NOT NULL AND requested_at < now() - make_interval(mins => @m);
            """, Minutes("m", o => o.DeliveryStuckMinutes)),
        new(ErrorLayer.Integration, "integration.catalog_undelivered",
            """
            SELECT 1, 'channel=' || channel FROM online_ordering.catalog_publications
            WHERE status = 'Pending' AND last_error IS NOT NULL AND requested_at < now() - make_interval(mins => @m)
            GROUP BY channel;
            """, Minutes("m", o => o.DeliveryStuckMinutes)),
        new(ErrorLayer.Backup, "backup.failed",
            """
            SELECT 2, 'latest backup failed' FROM (
                SELECT status FROM operations.backups ORDER BY started_at DESC LIMIT 1) latest WHERE status = 'Failed';
            """, NoBinding),
        new(ErrorLayer.Backup, "backup.stale",
            """
            SELECT 1, 'no recent completed backup' FROM operations.backups
            HAVING count(*) > 0
               AND coalesce(max(completed_at) FILTER (WHERE status = 'Completed'), '-infinity') < now() - make_interval(hours => @h);
            """, Minutes("h", o => o.BackupMaxAgeHours)),
        new(ErrorLayer.Backup, "backup.rpo_exceeded",
            """
            SELECT 2, 'data class=' || r.data_class FROM operations.offsite_backup_receipts r
            GROUP BY r.data_class
            HAVING max(r.uploaded_at) < now() - make_interval(mins => CASE r.data_class
                WHEN 'Fiscal' THEN @fiscal WHEN 'OrdersInventory' THEN @orders ELSE @settings END);
            """, (command, o) =>
            {
                command.Parameters.AddWithValue("fiscal", o.FiscalRpoMinutes);
                command.Parameters.AddWithValue("orders", o.OrdersInventoryRpoMinutes);
                command.Parameters.AddWithValue("settings", o.SettingsRpoMinutes);
            }),
        new(ErrorLayer.Backup, "backup.restore_failed",
            """
            SELECT 2, 'data class=' || data_class FROM (
                SELECT DISTINCT ON (data_class) data_class, succeeded FROM operations.restore_attempts
                ORDER BY data_class, started_at DESC) latest WHERE NOT succeeded;
            """, NoBinding),
    ];

    /// <summary>Runs every check once; a check that cannot be read is skipped and leaves its events untouched.</summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        foreach (var check in Checks)
        {
            try
            {
                var active = await ReadActiveAsync(check, cancellationToken).ConfigureAwait(false);
                foreach (var (severity, detail) in active)
                    writer.TryEnqueue(new ErrorEventInput(check.Layer, severity, check.Category, detail));
                await ResolveRecoveredAsync(
                    check, active.Select(a => ErrorFingerprint.Compute(check.Layer, check.Category, a.Detail)).ToArray(),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Failures++;
            }
        }

        heartbeats?.Beat(HeartbeatName);
    }

    private async Task<List<(ErrorSeverity Severity, string Detail)>> ReadActiveAsync(DomainCheck check, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(check.Sql);
        check.Bind(command, _options);
        var rows = new List<(ErrorSeverity, string)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(((ErrorSeverity)reader.GetInt32(0), reader.GetString(1)));
        return rows;
    }

    private async Task ResolveRecoveredAsync(DomainCheck check, string[] activeFingerprints, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            UPDATE observability.error_events SET status = 'Resolved', resolved_at = now()
            WHERE category = @category AND status <> 'Resolved' AND fingerprint <> ALL(@active);
            """);
        command.Parameters.AddWithValue("category", check.Category);
        command.Parameters.AddWithValue("active", activeFingerprints);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var wait = interval ?? TimeSpan.FromSeconds(60);
        heartbeats?.Register(HeartbeatName, wait);
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            await Task.Delay(wait, stoppingToken).ConfigureAwait(false);
        }
    }
}

public static class DomainDetectorExtensions
{
    public static IServiceCollection AddDomainDetectors(this IServiceCollection services, DomainDetectorOptions? options = null)
    {
        services.AddHostedService(sp => new DomainDetectorRunner(
            sp.GetRequiredService<MonitoringDataSource>().Inner, sp.GetRequiredService<ErrorEventWriter>(), options,
            sp.GetService<DetectorHeartbeats>()));
        return services;
    }
}
