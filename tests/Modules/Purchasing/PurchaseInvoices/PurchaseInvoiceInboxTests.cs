using ALKAROS.Inventory.StockMaster;
using ALKAROS.Purchasing.Suppliers;
using FluentAssertions;
using Xunit;
using static ALKAROS.Purchasing.PurchaseInvoices.Tests.InvoiceXml;

namespace ALKAROS.Purchasing.PurchaseInvoices.Tests;

public sealed class PurchaseInvoiceInboxTests : IClassFixture<PurchaseInvoiceTestDatabase>, IAsyncDisposable
{
    private static long _sequence = 1_000;

    private readonly PurchaseInvoiceTestDatabase _database;
    private readonly PostgresPurchaseInvoiceRepository _repository;
    private readonly FakeSource _source = new();
    private readonly PurchaseInvoiceInboxService _inbox;
    private readonly PurchaseInvoiceService _invoices;

    public PurchaseInvoiceInboxTests(PurchaseInvoiceTestDatabase database)
    {
        _database = database;
        _repository = new PostgresPurchaseInvoiceRepository(database.DataSource);
        _invoices = new PurchaseInvoiceService(
            _repository, new PostgresSupplierRepository(database.DataSource), new PostgresStockItemRepository(database.DataSource));
        _inbox = new PurchaseInvoiceInboxService(_source, _invoices, _repository);
    }

    public ValueTask DisposeAsync() => _source.DisposeAsync();

    private static long NextSequence() => Interlocked.Increment(ref _sequence);

