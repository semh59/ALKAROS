using ALKAROS.Invoicing.Generation.OrderInvoices;
using ALKAROS.Invoicing.Generation.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace ALKAROS.Invoicing.Generation.Tests;

/// <summary>
/// An online order's invoice draft on a real PostgreSQL with every migration: menu prices are tax-inclusive so the
/// lines reconcile to the gross, the seller is copied onto the draft, an order is drafted once even under
/// concurrency, a missing seller profile drafts nothing, and the draft cannot be changed.
/// </summary>
public sealed class OrderInvoiceDraftTests : IAsyncLifetime
{
    private readonly InvoiceGenerationTestDatabase _database = new();
    private PostgresSellerProfileStore _sellers = null!;
    private PostgresOrderInvoiceDraftService _drafts = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _sellers = new PostgresSellerProfileStore(_database.DataSource);
        _drafts = new PostgresOrderInvoiceDraftService(_database.DataSource, _sellers);
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    private static SellerProfile Seller() => new(
        "Deniz Lokantası Ltd. Şti.", SellerProfile.Vkn, "1234567890", "Kadıköy", "Moda Cad. 1", "Kadıköy", "İstanbul", null);

    private static OrderInvoiceInput Order(Guid? orderId = null, params OrderInvoiceLineInput[] lines) => new(
        orderId ?? Guid.NewGuid(), "yemeksepeti", "ys-1001", "YS-1001", new DateOnly(2026, 9, 30),
        "https://www.yemeksepeti.com", "KREDIKARTI/BANKAKARTI", new DateOnly(2026, 9, 30), "Yemeksepeti", "1234567891",
        lines.Length > 0 ? lines : [new("Köfte", 1m, 10m, 90.91m, 9.09m, 100m)]);

    [Fact]
    public async Task ATaxInclusiveOrderIsDraftedWithLinesThatReconcileToTheGross()
    {
        await _sellers.SaveAsync(Seller(), null);
        var input = Order(null, new OrderInvoiceLineInput("Köfte", 1m, 10m, 90.91m, 9.09m, 100m), new OrderInvoiceLineInput("Ayran", 2m, 20m, 33.33m, 6.67m, 40m));

        var result = await _drafts.CreateAsync(input, Guid.NewGuid());

        Assert.False(result.WasAlreadyCreated);
        var invoice = result.Invoice;
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.Equal(124.24m, invoice.LineExtensionAmount);
        Assert.Equal(15.76m, invoice.TaxTotal);
        Assert.Equal(140m, invoice.PayableAmount);
        Assert.Equal(invoice.PayableAmount, invoice.Lines.Sum(line => line.GrossAmount));
        Assert.All(invoice.Lines, line => Assert.Equal(line.GrossAmount, line.NetAmount + line.TaxAmount));
        Assert.Equal([1, 2], invoice.Lines.Select(line => line.LineNumber));
        Assert.Equal("Ayran", invoice.Lines[1].Description);
        Assert.Equal(2m, invoice.Lines[1].Quantity);
        Assert.Equal("yemeksepeti", invoice.Provider);
        Assert.Equal("https://www.yemeksepeti.com", invoice.WebAddress);
        Assert.Equal("KREDIKARTI/BANKAKARTI", invoice.PaymentMethod);
        Assert.Equal(new DateOnly(2026, 9, 30), invoice.ServiceDate);
        Assert.Equal("FinalConsumer", await TextAsync("SELECT buyer_kind FROM invoicing.order_invoices;"));
        Assert.Equal("EARSIVFATURA", await TextAsync("SELECT ubl_profile_id FROM invoicing.order_invoices;"));
    }

    [Fact]
    public async Task TheSellerIsCopiedOntoTheDraftAndLaterEditsDoNotChangeIt()
    {
        await _sellers.SaveAsync(Seller(), null);
        var input = Order();
        var first = await _drafts.CreateAsync(input, null);

        await _sellers.SaveAsync(Seller() with { LegalName = "Başka Ünvan A.Ş." }, null);
        var again = await _drafts.GetByOrderAsync(input.OrderId);

        Assert.Equal("Deniz Lokantası Ltd. Şti.", first.Invoice.Seller.LegalName);
        Assert.Equal("Moda Cad. 1, Kadıköy/İstanbul", first.Invoice.Seller.Address);
        Assert.Equal("Deniz Lokantası Ltd. Şti.", again!.Seller.LegalName);
    }

    [Fact]
    public async Task ARepeatedCallReturnsTheSameInvoice()
    {
        await _sellers.SaveAsync(Seller(), null);
        var input = Order();

        var first = await _drafts.CreateAsync(input, null);
        var second = await _drafts.CreateAsync(input, null);

        Assert.True(second.WasAlreadyCreated);
        Assert.Equal(first.Invoice.InvoiceId, second.Invoice.InvoiceId);
        Assert.Equal(1L, await CountAsync("SELECT count(*) FROM invoicing.order_invoices;"));
        Assert.Equal(1L, await CountAsync("SELECT count(*) FROM invoicing.order_invoice_lines;"));
    }

