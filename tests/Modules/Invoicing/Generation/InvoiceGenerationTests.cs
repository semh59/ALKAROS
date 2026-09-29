using System.Security.Cryptography;
using System.Text;
using ALKAROS.CustomerData.Profiles;
using ALKAROS.Invoicing.Generation.Tests.Fixtures;
using ALKAROS.Invoicing.SourceSelection;
using ALKAROS.Secrets;
using Npgsql;
using Xunit;

namespace ALKAROS.Invoicing.Generation.Tests;

/// <summary>
/// V14-INV-002 on a real PostgreSQL with every migration: KDV groups reconcile with the charged bills, only charges
/// are invoiced, the ledger is never written, retries and concurrent calls give one invoice, the buyer is a sealed
/// snapshot, and the draft cannot be changed.
/// </summary>
public sealed class InvoiceGenerationTests : IAsyncLifetime
{
    // August 2026 in Istanbul (UTC+3).
    private static readonly DateOnly August = new(2026, 8, 1);
    private static readonly DateOnly September = new(2026, 9, 1);
    private static readonly DateTimeOffset InAugust = new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly InvoiceGenerationTestDatabase _database = new();
    private readonly Guid _operator = Guid.NewGuid();
    private PostgresCustomerProfileStore _profiles = null!;
    private PostgresInvoiceSourceSelectionService _selection = null!;
    private PostgresInvoiceGenerationService _generation = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        var secrets = new InMemorySecretProvider();
        secrets.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        _profiles = new PostgresCustomerProfileStore(_database.DataSource, secrets);
        _selection = new PostgresInvoiceSourceSelectionService(_database.DataSource);
        _generation = new PostgresInvoiceGenerationService(_database.DataSource, _profiles, secrets);
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    private Task<Guid> CompanyAsync(string name = "Deniz Gıda Ltd.")
        => _profiles.CreateAsync(new CreateCustomerProfileRequest(
            name, "05551234567", "muhasebe@deniz.example", "Moda Cad. 1, Kadıköy",
            CustomerTaxIdentity.Create(CustomerTaxIdKind.Vkn, "1234567890", "Kadıköy")));

    private async Task<Guid> SelectAugustAsync(Guid customerId)
    {
        var period = await _selection.ClosePeriodAsync(August, September, _operator);
        return (await _selection.SelectAsync(period.PeriodId, customerId)).SourceSet.SourceSetId;
    }

    [Fact]
    public async Task AFullyChargedBillBecomesOneLinePerKdvRateThatReconcilesWithTheCharge()
    {
        var customer = await CompanyAsync();
        var bill = await _database.SeedBillAsync((10m, 110m), (20m, 120m));
        await _database.SeedChargeAsync(customer, bill, 230m, InAugust);
        var setId = await SelectAugustAsync(customer);

        var result = await _generation.GenerateAsync(setId, InvoiceProfile.EFatura, _operator);

        var invoice = result.Invoice;
        Assert.False(result.WasAlreadyGenerated);
        Assert.Equal(setId, invoice.SourceSetId);
        Assert.Equal(customer, invoice.CustomerId);
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.Equal("TEMELFATURA", invoice.UblProfileId);
        Assert.Equal("SATIS", invoice.InvoiceTypeCode);
        Assert.Equal("TRY", invoice.CurrencyCode);
        Assert.Equal(_operator, invoice.CreatedBy);
        Assert.Collection(
            invoice.Lines,
            line => Assert.Equal(
                new InvoiceDraftLine(1, "Restoran hizmet bedeli, 01.08.2026-31.08.2026 dönemi (KDV %20)", 1m, "C62", 20m, 100m, 20m, 120m),
                line),
            line => Assert.Equal(
                new InvoiceDraftLine(2, "Restoran hizmet bedeli, 01.08.2026-31.08.2026 dönemi (KDV %10)", 1m, "C62", 10m, 100m, 10m, 110m),
                line));
        Assert.Equal(200m, invoice.LineExtensionAmount);
        Assert.Equal(30m, invoice.TaxTotal);
        Assert.Equal(230m, invoice.PayableAmount);
    }

