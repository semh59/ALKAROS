using System.Security.Cryptography;
using ALKAROS.CustomerData.Profiles;
using ALKAROS.Invoicing.Generation;
using ALKAROS.Invoicing.Generation.Tests.Fixtures;
using ALKAROS.Invoicing.SourceSelection;
using ALKAROS.Secrets;
using Npgsql;
using Xunit;

namespace ALKAROS.Invoicing.SourceTraceability.Tests;

/// <summary>
/// On a real PostgreSQL with every migration: each invoice line reaches the charges it was built from,
/// every live charge is represented in full, and orphan, duplicate, partial and edited links are refused by the
/// database itself.
/// </summary>
public sealed class InvoiceTraceabilityTests : IAsyncLifetime
{
    private static readonly DateOnly August = new(2026, 8, 1);
    private static readonly DateOnly September = new(2026, 9, 1);
    private static readonly DateTimeOffset InAugust = new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly InvoiceGenerationTestDatabase _database = new();
    private readonly Guid _operator = Guid.NewGuid();
    private PostgresCustomerProfileStore _profiles = null!;
    private PostgresInvoiceSourceSelectionService _selection = null!;
    private PostgresInvoiceGenerationService _generation = null!;
    private PostgresInvoiceTraceabilityService _traceability = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        var secrets = new InMemorySecretProvider();
        secrets.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        _profiles = new PostgresCustomerProfileStore(_database.DataSource, secrets);
        _selection = new PostgresInvoiceSourceSelectionService(_database.DataSource);
        _generation = new PostgresInvoiceGenerationService(_database.DataSource, _profiles, secrets);
        _traceability = new PostgresInvoiceTraceabilityService(_database.DataSource);
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    private sealed record Scenario(Guid InvoiceId, Guid SourceSetId, Guid MixedCharge, Guid DrinksCharge, Guid MixedBill);

    // A 100 charge on a 10%/20% bill (52.17 at 20%, 47.83 at 10%) and a 60 charge on a 20% bill: line 1 (20%) draws on
    // both charges, line 2 (10%) on the first only.
    private async Task<Scenario> InvoiceAsync(bool withPayment = false)
    {
        var customer = await _profiles.CreateAsync(new CreateCustomerProfileRequest(
            "Deniz Gıda Ltd.", "05551234567", "muhasebe@deniz.example", "Moda Cad. 1, Kadıköy",
            CustomerTaxIdentity.Create(CustomerTaxIdKind.Vkn, "1234567890", "Kadıköy")));
        var mixedBill = await _database.SeedBillAsync((10m, 110m), (20m, 120m));
        var drinksBill = await _database.SeedBillAsync((20m, 60m));
        var mixed = await _database.SeedChargeAsync(customer, mixedBill, 100m, InAugust);
        var drinks = await _database.SeedChargeAsync(customer, drinksBill, 60m, InAugust.AddDays(1));
        if (withPayment)
            await _database.RecordAsync(customer, "Payment", 80m, InAugust.AddDays(2));
        var period = await _selection.ClosePeriodAsync(August, September, _operator);
        var setId = (await _selection.SelectAsync(period.PeriodId, customer)).SourceSet.SourceSetId;
        var invoice = (await _generation.GenerateAsync(setId, InvoiceProfile.EFatura, _operator)).Invoice;
        return new Scenario(invoice.InvoiceId, setId, mixed, drinks, mixedBill);
    }

    private Task<int> InsertLinkAsync(Guid invoiceId, int line, Guid setId, Guid transactionId, decimal amount)
        => _database.ExecuteAsync(
            """
            INSERT INTO invoicing.invoice_line_sources
                (invoice_id, line_number, source_set_id, transaction_id, allocated_amount, recorded_by)
            VALUES (@invoice_id, @line, @set, @transaction, @amount, @by);
            """,
            ("invoice_id", invoiceId), ("line", line), ("set", setId), ("transaction", transactionId),
            ("amount", amount), ("by", _operator));

    private static IEnumerable<(int Line, Guid Transaction, decimal Amount)> Flatten(InvoiceTrace trace)
        => trace.Lines.SelectMany(line => line.Sources.Select(source => (line.LineNumber, source.TransactionId, source.AllocatedAmount)));

    private static async Task<string?> SqlStateOfAsync(Func<Task> action)
        => (await Assert.ThrowsAsync<PostgresException>(action)).SqlState;

