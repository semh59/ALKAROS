using ALKAROS.Reporting.ProductMargin.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Reporting.ProductMargin.Tests;

/// <summary>
/// Every test sits on its own service day, so one shared database can hold all of them without one test's lines showing in another's range.
/// Istanbul is UTC+3 all year, so noon UTC is 15:00 local on the same date.
/// </summary>
public sealed class ProductMarginReportTests : IClassFixture<ProductMarginTestDatabase>
{
    private readonly ProductMarginTestDatabase _database;
    private readonly PostgresProductMarginReportService _service;

    public ProductMarginReportTests(ProductMarginTestDatabase database)
    {
        _database = database;
        _service = new PostgresProductMarginReportService(database.DataSource);
    }

    private static DateTimeOffset Noon(int day) => new(2026, 3, day, 12, 0, 0, TimeSpan.Zero);

    private static ProductMarginFilter Day(int day) => new(new DateOnly(2026, 3, day), new DateOnly(2026, 3, day));

    [Fact]
    public async Task RevenueIsTheSaleLinesNetAmountAndCostIsTheConsumedStockAtTheAverageReceiptPriceOfThatDay()
    {
        var product = await _database.SeedProductAsync("Köfte");
        var meat = await _database.SeedStockItemAsync("Dana kıyma");
        await _database.SeedReceiptAsync(meat, 10m, 4m, Noon(1));
        await _database.SeedReceiptAsync(meat, 10m, 6m, Noon(5));
        var line = await _database.SeedBillLineAsync(product, "Köfte", 2m, 90.91m, Noon(5));
        await _database.SeedConsumptionAsync(line, meat, 2m);

        var report = await _service.GetReportAsync(Day(5));

        var row = report.Rows.Should().ContainSingle().Subject;
        row.SoldQuantity.Should().Be(2m);
        row.NetRevenue.Should().Be(90.91m);
        row.Cost.Should().Be(10m);
        row.GrossMargin.Should().Be(80.91m);
        row.MarginPercent.Should().Be(89.00m);
        row.UnknownCostLines.Should().Be(0);
    }

    [Fact]
    public async Task AReceiptAfterTheSaleDayDoesNotChangeThatDaysCost()
    {
        var product = await _database.SeedProductAsync("Pilav");
        var rice = await _database.SeedStockItemAsync("Pirinç");
        await _database.SeedReceiptAsync(rice, 10m, 4m, Noon(1));
        await _database.SeedReceiptAsync(rice, 10m, 6m, Noon(8));
        var line = await _database.SeedBillLineAsync(product, "Pilav", 1m, 50m, Noon(3));
        await _database.SeedConsumptionAsync(line, rice, 1m);

        var report = await _service.GetReportAsync(Day(3));

        report.Rows.Single().Cost.Should().Be(4m);
    }

    [Fact]
    public async Task AnExtraAndTheProductMappingEachCountOnceInTheCost()
    {
        var product = await _database.SeedProductAsync("Burger");
        var meat = await _database.SeedStockItemAsync("Burger köftesi");
        var cheese = await _database.SeedStockItemAsync("Peynir");
        await _database.SeedReceiptAsync(meat, 10m, 10m, Noon(1));
        await _database.SeedReceiptAsync(cheese, 10m, 20m, Noon(1));
        var line = await _database.SeedBillLineAsync(product, "Burger", 1m, 150m, Noon(10));
        await _database.SeedConsumptionAsync(line, meat, 1m);
        await _database.SeedConsumptionAsync(line, cheese, 0.5m);

        var report = await _service.GetReportAsync(Day(10));

        report.Rows.Single().Cost.Should().Be(20m);
    }

