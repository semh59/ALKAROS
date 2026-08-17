namespace ALKAROS.Kitchen.PrintQueue.Tests;

using ALKAROS.Kitchen.PrintQueue;
using ALKAROS.Kitchen.TicketLifecycle;
using FluentAssertions;
using Xunit;

public sealed class PrintQueueUnitTests
{
    private readonly Guid _ticketId = Guid.NewGuid();
    private readonly Guid _printerId = Guid.NewGuid();
    private const string Payload = "PRINT TEST TICKET DATA";

    [Fact]
    public void FactoryCreatesPendingJobWithCanonicalDefaults()
    {
        var job = PrintJob.Create(_ticketId, _printerId, Payload);

        job.Id.Should().NotBeEmpty();
        job.TicketId.Should().Be(_ticketId);
        job.PrinterId.Should().Be(_printerId);
        job.IdempotencyKey.Should().Be($"print:ticket:{_ticketId}:printer:{_printerId}");
        job.Payload.Should().Be(Payload);
        job.Status.Should().Be(PrintJobStatus.Pending);
        job.AttemptCount.Should().Be(0);
        job.MaxAttempts.Should().Be(5);
        job.NextAttemptAt.Should().BeNull();
        job.LeasedBy.Should().BeNull();
        job.LeaseExpiresAt.Should().BeNull();
    }

    [Fact]
    public void ClaimLeaseTransitionsPendingToLeasedWithExpiration()
    {
        var job = PrintJob.Create(_ticketId, _printerId, Payload);
        var now = DateTimeOffset.UtcNow;
        var duration = TimeSpan.FromMinutes(2);

        var leased = job.ClaimLease("Worker-1", duration, now);

        leased.Status.Should().Be(PrintJobStatus.Leased);
        leased.LeasedBy.Should().Be("Worker-1");
        leased.LeaseExpiresAt.Should().Be(now.Add(duration));
    }

    [Fact]
    public void MarkPrintingRequiresActiveLeaseBySameWorker()
    {
        var job = PrintJob.Create(_ticketId, _printerId, Payload);
        var now = DateTimeOffset.UtcNow;
        var leased = job.ClaimLease("Worker-1", TimeSpan.FromMinutes(2), now);

        // Valid transition
        var printing = leased.MarkPrinting("Worker-1", now.AddSeconds(10));
        printing.Status.Should().Be(PrintJobStatus.Printing);

        // Different worker fails
        var actOtherWorker = () => leased.MarkPrinting("Worker-2", now.AddSeconds(10));
        actOtherWorker.Should().Throw<PrintJobLeaseException>();

        // Expired lease fails
        var actExpired = () => leased.MarkPrinting("Worker-1", now.AddMinutes(5));
        actExpired.Should().Throw<PrintJobLeaseException>();
    }

    [Fact]
    public void MarkSucceededSetsTerminalPrintedStatusAndPrintedAt()
    {
        var job = PrintJob.Create(_ticketId, _printerId, Payload);
        var now = DateTimeOffset.UtcNow;
        var leased = job.ClaimLease("Worker-1", TimeSpan.FromMinutes(2), now);
        var printing = leased.MarkPrinting("Worker-1", now.AddSeconds(5));

        var succeeded = printing.MarkSucceeded(now.AddSeconds(10));

        succeeded.Status.Should().Be(PrintJobStatus.Printed);
        succeeded.PrintedAt.Should().NotBeNull();
        succeeded.LeasedBy.Should().BeNull();
        succeeded.LeaseExpiresAt.Should().BeNull();
    }

    [Fact]
    public void RecordFailureIncrementsAttemptCountAndCalculatesBackoff()
    {
        var job = PrintJob.Create(_ticketId, _printerId, Payload, maxAttempts: 3);
        var now = DateTimeOffset.UtcNow;
        var leased = job.ClaimLease("Worker-1", TimeSpan.FromMinutes(2), now);

        var backoff = PrintQueueService.CalculateBackoff(0);
        var failed = leased.RecordFailure("Paper jam", backoff, now);

        failed.Status.Should().Be(PrintJobStatus.Failed);
        failed.AttemptCount.Should().Be(1);
        failed.LastError.Should().Be("Paper jam");
        failed.NextAttemptAt.Should().Be(now.Add(backoff));
        failed.LeasedBy.Should().BeNull();
        failed.LeaseExpiresAt.Should().BeNull();
    }

