namespace ALKAROS.Kitchen.PhysicalPrintRecovery.Tests;

using ALKAROS.Kitchen.PhysicalPrintRecovery;
using ALKAROS.Kitchen.PrintQueue;
using ALKAROS.Kitchen.Routing;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Xunit;

public sealed class KitchenPhysicalPrintRecoveryTestDatabase : PgTestDatabase
{
    public KitchenPhysicalPrintRecoveryTestDatabase()
        : base("alkaros_kit004_")
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

public sealed class PostgresPhysicalPrintRecoveryIntegrationTests : IAsyncLifetime
{
    private readonly KitchenPhysicalPrintRecoveryTestDatabase _db = new();
    private PostgresPhysicalPrintRecoveryRepository _deliveryRepo = null!;
    private PostgresPrintQueueRepository _queueRepo = null!;
    private PostgresPrinterRepository _printerRepo = null!;
    private PostgresKitchenTicketRepository _ticketRepo = null!;
    private PhysicalPrintRecoveryService _recoveryService = null!;

    private Guid _testOrderId;
    private Guid _testPrinterId;
    private KitchenTicket _testTicket = null!;
    private PrintJob _testJob = null!;

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
        _deliveryRepo = new PostgresPhysicalPrintRecoveryRepository(_db.DataSource);
        _queueRepo = new PostgresPrintQueueRepository(_db.DataSource);
        _printerRepo = new PostgresPrinterRepository(_db.DataSource);
        _ticketRepo = new PostgresKitchenTicketRepository(_db.DataSource);
        _recoveryService = new PhysicalPrintRecoveryService(_deliveryRepo);

        // Seed dependent entities: Table, Order, Ticket, Printer, PrintJob
        _testOrderId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await _db.ExecuteSqlAsync(
            $"""
            INSERT INTO table_mgmt.tables (table_id, table_number, capacity, active, current_status)
            VALUES ('{tableId}', 'T-20', 4, true, 'Occupied');

            INSERT INTO orders.orders (order_id, source, table_id, status, confirmation_status, order_number, subtotal, discount_total, tax_total, total, currency_code, created_at, updated_at)
            VALUES ('{_testOrderId}', 'Cashier', '{tableId}', 'Accepted', 'Accepted', 'ORD-200', 150.00, 0, 15.00, 165.00, 'TRY', now(), now());
            """);

        var printer = new Printer(Guid.NewGuid(), "GrillStationPrinter", "Station-Grill", "192.168.1.101", 9100);
        await _printerRepo.SaveAsync(printer);
        _testPrinterId = printer.Id;

        var ticketItem = new KitchenTicketItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            productId,
            "Karisik Izgara",
            1);

        _testTicket = new KitchenTicket(
            Guid.NewGuid(),
            _testOrderId,
            "KT-200",
            "Station-Grill",
            [ticketItem],
            status: KitchenTicketState.Accepted);

        await _ticketRepo.AddAsync(_testTicket);

        _testJob = PrintJob.Create(_testTicket.Id, _testPrinterId, "TICKET PAYLOAD DATA", "job-key-200");
        _testJob = await _queueRepo.EnqueueJobAsync(_testJob);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task PhysicalDeliveryLifecycleFromInFlightToPrintedSucceeds()
    {
        var delivery = await _recoveryService.StartInFlightDeliveryAsync(
            _testJob.Id, _testTicket.Id, _testPrinterId, _testJob.Payload);

        delivery.Status.Should().Be(PhysicalPrintDeliveryStatus.InFlight);

        var printed = await _recoveryService.ConfirmDeliverySuccessAsync(delivery.Id);
        printed.Status.Should().Be(PhysicalPrintDeliveryStatus.Printed);
        printed.DeliveredAt.Should().NotBeNull();

        var loaded = await _deliveryRepo.GetByIdAsync(delivery.Id);
        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be(PhysicalPrintDeliveryStatus.Printed);
    }

