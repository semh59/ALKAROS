namespace ALKAROS.Kitchen.PrintQueue.Tests;

using ALKAROS.Kitchen.PrintQueue;
using ALKAROS.Kitchen.Routing;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

public sealed class KitchenPrintQueueTestDatabase : PgTestDatabase
{
    public KitchenPrintQueueTestDatabase()
        : base("alkaros_kit003_")
    {
    }

    public Task ExecuteSqlAsync(string sql) => RunAsync(DataSource, sql);

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        var upFiles = Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f);
        foreach (var file in upFiles)
        {
            await RunAsync(DataSource, await File.ReadAllTextAsync(file)).ConfigureAwait(false);
        }
    }
}

public sealed class PostgresPrintQueueIntegrationTests : IAsyncLifetime
{
    private readonly KitchenPrintQueueTestDatabase _db = new();
    private PostgresPrintQueueRepository _queueRepo = null!;
    private PostgresPrinterRepository _printerRepo = null!;
    private PostgresKitchenTicketRepository _ticketRepo = null!;
    private PrintQueueService _queueService = null!;

    private Guid _testOrderId;
    private Guid _testPrinterId;
    private KitchenTicket _testTicket = null!;

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
        _queueRepo = new PostgresPrintQueueRepository(_db.DataSource);
        _printerRepo = new PostgresPrinterRepository(_db.DataSource);
        _ticketRepo = new PostgresKitchenTicketRepository(_db.DataSource);
        _queueService = new PrintQueueService(_queueRepo);

        // Seed dependent entities: Table, Order, Ticket, Printer
        _testOrderId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await _db.ExecuteSqlAsync(
            $"""
            INSERT INTO table_mgmt.tables (table_id, table_number, capacity, active, current_status)
            VALUES ('{tableId}', 'T-10', 4, true, 'Occupied');

            INSERT INTO orders.orders (order_id, source, table_id, status, confirmation_status, order_number, subtotal, discount_total, tax_total, total, currency_code, created_at, updated_at)
            VALUES ('{_testOrderId}', 'Cashier', '{tableId}', 'Accepted', 'Accepted', 'ORD-100', 100.00, 0, 10.00, 110.00, 'TRY', now(), now());
            """);

        var printer = new Printer(Guid.NewGuid(), "KitchenMain", "Station1", "192.168.1.100", 9100);
        await _printerRepo.SaveAsync(printer);
        _testPrinterId = printer.Id;

