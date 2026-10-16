using ALKAROS.Host.Observability.DomainDetectors;
using ALKAROS.Observability.ErrorAnalysis;
using ALKAROS.Observability.ErrorEvents;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Host.Observability.DomainDetectors.Tests;

public sealed class DomainDatabase : PgTestDatabase
{
    public DomainDatabase() : base("alkaros_rmd535_") { }

    protected override async Task ApplySqlAsync()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ALKAROS.slnx")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        var migrations = Path.Combine(root, "database", "migrations");
        await RunAsync(DataSource, "CREATE SCHEMA orders; CREATE TABLE orders.orders (order_id uuid PRIMARY KEY);"
            + " CREATE SCHEMA billing; CREATE TABLE billing.bills (bill_id uuid PRIMARY KEY);");
        foreach (var (version, folder, number) in new[]
        {
            ("V1", "V1-RMD-533", "187"), ("V1", "V1-KIT-001", "014"), ("V1", "V1-KIT-002", "016"), ("V1", "V1-KIT-003", "017"),
            ("V1", "V1-KIT-004", "018"), ("V13", "V13-PAY-001", "120"), ("V12", "V12-ONL-009", "156"), ("V12", "V12-ONL-011", "157"),
            ("V12", "V12-ONL-004", "147"), ("V1", "V1-OPS-002", "029"), ("V15", "V15-BKP-001", "138"), ("V15", "V15-BKP-002", "139"),
        })
        {
            var file = Directory.GetFiles(Path.Combine(migrations, version, folder), $"{number}-*.up.sql").Single();
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
        }
    }
}

public sealed class DomainDetectorsTests : IClassFixture<DomainDatabase>
{
    private readonly DomainDatabase _database;

    public DomainDetectorsTests(DomainDatabase database) => _database = database;

    private async Task ResetAsync()
    {
        await _database.ExecuteAsync(
            """
            DELETE FROM kitchen.physical_print_deliveries; DELETE FROM kitchen.print_jobs; DELETE FROM kitchen.kitchen_tickets;
            DELETE FROM kitchen.printers; DELETE FROM orders.orders; DELETE FROM payments.payments; DELETE FROM billing.bills;
            DELETE FROM online_ordering.provider_poll_state; DELETE FROM online_ordering.platform_store_status;
            DELETE FROM online_ordering.catalog_publications; DELETE FROM operations.backups;
            DELETE FROM operations.offsite_backup_receipts; DELETE FROM operations.restore_attempts;
            DELETE FROM observability.error_occurrences; DELETE FROM observability.error_events;
            """);
    }

    private async Task<DomainDetectorRunner> RunAsync(DomainDetectorOptions? options = null, DetectorHeartbeats? heartbeats = null)
    {
        await using var writer = new ErrorEventWriter(_database.DataSource, flushInterval: TimeSpan.FromMinutes(5));
        var runner = new DomainDetectorRunner(_database.DataSource, writer, options, heartbeats);
        await runner.RunOnceAsync(CancellationToken.None);
        await writer.FlushAsync();
        return runner;
    }

    private Task<string> StatusAsync(string category) =>
        _database.ScalarAsync<string>($"SELECT coalesce((SELECT status FROM observability.error_events WHERE category = '{category}' ORDER BY last_seen_at DESC LIMIT 1), 'None');");

    private Task<string> SeverityAsync(string category) =>
        _database.ScalarAsync<string>($"SELECT coalesce((SELECT severity FROM observability.error_events WHERE category = '{category}' LIMIT 1), 'None');");

    private async Task<Guid> SeedTicketAsync(string status = "Queued", int minutesAgo = 0)
    {
        var order = Guid.NewGuid();
        var ticket = Guid.NewGuid();
        await _database.ExecuteAsync("INSERT INTO orders.orders (order_id) VALUES (@o);", ("o", order));
        await _database.ExecuteAsync(
            "INSERT INTO kitchen.kitchen_tickets (id, order_id, ticket_number, station_id, status, created_at) VALUES (@t, @o, @n, 'grill', @s, now() - make_interval(mins => @m));",
            ("t", ticket), ("o", order), ("n", ticket.ToString("N")), ("s", status), ("m", minutesAgo));
        return ticket;
    }

