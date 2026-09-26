using System.Text.Json;
using ALKAROS.Reporting.Channels.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Reporting.Channels.Tests;

/// <summary>
/// V12-RPT-001 golden dataset against real Postgres. The dataset sits on business-date boundaries
/// (Europe/Istanbul is UTC+3, so 21:30 UTC is already the next business date), carries every status bucket,
/// cancellations, a provider order refused by two webhooks, a refusal later reprocessed into an order,
/// reconciliation cases and retries. Every test uses its own dates so the shared database never mixes them.
/// </summary>
public sealed class ChannelReportTests : IClassFixture<ChannelReportTestDatabase>
{
    private static readonly DateOnly Day1 = new(2031, 3, 10);
    private static readonly DateOnly Day2 = new(2031, 3, 11);

    private readonly ChannelReportTestDatabase _database;
    private readonly PostgresChannelReportService _reports;

    public ChannelReportTests(ChannelReportTestDatabase database)
    {
        _database = database;
        _reports = new PostgresChannelReportService(database.DataSource);
    }

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute) => new(year, month, day, hour, minute, 0, TimeSpan.Zero);

    private async Task SeedGoldenDatasetAsync()
    {
        // Business date 2031-03-10.
        await _database.SeedOrderAsync("Qr", "Accepted", 100m, Utc(2031, 3, 10, 9, 0));
        await _database.SeedOrderAsync("Qr", "PendingConfirmation", 40m, Utc(2031, 3, 10, 9, 5));
        await _database.SeedOrderAsync("Qr", "Rejected", 30m, Utc(2031, 3, 10, 9, 10));
        await _database.SeedOrderAsync("Online", "Served", 250.50m, Utc(2031, 3, 10, 10, 0), "ys-a1", discount: 10m);
        await _database.SeedOrderAsync("Online", "Cancelled", 80m, Utc(2031, 3, 10, 11, 0), "ys-a2");
        await _database.SeedOrderAsync("Online", "Accepted", 70m, Utc(2031, 3, 9, 21, 10), "ys-a3"); // 00:10 local on the 10th
        await _database.SeedOrderAsync("Cashier", "Accepted", 999m, Utc(2031, 3, 10, 12, 0)); // not a channel
        // Business date 2031-03-11.
        await _database.SeedOrderAsync("Online", "Completed", 60m, Utc(2031, 3, 10, 21, 30), "ys-b1"); // 00:30 local on the 11th
        await _database.SeedOrderAsync("Online", "Accepted", 45m, Utc(2031, 3, 11, 12, 0), "ys-reprocessed");
        // Business date 2031-03-12: outside the range.
        await _database.SeedOrderAsync("Online", "Accepted", 500m, Utc(2031, 3, 11, 21, 30), "ys-c1");

        // A provider order refused twice (two webhooks) is one refusal; one reprocessed into an order is none.
        await _database.SeedInboxAsync("ys-refused-twice", "Rejected", Utc(2031, 3, 10, 13, 0));
        await _database.SeedInboxAsync("ys-refused-twice", "Rejected", Utc(2031, 3, 10, 13, 1));
        await _database.SeedInboxAsync("ys-reprocessed", "Rejected", Utc(2031, 3, 10, 14, 0));
        await _database.SeedInboxAsync("ys-no-stock", "Diverged", Utc(2031, 3, 11, 8, 0));

        var unknown = await _database.SeedCaseAsync("LocallyAcceptedProviderUnknown", "Open", 250.50m, Utc(2031, 3, 10, 15, 0));
        await _database.SeedCaseAsync("ProviderAcceptedLocallyRefused", "Open", 0m, Utc(2031, 3, 10, 15, 1));
        await _database.SeedCaseAsync("ProviderAcceptedLocallyRefused", "Resolved", 0m, Utc(2031, 3, 10, 15, 2));
        await _database.SeedRetryAttemptAsync(unknown, Utc(2031, 3, 10, 16, 0));
        await _database.SeedRetryAttemptAsync(unknown, Utc(2031, 3, 11, 16, 0));
    }

    [Fact]
    public async Task TheGoldenDatasetProducesExactlyTheExpectedChannelReportAndBalancesToTheLedger()
    {
        await SeedGoldenDatasetAsync();

        var report = await _reports.GetReportAsync(new ChannelReportFilter(Day1, Day2));

        report.ReportVersion.Should().Be("channel-report.v1");
        report.Days.Should().Equal(
            new ChannelDayRow(Day1, "Online", 3, 0, 2, 0, 1, 320.50m, 267.08m, 53.42m, 10m, 80m, 1),
            new ChannelDayRow(Day1, "Qr", 3, 1, 1, 1, 0, 100m, 83.33m, 16.67m, 0m, 0m, 0),
            new ChannelDayRow(Day2, "Online", 2, 0, 2, 0, 0, 105m, 87.50m, 17.50m, 0m, 0m, 1));
        report.Reconciliation.Should().Equal(
            new ChannelReconciliationRow(Day1, "LocallyAcceptedProviderUnknown", 1, 0, 250.50m),
            new ChannelReconciliationRow(Day1, "ProviderAcceptedLocallyRefused", 1, 1, 0m));
        report.RetryAttempts.Should().Be(2);
        report.Check.Should().Be(new ChannelReportCheck(8, 525.50m, 437.91m, 8, 525.50m, 437.91m));
        report.Check.IsBalanced.Should().BeTrue();

        var again = await _reports.GetReportAsync(new ChannelReportFilter(Day1, Day2));
        JsonSerializer.Serialize(again).Should().Be(JsonSerializer.Serialize(report));
    }

    [Fact]
    public async Task ASourceFilterKeepsOnlyThatChannelAndItsOwnLedgerTotal()
    {
        var day = new DateOnly(2032, 6, 1);
        await _database.SeedOrderAsync("Qr", "Served", 20m, Utc(2032, 6, 1, 10, 0));
        await _database.SeedOrderAsync("Online", "Served", 35m, Utc(2032, 6, 1, 10, 0), "ys-filter");
        await _database.SeedInboxAsync("ys-filter-refused", "Rejected", Utc(2032, 6, 1, 11, 0));
        var caseId = await _database.SeedCaseAsync("ProviderEventFailed", "Open", 0m, Utc(2032, 6, 1, 12, 0));
        await _database.SeedRetryAttemptAsync(caseId, Utc(2032, 6, 1, 12, 5));

        var qr = await _reports.GetReportAsync(new ChannelReportFilter(day, day, "Qr"));
        qr.Days.Should().Equal(new ChannelDayRow(day, "Qr", 1, 0, 1, 0, 0, 20m, 16.67m, 3.33m, 0m, 0m, 0));
        qr.Reconciliation.Should().BeEmpty();
        qr.RetryAttempts.Should().Be(0);
        qr.Check.Should().Be(new ChannelReportCheck(1, 20m, 16.67m, 1, 20m, 16.67m));

        var online = await _reports.GetReportAsync(new ChannelReportFilter(day, day, "Online"));
        online.Days.Should().Equal(new ChannelDayRow(day, "Online", 1, 0, 1, 0, 0, 35m, 29.17m, 5.83m, 0m, 0m, 1));
        online.Reconciliation.Should().ContainSingle().Which.Kind.Should().Be("ProviderEventFailed");
        online.RetryAttempts.Should().Be(1);
    }

    [Fact]
    public async Task ADayWithOnlyProviderRefusalsStillAppears()
    {
        var day = new DateOnly(2033, 1, 5);
        await _database.SeedInboxAsync("ys-only-refused", "Diverged", Utc(2033, 1, 5, 9, 0));

        var report = await _reports.GetReportAsync(new ChannelReportFilter(day, day));

        report.Days.Should().Equal(new ChannelDayRow(day, "Online", 0, 0, 0, 0, 0, 0m, 0m, 0m, 0m, 0m, 1));
        report.Check.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public async Task AnInvalidRangeOrSourceIsRefused()
    {
        var reversed = () => _reports.GetReportAsync(new ChannelReportFilter(Day2, Day1));
        var tooLong = () => _reports.GetReportAsync(new ChannelReportFilter(Day1, Day1.AddDays(31)));
        var cashier = () => _reports.GetReportAsync(new ChannelReportFilter(Day1, Day1, "Cashier"));

        await reversed.Should().ThrowAsync<ArgumentException>();
        await tooLong.Should().ThrowAsync<ArgumentException>();
        await cashier.Should().ThrowAsync<ArgumentException>();
        (await _reports.GetReportAsync(new ChannelReportFilter(Day1, Day1.AddDays(30)))).Should().NotBeNull();
    }
}