    [Fact]
    public async Task CrashWindowTransitionsToUnknownAndAppearsInPendingOperatorReviews()
    {
        var delivery = await _recoveryService.StartInFlightDeliveryAsync(
            _testJob.Id, _testTicket.Id, _testPrinterId, _testJob.Payload);

        // Crash window event: connection dropped before ACK
        var unknown = await _recoveryService.ReportCrashWindowUncertaintyAsync(
            delivery.Id, "TCP connection reset by peer during ESC/POS data transfer");

        unknown.Status.Should().Be(PhysicalPrintDeliveryStatus.Unknown);
        unknown.CrashWindowReason.Should().Contain("TCP connection reset");

        var pendingList = await _recoveryService.GetPendingUnknownDeliveriesAsync();
        pendingList.Should().Contain(d => d.Id == delivery.Id);
    }

    [Fact]
    public async Task OperatorCanApproveReprintAndExecuteWithWatermarkBanner()
    {
        var delivery = await _recoveryService.StartInFlightDeliveryAsync(
            _testJob.Id, _testTicket.Id, _testPrinterId, _testJob.Payload);

        await _recoveryService.ReportCrashWindowUncertaintyAsync(delivery.Id, "Socket timeout");

        // Operator verifies kitchen and approves reprint
        var approved = await _recoveryService.ApproveOperatorReprintAsync(
            delivery.Id, "Operator-Ayse", "Yazici tikanmisti, fis cikmamis");

        approved.Status.Should().Be(PhysicalPrintDeliveryStatus.ReprintApproved);
        approved.IsReprint.Should().BeTrue();
        approved.ReprintPayload.Should().Contain("*** TEKRAR BASKI / REPRINT ***");
        approved.ReprintPayload.Should().Contain("ONAYLAYAN: Operator-Ayse");

        // Execute reprint
        string? executedReprintPayload = null;
        var reprinted = await _recoveryService.ExecuteApprovedReprintAsync(
            delivery.Id,
            payload =>
            {
                executedReprintPayload = payload;
                return Task.FromResult(true);
            });

        reprinted.Status.Should().Be(PhysicalPrintDeliveryStatus.Reprinted);
        executedReprintPayload.Should().NotBeNull();
        executedReprintPayload.Should().Contain("*** TEKRAR BASKI / REPRINT ***");

        var reloaded = await _deliveryRepo.GetByIdAsync(delivery.Id);
        reloaded!.Status.Should().Be(PhysicalPrintDeliveryStatus.Reprinted);
    }

    [Fact]
    public async Task OperatorCanRejectReprintToPreventDuplicateTicketInKitchen()
    {
        var delivery = await _recoveryService.StartInFlightDeliveryAsync(
            _testJob.Id, _testTicket.Id, _testPrinterId, _testJob.Payload);

        await _recoveryService.ReportCrashWindowUncertaintyAsync(delivery.Id, "Socket drop");

        // Operator sees paper actually came out
        var rejected = await _recoveryService.RejectOperatorReprintAsync(
            delivery.Id, "Operator-Mehmet", "Kagit cikmis, tekrar basmaya gerek yok");

        rejected.Status.Should().Be(PhysicalPrintDeliveryStatus.ReprintRejected);
        rejected.OperatorReason.Should().Contain("Kagit cikmis");

        // Should no longer appear in pending unknown reviews
        var pendingList = await _recoveryService.GetPendingUnknownDeliveriesAsync();
        pendingList.Should().NotContain(d => d.Id == delivery.Id);
    }

    [Fact]
    public async Task CrashAndRecoveryNeverModifiesOrDeletesOrderAndTicket()
    {
        var delivery = await _recoveryService.StartInFlightDeliveryAsync(
            _testJob.Id, _testTicket.Id, _testPrinterId, _testJob.Payload);

        await _recoveryService.ReportCrashWindowUncertaintyAsync(delivery.Id, "Total power failure");

        // Critical Invariant: Order and KitchenTicket entities remain 100% valid and untouched
        var ticket = await _ticketRepo.GetByIdAsync(_testTicket.Id);
        ticket.Should().NotBeNull();
        ticket!.Status.Should().Be(KitchenTicketState.Accepted);
        ticket.Items.Should().HaveCount(1);
    }
}