    private async Task SeedPrintJobAsync(string status)
    {
        var ticket = await SeedTicketAsync("Accepted");
        var printer = Guid.NewGuid();
        await _database.ExecuteAsync("INSERT INTO kitchen.printers (id, name, station_id) VALUES (@p, @n, 'grill');", ("p", printer), ("n", printer.ToString("N")));
        await _database.ExecuteAsync(
            "INSERT INTO kitchen.print_jobs (id, ticket_id, printer_id, idempotency_key, payload, status, failed_at) VALUES (@j, @t, @p, @k, 'x', @s, now());",
            ("j", Guid.NewGuid()), ("t", ticket), ("p", printer), ("k", Guid.NewGuid().ToString("N")), ("s", status));
    }

    private async Task SeedPaymentAsync(string status, int minutesAgo)
    {
        var bill = Guid.NewGuid();
        await _database.ExecuteAsync("INSERT INTO billing.bills (bill_id) VALUES (@b);", ("b", bill));
        await _database.ExecuteAsync(
            """
            INSERT INTO payments.payments (payment_id, bill_id, status, requested_amount, initiated_at, created_at, updated_at)
            VALUES (@p, @b, @s, 10, now(), now(), now() - make_interval(mins => @m));
            """, ("p", Guid.NewGuid()), ("b", bill), ("s", status), ("m", minutesAgo));
    }

    [Fact]
    public async Task FailedPrintJobBecomesAnEventAndResolvesOnceThePrinterRecovers()
    {
        await ResetAsync();
        await SeedPrintJobAsync("DeadLetter");

        await RunAsync();
        (await StatusAsync("printer.job_failed")).Should().Be("Open");

        await _database.ExecuteAsync("UPDATE kitchen.print_jobs SET status = 'Printed';");
        await RunAsync();
        (await StatusAsync("printer.job_failed")).Should().Be("Resolved");
    }

    [Fact]
    public async Task UnknownPaymentIsWarningAtFirstAndCriticalAfterFiveMinutes()
    {
        await ResetAsync();
        await SeedPaymentAsync("Unknown", 1);
        await RunAsync();
        (await SeverityAsync("payment.unknown_outcome")).Should().Be("Warning");

        await _database.ExecuteAsync("UPDATE payments.payments SET updated_at = now() - interval '6 minutes';");
        await RunAsync();
        (await SeverityAsync("payment.unknown_outcome")).Should().Be("Critical");

        await _database.ExecuteAsync("UPDATE payments.payments SET status = 'Approved';");
        await RunAsync();
        (await StatusAsync("payment.unknown_outcome")).Should().Be("Resolved");
    }

    [Fact]
    public async Task ConfiguredThresholdMovesTheCriticalLine()
    {
        await ResetAsync();
        await SeedPaymentAsync("Unknown", 6);

        await RunAsync(new DomainDetectorOptions { PaymentUnknownCriticalMinutes = 30 });

        (await SeverityAsync("payment.unknown_outcome")).Should().Be("Warning");
    }

    [Fact]
    public async Task TicketWaitingInTheKitchenQueueBecomesAnEventAndResolvesWhenAccepted()
    {
        await ResetAsync();
        var ticket = await SeedTicketAsync("Queued", 10);
        await RunAsync();
        (await StatusAsync("kitchen.ticket_not_accepted")).Should().Be("Open");

        await _database.ExecuteAsync("UPDATE kitchen.kitchen_tickets SET status = 'Accepted' WHERE id = @t;", ("t", ticket));
        await RunAsync();
        (await StatusAsync("kitchen.ticket_not_accepted")).Should().Be("Resolved");
    }