        var ticketItem = new KitchenTicketItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            productId,
            "Burger",
            1);

        _testTicket = new KitchenTicket(
            Guid.NewGuid(),
            _testOrderId,
            "KT-100",
            "Station1",
            [ticketItem],
            status: KitchenTicketState.Accepted);

        await _ticketRepo.AddAsync(_testTicket);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task EnqueueJobIsIdempotentAndReturnsExistingJobOnDuplicate()
    {
        var job1 = PrintJob.Create(_testTicket.Id, _testPrinterId, "Payload 1", idempotencyKey: "key-unique-1");
        var job2 = PrintJob.Create(_testTicket.Id, _testPrinterId, "Payload 2 (Duplicate)", idempotencyKey: "key-unique-1");

        var result1 = await _queueRepo.EnqueueJobAsync(job1);
        var result2 = await _queueRepo.EnqueueJobAsync(job2);

        result1.Id.Should().Be(job1.Id);
        result2.Id.Should().Be(job1.Id); // Returned preexisting job
        result2.Payload.Should().Be("Payload 1"); // Preserved original payload
    }

    [Fact]
    public async Task ClaimEligibleJobsAcquiresLeaseAndPreventsConcurrentDoubleClaim()
    {
        var job = PrintJob.Create(_testTicket.Id, _testPrinterId, "Payload", idempotencyKey: "key-claim-1");
        await _queueRepo.EnqueueJobAsync(job);

        var now = DateTimeOffset.UtcNow;
        var claimedByWorker1 = await _queueRepo.ClaimEligibleJobsAsync("Worker-1", 10, TimeSpan.FromMinutes(2), now);

        claimedByWorker1.Should().HaveCount(1);
        claimedByWorker1[0].LeasedBy.Should().Be("Worker-1");
        claimedByWorker1[0].Status.Should().Be(PrintJobStatus.Leased);

        // Worker 2 attempts to claim at the same time -> should find 0 eligible jobs
        var claimedByWorker2 = await _queueRepo.ClaimEligibleJobsAsync("Worker-2", 10, TimeSpan.FromMinutes(2), now);
        claimedByWorker2.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessEligibleJobsExecutesAndMarksSucceededOnSuccess()
    {
        var job = PrintJob.Create(_testTicket.Id, _testPrinterId, "Payload", idempotencyKey: "key-exec-1");
        await _queueRepo.EnqueueJobAsync(job);

        var executedJobs = new List<Guid>();
        var processedCount = await _queueService.ProcessEligibleJobsAsync(
            "Worker-1",
            batchSize: 5,
            leaseDuration: TimeSpan.FromMinutes(1),
            printerExecutor: j =>
            {
                executedJobs.Add(j.Id);
                return Task.FromResult(true); // Succeeded
            });

        processedCount.Should().Be(1);
        executedJobs.Should().Contain(job.Id);

        var loaded = await _queueRepo.GetByIdAsync(job.Id);
        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be(PrintJobStatus.Printed);
        loaded.PrintedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessEligibleJobsRecordsFailureAndDoesNotDeleteOrderOrTicket()
    {
        var job = PrintJob.Create(_testTicket.Id, _testPrinterId, "Payload", idempotencyKey: "key-fail-1", maxAttempts: 3);
        await _queueRepo.EnqueueJobAsync(job);

        var processedCount = await _queueService.ProcessEligibleJobsAsync(
            "Worker-1",
            batchSize: 5,
            leaseDuration: TimeSpan.FromMinutes(1),
            printerExecutor: _ => Task.FromResult(false)); // Simulated printer failure

        processedCount.Should().Be(1);

        var loadedJob = await _queueRepo.GetByIdAsync(job.Id);
        loadedJob.Should().NotBeNull();
        loadedJob!.Status.Should().Be(PrintJobStatus.Failed);
        loadedJob.AttemptCount.Should().Be(1);
        loadedJob.NextAttemptAt.Should().NotBeNull();

        // Critical Acceptance Invariant: Failed printing never deletes or corrupts order / ticket data
        var ticket = await _ticketRepo.GetByIdAsync(_testTicket.Id);
        ticket.Should().NotBeNull();
        ticket!.Status.Should().Be(KitchenTicketState.Accepted);
    }

    [Fact]
    public async Task RecoverExpiredLeasesResetsStaleJobsToPending()
    {
        var job = PrintJob.Create(_testTicket.Id, _testPrinterId, "Payload", idempotencyKey: "key-recover-1");
        await _queueRepo.EnqueueJobAsync(job);

        var past = DateTimeOffset.UtcNow.AddMinutes(-10);
        // Worker claims with 1-minute lease in the past (so it is now expired)
        await _queueRepo.ClaimEligibleJobsAsync("CrashedWorker", 5, TimeSpan.FromMinutes(1), past);

        var recoveredCount = await _queueRepo.RecoverExpiredLeasesAsync(DateTimeOffset.UtcNow);
        recoveredCount.Should().Be(1);

        var reloaded = await _queueRepo.GetByIdAsync(job.Id);
        reloaded!.Status.Should().Be(PrintJobStatus.Pending);
        reloaded.LeasedBy.Should().BeNull();
    }

    [Fact]
    public async Task EndToEndPrintingWithEscPosFormatterAndKitchenPrinterSimulator()
    {
        var simulator = new KitchenPrinterSimulator(_testPrinterId, "Station1-POSPrinter", PrinterSimulatedState.Online);
        var formattedTicket = EscPosTicketFormatter.FormatToPrintableText(_testTicket, tableNumber: "T-10", orderNumber: "ORD-100");

        var enqueuedJob = await _queueService.EnqueueTicketPrintJobAsync(
            _testTicket,
            _testPrinterId,
            formattedTicket,
            customIdempotencyKey: $"e2e-ticket:{_testTicket.Id}");

        enqueuedJob.Status.Should().Be(PrintJobStatus.Pending);

        // Process job using simulator
        var processed = await _queueService.ProcessEligibleJobsAsync(
            "Worker-Simulation-1",
            batchSize: 5,
            leaseDuration: TimeSpan.FromMinutes(1),
            printerExecutor: async j =>
            {
                var result = await simulator.PrintAsync(j);
                return result.Success;
            });

        processed.Should().Be(1);
        simulator.PrintedHistory.Should().HaveCount(1);
        simulator.PrintedPayloads[0].Should().Contain("MUTFAK SIPARIS FISI - STATION1");
        simulator.PrintedPayloads[0].Should().Contain("MASA: T-10");
        simulator.PrintedPayloads[0].Should().Contain("Burger");

        var completedJob = await _queueRepo.GetByIdAsync(enqueuedJob.Id);
        completedJob!.Status.Should().Be(PrintJobStatus.Printed);
        completedJob.PrintedAt.Should().NotBeNull();
    }
}