    [Fact]
    public async Task PartialChargesOnSeveralBillsAreSplitByEachBillsRatesAndAddUpToWhatWasCharged()
    {
        var customer = await CompanyAsync();
        var mixed = await _database.SeedBillAsync((10m, 110m), (20m, 120m));
        var drinks = await _database.SeedBillAsync((20m, 60m));
        await _database.SeedChargeAsync(customer, mixed, 100m, InAugust); // the rest of this bill was paid in cash
        await _database.SeedChargeAsync(customer, drinks, 60m, InAugust.AddDays(1));
        var setId = await SelectAugustAsync(customer);

        var invoice = (await _generation.GenerateAsync(setId, InvoiceProfile.EFatura, _operator)).Invoice;

        // 100 on the mixed bill splits 52.17 (20%) / 47.83 (10%); the drinks bill adds 60 at 20%.
        Assert.Equal([20m, 10m], invoice.Lines.Select(line => line.TaxRate));
        Assert.Equal(112.17m, invoice.Lines[0].GrossAmount);
        Assert.Equal(18.70m, invoice.Lines[0].TaxAmount); // 112.17 x 20/120 = 18.695 -> 18.70
        Assert.Equal(47.83m, invoice.Lines[1].GrossAmount);
        Assert.Equal(4.35m, invoice.Lines[1].TaxAmount); // 47.83 x 10/110 = 4.348 -> 4.35
        Assert.Equal(160m, invoice.PayableAmount);
        Assert.All(invoice.Lines, line => Assert.Equal(line.GrossAmount, line.NetAmount + line.TaxAmount));
        Assert.Equal(invoice.PayableAmount, invoice.LineExtensionAmount + invoice.TaxTotal);
    }

    [Fact]
    public async Task PaymentsInTheSetAreNotInvoicedAndGenerationWritesNothingToTheLedger()
    {
        var customer = await CompanyAsync();
        var bill = await _database.SeedBillAsync((10m, 250m));
        await _database.SeedChargeAsync(customer, bill, 250m, InAugust);
        await _database.RecordAsync(customer, "Payment", 80m, InAugust.AddDays(2));
        var setId = await SelectAugustAsync(customer);
        var ledgerRows = await _database.CountAsync("customer_account.account_transactions");
        var ledgerSum = await _database.LedgerSumAsync(customer);

        var invoice = (await _generation.GenerateAsync(setId, InvoiceProfile.EArsiv, _operator)).Invoice;

        Assert.Equal(250m, invoice.PayableAmount);
        Assert.Equal("EARSIVFATURA", invoice.UblProfileId);
        Assert.Equal(ledgerRows, await _database.CountAsync("customer_account.account_transactions"));
        Assert.Equal(ledgerSum, await _database.LedgerSumAsync(customer));
    }

    [Fact]
    public async Task ADiscountLowersItsRatesShareAndATipIsNotAKdvWeight()
    {
        var customer = await CompanyAsync();
        var bill = await _database.SeedBillAsync((10m, 100m), (20m, 100m));
        await _database.SeedAdjustmentAsync(bill, "DiscountAmount", 20m, 50m, isDeduction: true);
        await _database.SeedAdjustmentAsync(bill, "Tip", 0m, 30m, isDeduction: false);
        await _database.SeedChargeAsync(customer, bill, 150m, InAugust);
        var setId = await SelectAugustAsync(customer);

        var invoice = (await _generation.GenerateAsync(setId, InvoiceProfile.EFatura, _operator)).Invoice;

        // Weights: 10% -> 100, 20% -> 100 - 50 = 50; the tip adds no 0% line.
        Assert.Equal([20m, 10m], invoice.Lines.Select(line => line.TaxRate));
        Assert.Equal(50m, invoice.Lines[0].GrossAmount);
        Assert.Equal(100m, invoice.Lines[1].GrossAmount);
    }

    [Fact]
    public async Task ARetryReturnsTheSameInvoiceAndAnotherProfileIsRefused()
    {
        var customer = await CompanyAsync();
        await _database.SeedChargeAsync(customer, await _database.SeedBillAsync((10m, 55m)), 55m, InAugust);
        var setId = await SelectAugustAsync(customer);

        var first = await _generation.GenerateAsync(setId, InvoiceProfile.EFatura, _operator);
        var retry = await _generation.GenerateAsync(setId, InvoiceProfile.EFatura, Guid.NewGuid());

        Assert.True(retry.WasAlreadyGenerated);
        Assert.Equal(first.Invoice, retry.Invoice with { Lines = first.Invoice.Lines });
        Assert.Equal(first.Invoice.Lines, retry.Invoice.Lines);
        var other = await Assert.ThrowsAsync<InvoiceAlreadyGeneratedAsAnotherProfileException>(
            () => _generation.GenerateAsync(setId, InvoiceProfile.EArsiv, _operator));
        Assert.Equal(InvoiceProfile.EFatura, other.ExistingProfile);
        Assert.Equal(1, await _database.CountAsync("invoicing.invoices"));
    }