    [Fact]
    public void RecordFailureTransitionsToDeadLetterWhenMaxAttemptsExceeded()
    {
        var job = new PrintJob(
            Guid.NewGuid(),
            _ticketId,
            _printerId,
            "key1",
            Payload,
            status: PrintJobStatus.Leased,
            attemptCount: 2,
            maxAttempts: 3);

        var now = DateTimeOffset.UtcNow;
        var failed = job.RecordFailure("Unreachable", TimeSpan.FromSeconds(10), now);

        failed.Status.Should().Be(PrintJobStatus.DeadLetter);
        failed.AttemptCount.Should().Be(3);
        failed.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public void ResetExpiredLeaseResetsStaleLeaseToPending()
    {
        var now = DateTimeOffset.UtcNow;
        var staleJob = new PrintJob(
            Guid.NewGuid(),
            _ticketId,
            _printerId,
            "key1",
            Payload,
            status: PrintJobStatus.Leased,
            leasedBy: "DeadWorker",
            leaseExpiresAt: now.AddMinutes(-5));

        var recovered = staleJob.ResetExpiredLease(now);

        recovered.Status.Should().Be(PrintJobStatus.Pending);
        recovered.LeasedBy.Should().BeNull();
        recovered.LeaseExpiresAt.Should().BeNull();
    }

    [Fact]
    public void CancelSetsTerminalCancelledStatus()
    {
        var job = PrintJob.Create(_ticketId, _printerId, Payload);
        var now = DateTimeOffset.UtcNow;

        var cancelled = job.Cancel("Order voided", now);
        cancelled.Status.Should().Be(PrintJobStatus.Cancelled);
        cancelled.LastError.Should().Be("Order voided");

        // Cannot cancel already printed job
        var printed = job.ClaimLease("W1", TimeSpan.FromMinutes(1), now).MarkSucceeded(now);
        var act = () => printed.Cancel("Too late", now);
        act.Should().Throw<InvalidPrintJobTransitionException>();
    }

    [Fact]
    public void EscPosFormatterRendersStandard80MmThermalTicket()
    {
        var item1 = new KitchenTicketItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Adana Kebap", 2, modifiersSummary: "Porsiyon", notes: "Az acili");
        var item2 = new KitchenTicketItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Ayran", 2);

        var ticket = new KitchenTicket(
            Guid.NewGuid(), Guid.NewGuid(), "KT-42", "Station-Ocakbasi", [item1, item2], status: KitchenTicketState.Accepted);

        var printableText = EscPosTicketFormatter.FormatToPrintableText(ticket, tableNumber: "T-05", orderNumber: "ORD-999");

        printableText.Should().Contain("MUTFAK SIPARIS FISI - STATION-OCAKBASI");
        printableText.Should().Contain("MASA: T-05");
        printableText.Should().Contain("FIS NO: KT-42");
        printableText.Should().Contain("SIPARIS: ORD-999");
        printableText.Should().Contain("Adana Kebap");
        printableText.Should().Contain("+ Porsiyon");
        printableText.Should().Contain("NOT: Az acili");
        printableText.Should().Contain("Ayran");
        printableText.Should().Contain("*** ACCEPTED ***");
    }

    [Fact]
    public void EscPosFormatterGeneratesValidEscPosBytesWithCutCommand()
    {
        var item = new KitchenTicketItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Burger", 1);
        var ticket = new KitchenTicket(
            Guid.NewGuid(), Guid.NewGuid(), "KT-01", "Station1", [item]);

        var escPosBytes = EscPosTicketFormatter.FormatToEscPosBytes(ticket);

        escPosBytes.Should().NotBeEmpty();
        // Starts with ESC @ (initialize)
        escPosBytes[0].Should().Be(0x1B);
        escPosBytes[1].Should().Be(0x40);
        // Ends with GS V 'B' 3 (cut paper with feed)
        escPosBytes[^4].Should().Be(0x1D);
        escPosBytes[^3].Should().Be(0x56);
    }

    [Fact]
    public async Task KitchenPrinterSimulatorHandlesSuccessAndSimulatedErrors()
    {
        var printer = new KitchenPrinterSimulator(Guid.NewGuid(), "KitchenThermalPOS-1");
        var job = PrintJob.Create(Guid.NewGuid(), printer.PrinterId, "PRINT PAYLOAD");

        // 1. Online success
        var successResult = await printer.PrintAsync(job);
        successResult.Success.Should().BeTrue();
        successResult.BytesReceived.Should().BeGreaterThan(0);
        printer.PrintedHistory.Should().HaveCount(1);

        // 2. Paper Out error
        printer.State = PrinterSimulatedState.PaperOut;
        var paperOutResult = await printer.PrintAsync(job);
        paperOutResult.Success.Should().BeFalse();
        paperOutResult.ErrorMessage.Should().Contain("PRINTER_PAPER_OUT");

        // 3. Offline error
        printer.State = PrinterSimulatedState.Offline;
        var offlineResult = await printer.PrintAsync(job);
        offlineResult.Success.Should().BeFalse();
        offlineResult.ErrorMessage.Should().Contain("PRINTER_OFFLINE");
    }
}