    [Fact]
    public async Task FailingProviderPollIsAnEventPerProviderAndResolvesOnSuccess()
    {
        await ResetAsync();
        await _database.ExecuteAsync(
            "INSERT INTO online_ordering.provider_poll_state (provider, consecutive_failures, failing_since) VALUES ('yemeksepeti', 6, now()), ('trendyol-go', 1, now());");

        await RunAsync();

        (await _database.ScalarAsync<long>("SELECT count(*) FROM observability.error_events WHERE category = 'integration.provider_poll_failing' AND status = 'Open';")).Should().Be(2);
        (await _database.ScalarAsync<string>("SELECT severity FROM observability.error_events WHERE category = 'integration.provider_poll_failing' AND detail LIKE '%yemeksepeti%';")).Should().Be("Error");

        await _database.ExecuteAsync("UPDATE online_ordering.provider_poll_state SET consecutive_failures = 0, failing_since = NULL WHERE provider = 'yemeksepeti';");
        await RunAsync();
        (await _database.ScalarAsync<long>("SELECT count(*) FROM observability.error_events WHERE category = 'integration.provider_poll_failing' AND status = 'Open';")).Should().Be(1);
    }

    [Fact]
    public async Task UndeliveredCatalogPublicationIsAnIntegrationEvent()
    {
        await ResetAsync();
        await _database.ExecuteAsync(
            """
            INSERT INTO online_ordering.catalog_publications (publication_id, channel, menu_id, status, content_sha256, item_count, requested_by, requested_at, last_error)
            VALUES (@p, 'yemeksepeti', @m, 'Pending', repeat('a', 64), 1, @u, now() - interval '30 minutes', 'timeout');
            """, ("p", Guid.NewGuid()), ("m", Guid.NewGuid()), ("u", Guid.NewGuid()));

        await RunAsync();

        (await StatusAsync("integration.catalog_undelivered")).Should().Be("Open");
    }

    [Fact]
    public async Task FailedBackupAndMissedOffsiteTargetAreEventsAndResolveOnRecovery()
    {
        await ResetAsync();
        await _database.ExecuteAsync(
            "INSERT INTO operations.backups (backup_id, backup_type, file_path, file_size_bytes, checksum_sha256, status, error_message) VALUES (@b, 'Full', 'f', 0, 'c', 'Failed', 'disk');",
            ("b", Guid.NewGuid()));
        await _database.ExecuteAsync(
            "INSERT INTO operations.offsite_backup_receipts (artifact_id, data_class, plaintext_checksum_sha256, encryption_key_name, encryption_key_version, plaintext_size_bytes, target_location, uploaded_at) VALUES ('a1', 'Fiscal', 'c', 'k', 1, 1, 't', now() - interval '10 minutes');");

        await RunAsync();
        (await StatusAsync("backup.failed")).Should().Be("Open");
        (await StatusAsync("backup.rpo_exceeded")).Should().Be("Open");

        await _database.ExecuteAsync(
            "INSERT INTO operations.backups (backup_id, backup_type, file_path, file_size_bytes, checksum_sha256, status, started_at, completed_at) VALUES (@b, 'Full', 'f', 1, 'c', 'Completed', now() + interval '1 second', now() + interval '1 second');",
            ("b", Guid.NewGuid()));
        await _database.ExecuteAsync("UPDATE operations.offsite_backup_receipts SET uploaded_at = now();");
        await RunAsync();
        (await StatusAsync("backup.failed")).Should().Be("Resolved");
        (await StatusAsync("backup.rpo_exceeded")).Should().Be("Resolved");
    }

    [Fact]
    public async Task FailedRestoreRehearsalIsAnEvent()
    {
        await ResetAsync();
        await _database.ExecuteAsync(
            "INSERT INTO operations.restore_attempts (artifact_id, data_class, started_at, duration_ms, succeeded, within_rto_target, integrity_checks_passed, integrity_checks_total, failure_reason) VALUES ('a', 'Settings', now(), 5, false, false, 0, 1, 'checksum');");

        await RunAsync();

        (await StatusAsync("backup.restore_failed")).Should().Be("Open");
    }