    [Fact]
    public async Task EveryLineReachesTheChargesItWasBuiltFromAndEveryChargeIsRepresentedInFull()
    {
        var s = await InvoiceAsync(withPayment: true);

        var result = await _traceability.RecordAsync(s.InvoiceId, _operator);

        Assert.False(result.WasAlreadyRecorded);
        Assert.Equal(s.SourceSetId, result.Trace.SourceSetId);
        Assert.Collection(
            result.Trace.Lines,
            line =>
            {
                Assert.Equal(1, line.LineNumber);
                Assert.Equal(
                    [new InvoiceLineSource(s.DrinksCharge, 60m), new InvoiceLineSource(s.MixedCharge, 52.17m)],
                    line.Sources);
            },
            line =>
            {
                Assert.Equal(2, line.LineNumber);
                Assert.Equal([new InvoiceLineSource(s.MixedCharge, 47.83m)], line.Sources);
            });

        var invoice = (await _generation.GetAsync(s.InvoiceId))!;
        foreach (var line in invoice.Lines)
            Assert.Equal(line.GrossAmount, result.Trace.Lines.Single(l => l.LineNumber == line.LineNumber).Sources.Sum(x => x.AllocatedAmount));
        var perCharge = result.Trace.Lines.SelectMany(l => l.Sources).GroupBy(x => x.TransactionId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.AllocatedAmount));
        Assert.Equal(new Dictionary<Guid, decimal> { [s.MixedCharge] = 100m, [s.DrinksCharge] = 60m }, perCharge);
    }

    [Fact]
    public async Task AnInvoiceHasNoTraceUntilItIsRecordedAndARetryReturnsTheSameOne()
    {
        var s = await InvoiceAsync();
        Assert.Null(await _traceability.GetAsync(s.InvoiceId));

        var first = await _traceability.RecordAsync(s.InvoiceId, _operator);
        var again = await _traceability.RecordAsync(s.InvoiceId, Guid.NewGuid());

        Assert.True(again.WasAlreadyRecorded);
        Assert.Equal(Flatten(first.Trace), Flatten(again.Trace));
        Assert.Equal(3, await _database.CountAsync("invoicing.invoice_line_sources"));
        Assert.Equal(Flatten(first.Trace), Flatten((await _traceability.GetAsync(s.InvoiceId))!));
    }

    [Fact]
    public async Task ConcurrentRecordingGivesOneSetOfLinks()
    {
        var s = await InvoiceAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => _traceability.RecordAsync(s.InvoiceId, _operator)));

        Assert.Equal(1, results.Count(result => !result.WasAlreadyRecorded));
        Assert.Equal(3, await _database.CountAsync("invoicing.invoice_line_sources"));
    }

    [Fact]
    public async Task ARefusedRecordingLeavesNothingBehind()
    {
        var s = await InvoiceAsync();
        await _database.ExecuteAsync("UPDATE billing.bill_items SET tax_rate = 8 WHERE bill_id = @bill", ("bill", s.MixedBill));

        var exception = await Assert.ThrowsAsync<InvoiceTraceMismatchException>(() => _traceability.RecordAsync(s.InvoiceId, _operator));

        Assert.Equal(s.InvoiceId, exception.InvoiceId);
        Assert.Equal(0, await _database.CountAsync("invoicing.invoice_line_sources"));
        await Assert.ThrowsAsync<InvoiceTraceInvoiceNotFoundException>(() => _traceability.RecordAsync(Guid.NewGuid(), _operator));
    }

    [Fact]
    public async Task ALinkToALineOrChargeThatIsNotThereIsRefused()
    {
        var s = await InvoiceAsync();
        await _traceability.RecordAsync(s.InvoiceId, _operator);
        var otherCustomerCharge = await _database.RecordAsync(Guid.NewGuid(), "Charge", 10m, InAugust);

        Assert.Equal("23503", await SqlStateOfAsync(() => InsertLinkAsync(s.InvoiceId, 9, s.SourceSetId, s.MixedCharge, 1m)));
        Assert.Equal("23503", await SqlStateOfAsync(() => InsertLinkAsync(s.InvoiceId, 1, s.SourceSetId, otherCustomerCharge, 1m)));
        Assert.Equal("23503", await SqlStateOfAsync(() => InsertLinkAsync(s.InvoiceId, 2, Guid.NewGuid(), s.DrinksCharge, 1m)));
        Assert.Equal(3, await _database.CountAsync("invoicing.invoice_line_sources"));
    }

    [Fact]
    public async Task ADuplicateLinkIsRefused()
    {
        var s = await InvoiceAsync();
        await _traceability.RecordAsync(s.InvoiceId, _operator);

        Assert.Equal("23505", await SqlStateOfAsync(() => InsertLinkAsync(s.InvoiceId, 2, s.SourceSetId, s.MixedCharge, 47.83m)));
    }

    [Fact]
    public async Task LinksThatDoNotCoverEveryLineAndChargeCannotBeCommitted()
    {
        var s = await InvoiceAsync();

        // One of three links: line 1 and line 2 are short and the drinks charge is missing.
        Assert.Equal("23514", await SqlStateOfAsync(() => InsertLinkAsync(s.InvoiceId, 2, s.SourceSetId, s.MixedCharge, 47.83m)));
        Assert.Equal(0, await _database.CountAsync("invoicing.invoice_line_sources"));

        // Amounts that add up per line but shift money between charges.
        Assert.Equal("23514", await SqlStateOfAsync(() => _database.ExecuteAsync(
            """
            INSERT INTO invoicing.invoice_line_sources
                (invoice_id, line_number, source_set_id, transaction_id, allocated_amount, recorded_by)
            VALUES (@invoice, 1, @set, @drinks, 112.17, @by), (@invoice, 2, @set, @mixed, 47.83, @by);
            """,
            ("invoice", s.InvoiceId), ("set", s.SourceSetId), ("drinks", s.DrinksCharge), ("mixed", s.MixedCharge), ("by", _operator))));
        Assert.Equal(0, await _database.CountAsync("invoicing.invoice_line_sources"));
    }

    [Fact]
    public async Task RecordedLinksCannotBeChangedOrRemoved()
    {
        var s = await InvoiceAsync();
        await _traceability.RecordAsync(s.InvoiceId, _operator);

        Assert.Equal("23000", await SqlStateOfAsync(() => _database.ExecuteAsync(
            "UPDATE invoicing.invoice_line_sources SET allocated_amount = allocated_amount + 1")));
        Assert.Equal("23000", await SqlStateOfAsync(() => _database.ExecuteAsync("DELETE FROM invoicing.invoice_line_sources")));
        Assert.Equal(3, await _database.CountAsync("invoicing.invoice_line_sources"));
    }
}