    [Fact]
    public async Task ConcurrentGenerationOfOneSetCreatesExactlyOneInvoice()
    {
        var customer = await CompanyAsync();
        await _database.SeedChargeAsync(customer, await _database.SeedBillAsync((10m, 99.99m)), 99.99m, InAugust);
        var setId = await SelectAugustAsync(customer);

        var results = await Task.WhenAll(Enumerable.Range(0, 6)
            .Select(_ => Task.Run(() => _generation.GenerateAsync(setId, InvoiceProfile.EFatura, _operator))));

        Assert.Single(results.Select(result => result.Invoice.InvoiceId).Distinct());
        Assert.Single(results, result => !result.WasAlreadyGenerated);
        Assert.Equal(1, await _database.CountAsync("invoicing.invoices"));
        Assert.Equal(1, await _database.CountAsync("invoicing.invoice_lines"));
    }

    [Fact]
    public async Task TheBuyerIsASealedSnapshotThatLaterCustomerEditsDoNotChange()
    {
        var customer = await CompanyAsync();
        await _database.SeedChargeAsync(customer, await _database.SeedBillAsync((10m, 40m)), 40m, InAugust);
        var setId = await SelectAugustAsync(customer);
        var invoice = (await _generation.GenerateAsync(setId, InvoiceProfile.EFatura, _operator)).Invoice;

        var current = (await _profiles.GetAsync(customer, CustomerAccessRole.Manager))!;
        await _profiles.UpdateContactAsync(
            customer,
            new UpdateCustomerContactRequest("Yeni Unvan A.Ş.", null, null, null,
                CustomerTaxIdentity.Create(CustomerTaxIdKind.Vkn, "9876543217", "Çankaya")),
            current.RowVersion);
        var reread = (await _generation.GetAsync(invoice.InvoiceId))!;

        Assert.Equal(
            new InvoiceBuyerSnapshot("Deniz Gıda Ltd.", "Vkn", "1234567890", "Kadıköy", "Moda Cad. 1, Kadıköy", "muhasebe@deniz.example"),
            reread.Buyer);
        var raw = Encoding.Latin1.GetString(await _database.RawBuyerEnvelopeAsync(invoice.InvoiceId));
        Assert.DoesNotContain("1234567890", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("muhasebe@deniz.example", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACustomerWithoutATaxIdentityOrAnonymizedCannotBeInvoicedAndNothingIsWritten()
    {
        var person = await _profiles.CreateAsync(new CreateCustomerProfileRequest("Ayşe Yılmaz", null, null, null));
        var gone = await CompanyAsync("Silinecek Ltd.");
        await _database.SeedChargeAsync(person, await _database.SeedBillAsync((10m, 20m)), 20m, InAugust);
        await _database.SeedChargeAsync(gone, await _database.SeedBillAsync((10m, 30m)), 30m, InAugust);
        var period = await _selection.ClosePeriodAsync(August, September, _operator);
        var personSet = (await _selection.SelectAsync(period.PeriodId, person)).SourceSet.SourceSetId;
        var goneSet = (await _selection.SelectAsync(period.PeriodId, gone)).SourceSet.SourceSetId;
        await _profiles.AnonymizeAsync(gone, expectedRowVersion: 1);

        var missing = await Assert.ThrowsAsync<InvoiceBuyerIncompleteException>(
            () => _generation.GenerateAsync(personSet, InvoiceProfile.EArsiv, _operator));
        var anonymized = await Assert.ThrowsAsync<InvoiceBuyerIncompleteException>(
            () => _generation.GenerateAsync(goneSet, InvoiceProfile.EFatura, _operator));

        Assert.Equal(InvoiceBuyerProblem.TaxIdentityMissing, missing.Problem);
        Assert.Equal(InvoiceBuyerProblem.CustomerAnonymized, anonymized.Problem);
        Assert.Equal(0, await _database.CountAsync("invoicing.invoices"));
    }

    [Fact]
    public async Task AnUnknownACancelledOrAPaymentOnlySetIsRefused()
    {
        var customer = await CompanyAsync();
        var payer = await CompanyAsync("Sadece Ödeme Ltd.");
        await _database.SeedChargeAsync(customer, await _database.SeedBillAsync((10m, 20m)), 20m, InAugust);
        await _database.RecordAsync(payer, "Payment", 15m, InAugust);
        var period = await _selection.ClosePeriodAsync(August, September, _operator);
        var cancelled = (await _selection.SelectAsync(period.PeriodId, customer)).SourceSet.SourceSetId;
        await _selection.CancelAsync(cancelled, _operator, "yanlış dönem");
        var paymentsOnly = (await _selection.SelectAsync(period.PeriodId, payer)).SourceSet.SourceSetId;

        await Assert.ThrowsAsync<InvoiceGenerationSourceSetNotFoundException>(
            () => _generation.GenerateAsync(Guid.NewGuid(), InvoiceProfile.EFatura, _operator));
        await Assert.ThrowsAsync<InvoiceGenerationSourceSetCancelledException>(
            () => _generation.GenerateAsync(cancelled, InvoiceProfile.EFatura, _operator));
        await Assert.ThrowsAsync<InvoiceGenerationNothingToInvoiceException>(
            () => _generation.GenerateAsync(paymentsOnly, InvoiceProfile.EFatura, _operator));
        Assert.Equal(0, await _database.CountAsync("invoicing.invoices"));
    }

    [Fact]
    public async Task AChargeWhoseBillCannotBeFoundStopsTheWholeInvoice()
    {
        var customer = await CompanyAsync();
        await _database.SeedChargeAsync(customer, await _database.SeedBillAsync((10m, 20m)), 20m, InAugust);
        var orphan = await _database.RecordAsync(customer, "Charge", 35m, InAugust, referenceType: "Manual");
        var setId = await SelectAugustAsync(customer);

        var exception = await Assert.ThrowsAsync<InvoiceChargeSourceUnresolvedException>(
            () => _generation.GenerateAsync(setId, InvoiceProfile.EFatura, _operator));

        Assert.Equal(orphan, exception.TransactionId);
        Assert.Equal(0, await _database.CountAsync("invoicing.invoices"));
    }

    [Fact]
    public async Task TheDraftsLinesAndContentCannotBeChangedOrDeleted()
    {
        var customer = await CompanyAsync();
        await _database.SeedChargeAsync(customer, await _database.SeedBillAsync((10m, 20m)), 20m, InAugust);
        var invoiceId = (await _generation.GenerateAsync(await SelectAugustAsync(customer), InvoiceProfile.EFatura, _operator))
            .Invoice.InvoiceId;

        foreach (var sql in new[]
                 {
                     "UPDATE invoicing.invoice_lines SET gross_amount = 1, net_amount = 1, tax_amount = 0 WHERE invoice_id = @id;",
                     "DELETE FROM invoicing.invoice_lines WHERE invoice_id = @id;",
                     "UPDATE invoicing.invoices SET payable_amount = 1, line_extension_amount = 1, tax_total = 0 WHERE invoice_id = @id;",
                     "UPDATE invoicing.invoices SET buyer_envelope = '\\x00'::bytea WHERE invoice_id = @id;",
                     "DELETE FROM invoicing.invoices WHERE invoice_id = @id;",
                 })
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(() => _database.ExecuteAsync(sql, ("id", invoiceId)));
            Assert.Equal(PostgresErrorCodes.IntegrityConstraintViolation, exception.SqlState);
        }

        // The status column stays writable for the tasks that number, send or cancel the draft.
        Assert.Equal(1, await _database.ExecuteAsync(
            "UPDATE invoicing.invoices SET status = 'Draft' WHERE invoice_id = @id;", ("id", invoiceId)));
        Assert.Equal(20m, (await _generation.GetAsync(invoiceId))!.PayableAmount);
    }

    [Fact]
    public async Task TheIssueDateIsTodayInIstanbulAndTheInvoiceIsFoundByItsSourceSet()
    {
        var customer = await CompanyAsync();
        await _database.SeedChargeAsync(customer, await _database.SeedBillAsync((10m, 20m)), 20m, InAugust);
        var setId = await SelectAugustAsync(customer);

        var invoice = (await _generation.GenerateAsync(setId, InvoiceProfile.EFatura, _operator)).Invoice;

        var istanbulToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
            DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")).DateTime);
        Assert.InRange(invoice.IssueDate, istanbulToday.AddDays(-1), istanbulToday);
        Assert.Equal(invoice.InvoiceId, (await _generation.GetBySourceSetAsync(setId))!.InvoiceId);
        Assert.Null(await _generation.GetBySourceSetAsync(Guid.NewGuid()));
        Assert.Null(await _generation.GetAsync(Guid.NewGuid()));
    }
}