    [Fact]
    public async Task UnreadableSourceIsSkippedWithoutResolvingItsEventsOrStoppingOtherChecks()
    {
        await ResetAsync();
        await SeedPaymentAsync("Unknown", 10);
        await RunAsync();
        await _database.ExecuteAsync("ALTER TABLE payments.payments RENAME TO payments_hidden;");
        try
        {
            await SeedTicketAsync("Queued", 10);
            var runner = await RunAsync();

            runner.Failures.Should().BeGreaterThan(0);
            (await StatusAsync("payment.unknown_outcome")).Should().Be("Open");
            (await StatusAsync("kitchen.ticket_not_accepted")).Should().Be("Open");
        }
        finally
        {
            await _database.ExecuteAsync("ALTER TABLE payments.payments_hidden RENAME TO payments;");
        }
    }

    [Fact]
    public async Task EachRunBeatsTheDetectorHeartbeat()
    {
        await ResetAsync();
        var now = DateTimeOffset.UtcNow;
        var heartbeats = new DetectorHeartbeats(() => now);
        heartbeats.Register(DomainDetectorRunner.HeartbeatName, TimeSpan.FromSeconds(60));
        now = now.AddMinutes(10);

        heartbeats.Degraded().Should().NotBeEmpty();
        await RunAsync(heartbeats: heartbeats);

        heartbeats.Degraded().Should().BeEmpty();
    }

    [Fact]
    public async Task StuckQueueAndUnresolvedPhysicalPrintAreEvents()
    {
        await ResetAsync();
        await SeedPrintJobAsync("Pending");
        await _database.ExecuteAsync("UPDATE kitchen.print_jobs SET created_at = now() - interval '20 minutes';");
        await _database.ExecuteAsync(
            """
            INSERT INTO kitchen.physical_print_deliveries (id, print_job_id, ticket_id, printer_id, status, payload_snapshot, created_at)
            SELECT gen_random_uuid(), id, ticket_id, printer_id, 'Unknown', 'x', now() - interval '20 minutes' FROM kitchen.print_jobs;
            """);

        await RunAsync();

        (await StatusAsync("printer.queue_stuck")).Should().Be("Open");
        (await StatusAsync("printer.delivery_unknown")).Should().Be("Open");
        await _database.ExecuteAsync("UPDATE kitchen.physical_print_deliveries SET status = 'Printed';");
        await RunAsync();
        (await StatusAsync("printer.delivery_unknown")).Should().Be("Resolved");
    }

    [Fact]
    public async Task PaymentNeedingReconciliationIsAWarning()
    {
        await ResetAsync();
        await SeedPaymentAsync("ReconciliationRequired", 1);

        await RunAsync();

        (await SeverityAsync("payment.reconciliation_required")).Should().Be("Warning");
    }

    [Fact]
    public async Task UndeliveredStoreStatusIsAnIntegrationEvent()
    {
        await ResetAsync();
        await _database.ExecuteAsync(
            "INSERT INTO online_ordering.platform_store_status (provider, desired_state, requested_at, delivery_attempts, last_error) VALUES ('trendyol-go', 'Open', now() - interval '30 minutes', 3, 'timeout');");

        await RunAsync();

        (await StatusAsync("integration.store_status_undelivered")).Should().Be("Open");
    }

    [Fact]
    public async Task NoRecentCompletedBackupIsAWarningButAnEmptyBackupTableIsSilent()
    {
        await ResetAsync();
        await RunAsync();
        (await StatusAsync("backup.stale")).Should().Be("None");

        await _database.ExecuteAsync(
            "INSERT INTO operations.backups (backup_id, backup_type, file_path, file_size_bytes, checksum_sha256, status, started_at, completed_at) VALUES (@b, 'Full', 'f', 1, 'c', 'Completed', now() - interval '3 days', now() - interval '3 days');",
            ("b", Guid.NewGuid()));
        await RunAsync();
        (await StatusAsync("backup.stale")).Should().Be("Open");
    }
}