    private static string Tax() => Random.Shared.NextInt64(1_000_000_000, 9_999_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private (Guid Ettn, long Sequence) Offer(string? xml = null, Guid? ettn = null)
    {
        var id = ettn ?? Guid.NewGuid();
        var sequence = NextSequence();
        _source.Documents.Add((new InboxDocument(id.ToString(), "N" + sequence, sequence), xml ?? Build(id, Tax())));
        return (id, sequence);
    }

    [Fact]
    public async Task NewDocumentsBecomeDraftsAndTheCursorMovesPastThem()
    {
        var (first, _) = Offer();
        var (second, lastSequence) = Offer();

        var result = await _inbox.FetchAsync("Ayşe");

        result.Should().Be(new InboxFetchResult(2, 2, 0, 0, false));
        (await _repository.GetInboxCursorAsync(PurchaseInvoiceSources.QnbInbox)).Should().Be(lastSequence);
        var drafts = await _invoices.ListAsync("Draft");
        drafts.Should().HaveCountGreaterThanOrEqualTo(2);
        var again = await _inbox.FetchAsync("Ayşe");
        again.Should().Be(new InboxFetchResult(0, 0, 0, 0, false));
        _source.Downloads.Should().Equal(first.ToString(), second.ToString());
    }

    [Fact]
    public async Task ADocumentAlreadyImportedIsCountedAsADuplicateAndDoesNotBlockTheRest()
    {
        var ettn = Guid.NewGuid();
        await _invoices.ImportAsync(Build(ettn, Tax()), "Elle", PurchaseInvoiceSources.XmlUpload);
        Offer(ettn: ettn);
        Offer();

        var result = await _inbox.FetchAsync("Ayşe");

        result.Should().Be(new InboxFetchResult(2, 1, 1, 0, false));
    }

    [Fact]
    public async Task ABrokenOrForeignDocumentIsSkippedAndTheCursorPassesIt()
    {
        Offer(xml: Build(Guid.NewGuid(), Tax(), root: "Order"));
        Offer(xml: "bu xml degil");
        var (_, lastSequence) = Offer();

        var result = await _inbox.FetchAsync("Ayşe");

        result.Should().Be(new InboxFetchResult(3, 1, 0, 2, false));
        (await _repository.GetInboxCursorAsync(PurchaseInvoiceSources.QnbInbox)).Should().Be(lastSequence);
    }

    [Fact]
    public async Task AReturnInvoiceBecomesAReturnDraft()
    {
        var number = "IADE" + Guid.NewGuid().ToString("N")[..10];
        Offer(xml: Build(Guid.NewGuid(), Tax(), number: number, typeCode: "IADE", referenced: "ABC1"));

        var result = await _inbox.FetchAsync("Ayşe");

        result.Should().Be(new InboxFetchResult(1, 1, 0, 0, false));
        (await _invoices.ListAsync("Draft")).Should().ContainSingle(i => i.InvoiceNumber == number).Which.Kind.Should().Be(PurchaseInvoiceKinds.Return);
    }

    [Fact]
    public async Task ATransientDownloadFailureStopsTheRunAndTheNextRunResumesAtThatDocument()
    {
        var (good, _) = Offer();
        var (flaky, flakySequence) = Offer();
        var (after, afterSequence) = Offer();
        _source.FailOnce.Add(flaky.ToString());

        var first = await _inbox.FetchAsync("Ayşe");

        first.Should().Be(new InboxFetchResult(2, 1, 0, 0, true));
        (await _repository.GetInboxCursorAsync(PurchaseInvoiceSources.QnbInbox)).Should().BeLessThan(flakySequence);

        var second = await _inbox.FetchAsync("Ayşe");

        second.Should().Be(new InboxFetchResult(2, 2, 0, 0, false));
        (await _repository.GetInboxCursorAsync(PurchaseInvoiceSources.QnbInbox)).Should().Be(afterSequence);
        _source.Downloads.Should().Contain([good.ToString(), flaky.ToString(), after.ToString()]);
    }

    [Fact]
    public async Task MoreDocumentsThanOnePageAreAllFetched()
    {
        for (var i = 0; i < 5; i++)
            Offer();

        var result = await _inbox.FetchAsync("Ayşe");

        result.Imported.Should().Be(5);
        result.StoppedEarly.Should().BeFalse();
    }

    [Fact]
    public async Task TheCursorNeverMovesBackwards()
    {
        var high = NextSequence() + 500;
        Interlocked.Exchange(ref _sequence, high);
        await _repository.SaveInboxCursorAsync(PurchaseInvoiceSources.QnbInbox, high);

        await _repository.SaveInboxCursorAsync(PurchaseInvoiceSources.QnbInbox, 5);

        (await _repository.GetInboxCursorAsync(PurchaseInvoiceSources.QnbInbox)).Should().Be(high);
    }

    [Fact]
    public async Task AnUnconfiguredSourceIsReportedBeforeAnythingIsFetched()
    {
        var inbox = new PurchaseInvoiceInboxService(new ClosedSource(), _invoices, _repository);

        var act = () => inbox.FetchAsync("Ayşe");

        await act.Should().ThrowAsync<PurchaseInvoiceSourceNotConfiguredException>();
    }

    private sealed class ClosedSource : IPurchaseInvoiceInboxSource
    {
        public Task<IPurchaseInvoiceInboxSession> OpenAsync(CancellationToken ct = default) => throw new PurchaseInvoiceSourceNotConfiguredException();
    }

    private sealed class FakeSource : IPurchaseInvoiceInboxSource, IPurchaseInvoiceInboxSession
    {
        private const int PageSize = 2;

        public List<(InboxDocument Document, string Xml)> Documents { get; } = [];

        public List<string> Downloads { get; } = [];

        public HashSet<string> FailOnce { get; } = [];

        public Task<IPurchaseInvoiceInboxSession> OpenAsync(CancellationToken ct = default) => Task.FromResult<IPurchaseInvoiceInboxSession>(this);

        public Task<IReadOnlyList<InboxDocument>> ListAfterAsync(long afterSequenceNo, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<InboxDocument>>(
                Documents.Select(d => d.Document).Where(d => d.SequenceNo > afterSequenceNo).OrderBy(d => d.SequenceNo).Take(PageSize).ToArray());

        public Task<string> DownloadXmlAsync(string ettn, CancellationToken ct = default)
        {
            if (FailOnce.Remove(ettn))
                throw new PurchaseInvoiceSourceUnavailableException("temporary");
            Downloads.Add(ettn);
            return Task.FromResult(Documents.Single(d => d.Document.Ettn == ettn).Xml);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