    [Fact]
    public async Task AReversedConsumptionIsTakenOutOfTheCost()
    {
        var product = await _database.SeedProductAsync("Tavuk");
        var chicken = await _database.SeedStockItemAsync("Tavuk göğsü");
        await _database.SeedReceiptAsync(chicken, 10m, 10m, Noon(1));
        var line = await _database.SeedBillLineAsync(product, "Tavuk", 1m, 100m, Noon(11));
        var consumption = await _database.SeedConsumptionAsync(line, chicken, 2m);
        await _database.SeedReversalAsync(consumption, chicken, 1m);

        var report = await _service.GetReportAsync(Day(11));

        report.Rows.Single().Cost.Should().Be(10m);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AStockItemWithoutAReceiptHistoryIsFlaggedAndNeverPricedAtZero(bool consumptionMissingEntirely)
    {
        var product = await _database.SeedProductAsync(consumptionMissingEntirely ? "Çay" : "Ayran");
        var drink = await _database.SeedStockItemAsync("Şişe");
        var line = await _database.SeedBillLineAsync(product, "Ürün", 3m, 60m, Noon(consumptionMissingEntirely ? 13 : 12));
        if (!consumptionMissingEntirely)
            await _database.SeedConsumptionAsync(line, drink, 3m);

        var report = await _service.GetReportAsync(Day(consumptionMissingEntirely ? 13 : 12));

        var row = report.Rows.Single();
        row.UnknownCostLines.Should().Be(1);
        row.GrossMargin.Should().BeNull();
        row.MarginPercent.Should().BeNull();
        row.NetRevenue.Should().Be(60m);
        report.UnknownCostLines.Should().Be(1);
    }

    [Fact]
    public async Task ComplimentaryUnitsCarryCostButNoRevenue()
    {
        var product = await _database.SeedProductAsync("Tatlı");
        var cream = await _database.SeedStockItemAsync("Krema");
        await _database.SeedReceiptAsync(cream, 10m, 8m, Noon(1));
        var sold = await _database.SeedBillLineAsync(product, "Tatlı", 1m, 40m, Noon(14));
        var given = await _database.SeedBillLineAsync(product, "Tatlı", 2m, 0m, Noon(14), lineType: "Complimentary");
        await _database.SeedConsumptionAsync(sold, cream, 1m);
        await _database.SeedConsumptionAsync(given, cream, 2m);

        var report = await _service.GetReportAsync(Day(14));

        var row = report.Rows.Single();
        row.SoldQuantity.Should().Be(1m);
        row.GivenAwayQuantity.Should().Be(2m);
        row.NetRevenue.Should().Be(40m);
        row.Cost.Should().Be(24m);
        row.GrossMargin.Should().Be(16m);
    }

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("Open")]
    [InlineData("Reopened")]
    public async Task OnlyPaidBillsCount(string status)
    {
        var product = await _database.SeedProductAsync("Salata " + status);
        await _database.SeedBillLineAsync(product, "Salata", 1m, 30m, Noon(15), billStatus: status);

        var report = await _service.GetReportAsync(Day(15));

        report.Rows.Should().NotContain(r => r.ProductId == product);
    }

    [Fact]
    public async Task TheServiceDayIsTheIstanbulCalendarDay()
    {
        var product = await _database.SeedProductAsync("Gece");
        // 2026-03-17 23:59:59 local is 20:59:59 UTC; one second later it is 18 March locally.
        await _database.SeedBillLineAsync(product, "Gece", 1m, 10m, new DateTimeOffset(2026, 3, 17, 20, 59, 59, TimeSpan.Zero));
        await _database.SeedBillLineAsync(product, "Gece", 1m, 20m, new DateTimeOffset(2026, 3, 17, 21, 0, 0, TimeSpan.Zero));

        var seventeenth = await _service.GetReportAsync(Day(17));
        var eighteenth = await _service.GetReportAsync(Day(18));

        seventeenth.Rows.Single().NetRevenue.Should().Be(10m);
        eighteenth.Rows.Single().NetRevenue.Should().Be(20m);
    }

    [Fact]
    public async Task TheCheckBalancesAndShowsTheBillLevelDiscountsSeparately()
    {
        var first = await _database.SeedProductAsync("Birinci");
        var second = await _database.SeedProductAsync("İkinci");
        await _database.SeedBillLineAsync(first, "Birinci", 1m, 50m, Noon(19), billDiscount: 5m);
        await _database.SeedBillLineAsync(second, "İkinci", 1m, 70m, Noon(19), billDiscount: 7m);

        var report = await _service.GetReportAsync(Day(19));

        report.Rows.Should().HaveCount(2);
        report.TotalNetRevenue.Should().Be(120m);
        report.Check.ProductNetTotal.Should().Be(120m);
        report.Check.LinesNetTotal.Should().Be(120m);
        report.Check.BillLevelDiscounts.Should().Be(12m);
        report.Check.IsBalanced.Should().BeTrue();
        report.ReportVersion.Should().Be("product-margin.v1");
    }

    [Fact]
    public void ARangeLongerThanThirtyOneDaysOrBackwardsIsRefused()
    {
        var tooLong = () => new ProductMarginFilter(new DateOnly(2026, 3, 1), new DateOnly(2026, 4, 2)).Validate();
        var backwards = () => new ProductMarginFilter(new DateOnly(2026, 3, 5), new DateOnly(2026, 3, 4)).Validate();

        tooLong.Should().Throw<ArgumentException>();
        backwards.Should().Throw<ArgumentException>();
    }
}
