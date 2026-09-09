using ALKAROS.Kitchen.PrintQueue;
using ALKAROS.Kitchen.Routing;
using ALKAROS.Kitchen.TicketLifecycle;
using Xunit;

namespace ALKAROS.Host.Experience.KitchenOperations.Tests;

/// <summary>
/// V1-RMD-130: found by an independent audit (2026-09-09) — a kitchen
/// ticket never got a print job at all (nothing called
/// IPrintQueueService.EnqueueTicketPrintJobAsync in production). These
/// tests cover the bridge step that closes that gap, against real Postgres.
/// The dispatch-to-real-printer step itself is covered separately
/// (TcpEscPosPrinterTransportTests, PostgresPrintQueueIntegrationTests'
/// ProcessEligibleJobsRoutesAnUncertainTransmissionToOperatorReviewInsteadOfRetrying).
/// </summary>
[Collection("Kitchen print dispatch PostgreSQL")]
public sealed class KitchenPrintDispatchHostedServiceTests : IAsyncLifetime
{
    private readonly KitchenOperationsTestDatabase _database = new();
    private PostgresKitchenTicketRepository _ticketRepository = null!;
    private PostgresPrinterRepository _printerRepository = null!;
    private PostgresPrintQueueRepository _printQueueRepository = null!;
    private PrintQueueService _printQueueService = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _ticketRepository = new PostgresKitchenTicketRepository(_database.DataSource);
        _printerRepository = new PostgresPrinterRepository(_database.DataSource);
        _printQueueRepository = new PostgresPrintQueueRepository(_database.DataSource);
        _printQueueService = new PrintQueueService(_printQueueRepository);
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task BridgesATicketToOnePrintJobPerActivePrinterAtItsStation()
    {
        var tableId = Guid.NewGuid();
        var (ticketId, printerId, _) = await _database.SeedTicketAwaitingPrintJobAsync(tableId, "T-42");

        var bridged = await KitchenPrintDispatchHostedService.BridgeUnprintedTicketsAsync(
            _database.DataSource, _ticketRepository, _printerRepository, _printQueueService, CancellationToken.None);

        Assert.Equal(1, bridged);

        var jobs = await _printQueueRepository.GetByTicketIdAsync(ticketId);
        var job = Assert.Single(jobs);
        Assert.Equal(printerId, job.PrinterId);
        Assert.Equal(PrintJobStatus.Pending, job.Status);
        Assert.Contains("Bridge test item", job.Payload);
        Assert.Contains("T-42", job.Payload);
        Assert.Contains("ORD-", job.Payload);
    }

    [Fact]
    public async Task RunningTheBridgeTwiceDoesNotDuplicatePrintJobs()
    {
        var (ticketId, _, _) = await _database.SeedTicketAwaitingPrintJobAsync();

        await KitchenPrintDispatchHostedService.BridgeUnprintedTicketsAsync(
            _database.DataSource, _ticketRepository, _printerRepository, _printQueueService, CancellationToken.None);
        var secondPassBridged = await KitchenPrintDispatchHostedService.BridgeUnprintedTicketsAsync(
            _database.DataSource, _ticketRepository, _printerRepository, _printQueueService, CancellationToken.None);

        // The second pass finds no candidate tickets at all: the first pass's
        // job already exists, so the ticket no longer matches
        // "NOT EXISTS (print_jobs WHERE ticket_id = ...)".
        Assert.Equal(0, secondPassBridged);
        var jobs = await _printQueueRepository.GetByTicketIdAsync(ticketId);
        Assert.Single(jobs);
    }

    [Fact]
    public async Task ATicketAtAStationWithNoActivePrinterIsNotBridged()
    {
        var (ticketId, printerId, _) = await _database.SeedTicketAwaitingPrintJobAsync();
        await _database.ExecuteAsync($"UPDATE kitchen.printers SET is_active = FALSE WHERE id = '{printerId:D}';");

        var bridged = await KitchenPrintDispatchHostedService.BridgeUnprintedTicketsAsync(
            _database.DataSource, _ticketRepository, _printerRepository, _printQueueService, CancellationToken.None);

        Assert.Equal(0, bridged);
        Assert.Empty(await _printQueueRepository.GetByTicketIdAsync(ticketId));
    }
}

[CollectionDefinition("Kitchen print dispatch PostgreSQL", DisableParallelization = true)]
public sealed class KitchenPrintDispatchPostgresqlDefinition;
