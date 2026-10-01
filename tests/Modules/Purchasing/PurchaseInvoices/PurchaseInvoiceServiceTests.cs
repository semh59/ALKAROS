using ALKAROS.Inventory.StockMaster;
using ALKAROS.Purchasing.Suppliers;
using FluentAssertions;
using Xunit;
using static ALKAROS.Purchasing.PurchaseInvoices.Tests.InvoiceXml;

namespace ALKAROS.Purchasing.PurchaseInvoices.Tests;

public sealed class PurchaseInvoiceServiceTests : IClassFixture<PurchaseInvoiceTestDatabase>
{
    private readonly PurchaseInvoiceTestDatabase _database;
    private readonly PostgresPurchaseInvoiceRepository _repository;
    private readonly PurchaseInvoiceService _service;

    public PurchaseInvoiceServiceTests(PurchaseInvoiceTestDatabase database)
    {
        _database = database;
        _repository = new PostgresPurchaseInvoiceRepository(database.DataSource);
        _service = new PurchaseInvoiceService(
            _repository, new PostgresSupplierRepository(database.DataSource), new PostgresStockItemRepository(database.DataSource));
    }

    private static string NewTax() => Random.Shared.NextInt64(1_000_000_000, 9_999_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static Line Meat(string unit = "KGM", string quantity = "10", string net = "1500.00") => new("1", "KIYMA-01", "Dana Kıyma", quantity, unit, net);

    [Fact]
    public async Task AnImportedInvoiceIsStoredAsADraftWithItsLinesAndTheRawXml()
    {
        var tax = NewTax();
        var supplierId = await _database.SeedSupplierAsync(tax);

        var invoice = await _service.ImportAsync(Build(Guid.NewGuid(), tax, lines: [Meat()]), "Ayşe", PurchaseInvoiceSources.XmlUpload);

        var stored = await _service.GetAsync(invoice.InvoiceId);
        stored.Status.Should().Be("Draft");
        stored.SupplierId.Should().Be(supplierId);
        stored.ImportedBy.Should().Be("Ayşe");
        stored.Lines.Should().ContainSingle().Which.Should().Match<PurchaseInvoiceLine>(l =>
            l.Quantity == 10m && l.UnitCode == "KGM" && l.UnitPrice == 150m && l.StockItemId == null);
        await using var command = _database.DataSource.CreateCommand("SELECT raw_xml FROM purchasing.purchase_invoices WHERE invoice_id = @id;");
        command.Parameters.AddWithValue("id", invoice.InvoiceId);
        ((string?)await command.ExecuteScalarAsync()).Should().Contain("KIYMA-01");
    }

    [Fact]
    public async Task ASupplierNobodyHasRegisteredStaysUnlinked()
    {
        var invoice = await _service.ImportAsync(Build(Guid.NewGuid(), NewTax()), "Ayşe", PurchaseInvoiceSources.XmlUpload);

        invoice.SupplierId.Should().BeNull();
    }

    [Fact]
    public async Task TheSameEttnCannotBeImportedTwice()
    {
        var ettn = Guid.NewGuid();
        var tax = NewTax();
        await _service.ImportAsync(Build(ettn, tax), "Ayşe", PurchaseInvoiceSources.XmlUpload);

        var again = () => _service.ImportAsync(Build(ettn, tax, number: "ZZZ"), "Ayşe", PurchaseInvoiceSources.XmlUpload);

        await again.Should().ThrowAsync<DuplicatePurchaseInvoiceException>();
    }

    [Fact]
    public async Task AMappingIsRememberedForTheSuppliersItemAndUnitOnTheNextInvoice()
    {
        var tax = NewTax();
        var meat = await _database.SeedStockItemAsync("Dana kıyma");
        var first = await _service.ImportAsync(Build(Guid.NewGuid(), tax, lines: [Meat()]), "Ayşe", PurchaseInvoiceSources.XmlUpload);
        await _service.MapLineAsync(first.InvoiceId, first.Lines[0].LineId, meat, 1m);

        var next = await _service.ImportAsync(Build(Guid.NewGuid(), tax, lines: [Meat(quantity: "4", net: "600.00")]), "Ayşe", PurchaseInvoiceSources.XmlUpload);
        var otherUnit = await _service.ImportAsync(Build(Guid.NewGuid(), tax, lines: [Meat(unit: "C62")]), "Ayşe", PurchaseInvoiceSources.XmlUpload);
        var otherSupplier = await _service.ImportAsync(Build(Guid.NewGuid(), NewTax(), lines: [Meat()]), "Ayşe", PurchaseInvoiceSources.XmlUpload);

        next.Lines[0].StockItemId.Should().Be(meat);
        next.Lines[0].ConversionFactor.Should().Be(1m);
        otherUnit.Lines[0].StockItemId.Should().BeNull();
        otherSupplier.Lines[0].StockItemId.Should().BeNull();
    }

    [Fact]
    public async Task MappingALineAlsoMapsTheSameItemOnOtherDraftInvoicesOfThatSupplier()
    {
        var tax = NewTax();
        var box = await _database.SeedStockItemAsync("Süt", "l");
        var first = await _service.ImportAsync(Build(Guid.NewGuid(), tax, lines: [Meat()]), "Ayşe", PurchaseInvoiceSources.XmlUpload);
        var second = await _service.ImportAsync(Build(Guid.NewGuid(), tax, lines: [Meat()]), "Ayşe", PurchaseInvoiceSources.XmlUpload);
        var approved = await _service.ImportAsync(Build(Guid.NewGuid(), tax, lines: [Meat()]), "Ayşe", PurchaseInvoiceSources.XmlUpload);
        await _database.SetStatusAsync(approved.InvoiceId, "Approved");

        await _service.MapLineAsync(first.InvoiceId, first.Lines[0].LineId, box, 24m);

        (await _service.GetAsync(second.InvoiceId)).Lines[0].ConversionFactor.Should().Be(24m);
        (await _service.GetAsync(approved.InvoiceId)).Lines[0].StockItemId.Should().BeNull();
    }

    [Fact]
    public async Task RemappingReplacesTheRememberedChoice()
    {
        var tax = NewTax();
        var meat = await _database.SeedStockItemAsync("Kıyma A");
        var other = await _database.SeedStockItemAsync("Kıyma B");
        var first = await _service.ImportAsync(Build(Guid.NewGuid(), tax, lines: [Meat()]), "Ayşe", PurchaseInvoiceSources.XmlUpload);
        await _service.MapLineAsync(first.InvoiceId, first.Lines[0].LineId, meat, 1m);
        var second = await _service.ImportAsync(Build(Guid.NewGuid(), tax, lines: [Meat()]), "Ayşe", PurchaseInvoiceSources.XmlUpload);

        await _service.MapLineAsync(second.InvoiceId, second.Lines[0].LineId, other, 2m);

        var third = await _service.ImportAsync(Build(Guid.NewGuid(), tax, lines: [Meat()]), "Ayşe", PurchaseInvoiceSources.XmlUpload);
        third.Lines[0].StockItemId.Should().Be(other);
        third.Lines[0].ConversionFactor.Should().Be(2m);
    }

    [Fact]
    public async Task OnlyADraftCanBeMapped()
    {
        var meat = await _database.SeedStockItemAsync("Kıyma");
        var invoice = await _service.ImportAsync(Build(Guid.NewGuid(), NewTax(), lines: [Meat()]), "Ayşe", PurchaseInvoiceSources.XmlUpload);
        await _database.SetStatusAsync(invoice.InvoiceId, "Rejected");

        var act = () => _service.MapLineAsync(invoice.InvoiceId, invoice.Lines[0].LineId, meat, 1m);

        await act.Should().ThrowAsync<PurchaseInvoiceStatusException>();
    }

    [Fact]
    public async Task AnUnknownStockItemOrANonPositiveFactorIsRefused()
    {
        var meat = await _database.SeedStockItemAsync("Kıyma");
        var invoice = await _service.ImportAsync(Build(Guid.NewGuid(), NewTax(), lines: [Meat()]), "Ayşe", PurchaseInvoiceSources.XmlUpload);

        var unknown = () => _service.MapLineAsync(invoice.InvoiceId, invoice.Lines[0].LineId, Guid.NewGuid(), 1m);
        var zero = () => _service.MapLineAsync(invoice.InvoiceId, invoice.Lines[0].LineId, meat, 0m);
        var unknownLine = () => _service.MapLineAsync(invoice.InvoiceId, Guid.NewGuid(), meat, 1m);

        await unknown.Should().ThrowAsync<InvalidPurchaseInvoiceException>();
        await zero.Should().ThrowAsync<InvalidPurchaseInvoiceException>();
        await unknownLine.Should().ThrowAsync<PurchaseInvoiceNotFoundException>();
    }

    [Fact]
    public async Task TheListCountsUnmappedLinesAndFiltersByStatus()
    {
        var tax = NewTax();
        var meat = await _database.SeedStockItemAsync("Kıyma");
        var invoice = await _service.ImportAsync(Build(Guid.NewGuid(), tax, lines:
            [Meat(), new Line("2", "SUT", "Süt", "6", "LTR", "60.00")]), "Ayşe", PurchaseInvoiceSources.XmlUpload);
        await _service.MapLineAsync(invoice.InvoiceId, invoice.Lines[0].LineId, meat, 1m);

        var drafts = await _service.ListAsync("Draft");
        var approved = await _service.ListAsync("Approved");

        var summary = drafts.Single(s => s.InvoiceId == invoice.InvoiceId);
        summary.LineCount.Should().Be(2);
        summary.UnmappedLineCount.Should().Be(1);
        summary.NetTotal.Should().Be(1560m);
        approved.Should().NotContain(s => s.InvoiceId == invoice.InvoiceId);
        var unknownStatus = () => _service.ListAsync("Weird");
        await unknownStatus.Should().ThrowAsync<InvalidPurchaseInvoiceException>();
    }
}