    [Fact]
    public async Task ConcurrentCallsForOneOrderDraftExactlyOneInvoice()
    {
        await _sellers.SaveAsync(Seller(), null);
        var input = Order();

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => _drafts.CreateAsync(input, null)));

        Assert.Single(results, result => !result.WasAlreadyCreated);
        Assert.Single(results.Select(result => result.Invoice.InvoiceId).Distinct());
        Assert.Equal(1L, await CountAsync("SELECT count(*) FROM invoicing.order_invoices;"));
    }

    [Fact]
    public async Task WithoutACompleteSellerProfileNothingIsDrafted()
    {
        var input = Order();

        await Assert.ThrowsAsync<SellerProfileMissingException>(() => _drafts.CreateAsync(input, null));
        Assert.Equal(0L, await CountAsync("SELECT count(*) FROM invoicing.order_invoices;"));

        await _sellers.SaveAsync(Seller(), null);
        Assert.False((await _drafts.CreateAsync(input, null)).WasAlreadyCreated);
    }

    [Theory]
    [InlineData("net-plus-tax-is-not-gross")]
    [InlineData("zero-gross")]
    [InlineData("unknown-provider")]
    public async Task AnInvalidOrderIsRefusedAndNothingIsStored(string problem)
    {
        await _sellers.SaveAsync(Seller(), null);
        var input = problem switch
        {
            "net-plus-tax-is-not-gross" => Order(null, new OrderInvoiceLineInput("Köfte", 1m, 10m, 90m, 9.09m, 100m)),
            "zero-gross" => Order(null, new OrderInvoiceLineInput("Su", 1m, 10m, 0m, 0m, 0m)),
            _ => Order() with { Provider = "getir" },
        };

        await Assert.ThrowsAsync<ArgumentException>(() => _drafts.CreateAsync(input, null));
        Assert.Equal(0L, await CountAsync("SELECT count(*) FROM invoicing.order_invoices;"));
    }

    [Fact]
    public async Task AnOrderWithNoLineIsRefused()
    {
        await _sellers.SaveAsync(Seller(), null);
        var input = Order() with { Lines = [] };

        await Assert.ThrowsAsync<OrderInvoiceNothingToInvoiceException>(() => _drafts.CreateAsync(input, null));
    }

    [Fact]
    public async Task ADraftCannotBeChangedOrDeleted()
    {
        await _sellers.SaveAsync(Seller(), null);
        var invoice = (await _drafts.CreateAsync(Order(), null)).Invoice;

        await AssertRefusedAsync($"UPDATE invoicing.order_invoices SET payable_amount = 1, line_extension_amount = 1, tax_total = 0 WHERE invoice_id = '{invoice.InvoiceId}';");
        await AssertRefusedAsync($"UPDATE invoicing.order_invoices SET seller_legal_name = 'X' WHERE invoice_id = '{invoice.InvoiceId}';");
        await AssertRefusedAsync($"DELETE FROM invoicing.order_invoices WHERE invoice_id = '{invoice.InvoiceId}';");
        await AssertRefusedAsync($"UPDATE invoicing.order_invoice_lines SET description = 'X' WHERE invoice_id = '{invoice.InvoiceId}';");
        await AssertRefusedAsync($"DELETE FROM invoicing.order_invoice_lines WHERE invoice_id = '{invoice.InvoiceId}';");
    }

    [Fact]
    public async Task TheDatabaseRefusesTotalsThatDoNotReconcile()
    {
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            """
            INSERT INTO invoicing.order_invoices
                (invoice_id, order_id, provider, external_order_id, order_number, profile, ubl_profile_id, invoice_type_code,
                 currency_code, issue_date, service_date, buyer_kind, seller_legal_name, seller_tax_id_kind, seller_tax_id_number,
                 seller_tax_office, seller_address, web_address, line_extension_amount, tax_total, payable_amount)
            VALUES (gen_random_uuid(), gen_random_uuid(), 'yemeksepeti', 'x', 'x', 'EArsiv', 'EARSIVFATURA', 'SATIS',
                    'TRY', current_date, current_date, 'FinalConsumer', 'S', 'Vkn', '1234567890',
                    'K', 'A', 'https://x', 90, 9, 100);
            """));
    }

    private async Task AssertRefusedAsync(string sql)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(sql));
        Assert.Equal("23000", exception.SqlState[..2] + "000");
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var command = _database.DataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<long> CountAsync(string sql)
    {
        await using var command = _database.DataSource.CreateCommand(sql);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<string> TextAsync(string sql)
    {
        await using var command = _database.DataSource.CreateCommand(sql);
        return (string)(await command.ExecuteScalarAsync())!;
    }
}
