using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Purchasing.Suppliers;
using FluentAssertions;
using Xunit;
using static ALKAROS.Purchasing.PurchaseInvoices.Tests.InvoiceXml;

namespace ALKAROS.Purchasing.PurchaseInvoices.Tests;

public sealed class PurchaseInvoiceApprovalTests : IClassFixture<PurchaseInvoiceTestDatabase>
{
    private readonly PurchaseInvoiceTestDatabase _database;
    private readonly PurchaseInvoiceService _imports;
    private readonly PurchaseInvoiceApprovalService _approvals;

    public PurchaseInvoiceApprovalTests(PurchaseInvoiceTestDatabase database)
    {
        _database = database;
        var repository = new PostgresPurchaseInvoiceRepository(database.DataSource);
        var suppliers = new PostgresSupplierRepository(database.DataSource);
        var stockItems = new PostgresStockItemRepository(database.DataSource);
        _imports = new PurchaseInvoiceService(repository, suppliers, stockItems);
        _approvals = new PurchaseInvoiceApprovalService(
            repository, suppliers, stockItems, database.DataSource,
            new PostgresStockBalanceRepository(database.DataSource), new PostgresStockMovementRepository(database.DataSource));
    }

    private static string NewTax() => Random.Shared.NextInt64(1_000_000_000, 9_999_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private async Task<(PurchaseInvoice Invoice, Guid Stock, Guid Location)> MappedBoxInvoiceAsync(string tax, decimal factor = 24m)
    {
        await _database.SeedSupplierAsync(tax);
        var stock = await _database.SeedStockItemAsync("Süt", "l");
        var location = await _database.SeedLocationAsync();
        var invoice = await _imports.ImportAsync(
            Build(Guid.NewGuid(), tax, issueDate: "2026-09-20", lines: [new Line("1", "SUT-KOLI", "Süt koli", "5", "BX", "600.00")]),
            "Ayşe", PurchaseInvoiceSources.XmlUpload);
        await _imports.MapLineAsync(invoice.InvoiceId, invoice.Lines[0].LineId, stock, factor);
        return (invoice, stock, location);
    }

    [Fact]
    public async Task ApprovingPostsTheConvertedQuantityAndTheStockUnitPriceInOneGoodsReceipt()
    {
        var (invoice, stock, location) = await MappedBoxInvoiceAsync(NewTax());

        var receiptId = await _approvals.ApproveAsync(invoice.InvoiceId, location, "Müdür");

        (await _imports.GetAsync(invoice.InvoiceId)).Status.Should().Be("Approved");
        var item = await _database.ReadReceiptItemAsync(receiptId);
        item.Quantity.Should().Be(120m);
        item.Unit.Should().Be("l");
        item.UnitPrice.Should().Be(5m);
        item.ReceivedAtDate.Should().Be(new DateOnly(2026, 9, 20));
        (await _database.ReadOnHandAsync(stock, location)).Should().Be(120m);
        (await _database.CountMovementsAsync(receiptId)).Should().Be(1);
    }

    [Fact]
    public async Task ApprovingTwiceDoesNotPostStockAgain()
    {
        var (invoice, stock, location) = await MappedBoxInvoiceAsync(NewTax());
        await _approvals.ApproveAsync(invoice.InvoiceId, location, "Müdür");

        var again = () => _approvals.ApproveAsync(invoice.InvoiceId, location, "Müdür");

        await again.Should().ThrowAsync<PurchaseInvoiceStatusException>();
        (await _database.ReadOnHandAsync(stock, location)).Should().Be(120m);
    }

    [Fact]
    public async Task AnInvoiceWithAnUnmappedLineIsNotApproved()
    {
        var tax = NewTax();
        await _database.SeedSupplierAsync(tax);
        var location = await _database.SeedLocationAsync();
        var invoice = await _imports.ImportAsync(Build(Guid.NewGuid(), tax), "Ayşe", PurchaseInvoiceSources.XmlUpload);

        var act = () => _approvals.ApproveAsync(invoice.InvoiceId, location, "Müdür");

        await act.Should().ThrowAsync<PurchaseInvoiceNotReadyException>();
        (await _imports.GetAsync(invoice.InvoiceId)).Status.Should().Be("Draft");
    }

    [Fact]
    public async Task AnInvoiceFromAnUnregisteredSupplierIsNotApprovedUntilTheSupplierExists()
    {
        var tax = NewTax();
        var stock = await _database.SeedStockItemAsync("Un", "kg");
        var location = await _database.SeedLocationAsync();
        var invoice = await _imports.ImportAsync(Build(Guid.NewGuid(), tax), "Ayşe", PurchaseInvoiceSources.XmlUpload);
        await _imports.MapLineAsync(invoice.InvoiceId, invoice.Lines[0].LineId, stock, 1m);

        var before = () => _approvals.ApproveAsync(invoice.InvoiceId, location, "Müdür");
        await before.Should().ThrowAsync<PurchaseInvoiceNotReadyException>();

        await _database.SeedSupplierAsync(tax);
        await _approvals.ApproveAsync(invoice.InvoiceId, location, "Müdür");
        (await _imports.GetAsync(invoice.InvoiceId)).Status.Should().Be("Approved");
    }

    [Fact]
    public async Task AFailureWhilePostingStockLeavesTheInvoiceADraftAndWritesNoReceipt()
    {
        var (invoice, _, _) = await MappedBoxInvoiceAsync(NewTax());

        var act = () => _approvals.ApproveAsync(invoice.InvoiceId, Guid.NewGuid(), "Müdür");

        await act.Should().ThrowAsync<Exception>();
        (await _imports.GetAsync(invoice.InvoiceId)).Status.Should().Be("Draft");
        (await _database.CountReceiptsAsync(invoice.InvoiceId)).Should().Be(0);
    }

    [Fact]
    public async Task OnlyADraftCanBeRejectedAndARejectedInvoiceCannotBeApproved()
    {
        var (invoice, _, location) = await MappedBoxInvoiceAsync(NewTax());

        await _approvals.RejectAsync(invoice.InvoiceId);

        (await _imports.GetAsync(invoice.InvoiceId)).Status.Should().Be("Rejected");
        var reject = () => _approvals.RejectAsync(invoice.InvoiceId);
        var approve = () => _approvals.ApproveAsync(invoice.InvoiceId, location, "Müdür");
        await reject.Should().ThrowAsync<PurchaseInvoiceStatusException>();
        await approve.Should().ThrowAsync<PurchaseInvoiceStatusException>();
    }

    [Fact]
    public async Task AnUnknownInvoiceIsNotFound()
    {
        var act = () => _approvals.ApproveAsync(Guid.NewGuid(), Guid.NewGuid(), "Müdür");

        await act.Should().ThrowAsync<PurchaseInvoiceNotFoundException>();
    }
}
