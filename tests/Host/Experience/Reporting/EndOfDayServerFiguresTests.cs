using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ALKAROS.Reporting.BusinessDayTotals;
using ALKAROS.Reporting.V1Operations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Reporting.Tests;

/// <summary>
/// The business-day close counts everything itself: cancellations from the item-void audit events, print failures from
/// the failed print jobs, the waiter, station, table and cash breakdowns, the tips as a line apart from revenue, and the
/// open items as a warning the manager acknowledges. Real Postgres; the close and the preflight go through HTTP.
/// </summary>
[Collection("EOD business day PostgreSQL HTTP")]
public sealed class EndOfDayServerFiguresTests : IAsyncLifetime
{
    private static readonly DateOnly Day = new(2026, 2, 3);
    private static readonly DateTimeOffset Start = new(2026, 2, 3, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddDays(1);
    private const string Base = "/api/v1/management/reporting/business-day";

    private readonly EndOfDayTestDatabase _database = new();
    private WebApplication? _application;
    private Uri? _baseAddress;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddEndOfDayExperience();
        _application = builder.Build();
        _application.MapEndOfDayApi();
        await _application.StartAsync();
        _baseAddress = new Uri(
            Assert.Single(_application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses),
            UriKind.Absolute);
    }

    public async Task DisposeAsync()
    {
        if (_application is not null)
            await _application.DisposeAsync();
        await _database.DisposeAsync();
    }

    private HttpClient Manager()
    {
        var client = new HttpClient { BaseAddress = _baseAddress };
        client.DefaultRequestHeaders.Add("Cookie", $"{EndOfDayEndpoints.ManagerCookieName}={EndOfDayTestDatabase.ManagerToken}");
        return client;
    }

    private async Task<HttpClient> OpenedDayAsync()
    {
        var client = Manager();
        using var opened = await client.PostAsJsonAsync($"{Base}/open", new OpenBusinessDayV1(Day));
        Assert.Equal(HttpStatusCode.Created, opened.StatusCode);
        return client;
    }

    private static Task<HttpResponseMessage> CloseAsync(HttpClient client, bool acknowledge = false)
        => client.PostAsJsonAsync($"{Base}/{Day:yyyy-MM-dd}/close", new CloseBusinessDayV1(acknowledge));

    private static async Task<BusinessDayReportV1> ReportAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BusinessDayReportV1>())!;
    }

    [Fact]
    public async Task CancelledItemsAreTheItemVoidEventsOfTheWindowAndNothingTheCallerSends()
    {
        var order = await _database.SeedOrderAsync(Start.AddHours(2), "Completed");
        await _database.SeedAuditAsync("Order.ItemVoided", order, Start.AddHours(3));
        await _database.SeedAuditAsync("Order.SentItemVoided", order, Start.AddHours(4));
        await _database.SeedAuditAsync("Order.SentItemVoided", order, End.AddMinutes(-1));
        await _database.SeedAuditAsync("Order.ItemVoided", order, Start.AddMinutes(-1));
        await _database.SeedAuditAsync("Order.ItemVoided", order, End);
        await _database.SeedAuditAsync("Order.Submitted", order, Start.AddHours(5));
        using var client = await OpenedDayAsync();

        var report = await ReportAsync(await CloseAsync(client));

        Assert.Equal(3, report.BusinessDay.TotalCancelledItemsCount);
    }

    [Fact]
    public async Task FiguresASenderStillPutInTheCloseRequestAreIgnored()
    {
        var order = await _database.SeedOrderAsync(Start.AddHours(2), "Completed");
        await _database.SeedAuditAsync("Order.ItemVoided", order, Start.AddHours(3));
        using var client = await OpenedDayAsync();

        using var closed = await client.PostAsJsonAsync(
            $"{Base}/{Day:yyyy-MM-dd}/close", new { CancelledItems = 99, PrintFailures = 99, TotalRevenue = 12345m });

        var day = (await ReportAsync(closed)).BusinessDay;
        Assert.Equal((1, 0, 0m), (day.TotalCancelledItemsCount, day.TotalPrintFailuresCount, day.TotalRevenue));
    }

    [Fact]
    public async Task PrintFailuresAreTheFailedJobsOfTheWindowEvenWhenPrintedLaterAndAreSummarisedPerStation()
    {
        await _database.SeedPrintJobAsync("Mutfak", Start.AddHours(1), Start.AddHours(1), "Failed");
        await _database.SeedPrintJobAsync("Mutfak", Start.AddHours(2), Start.AddHours(2), "Printed");
        await _database.SeedPrintJobAsync("Mutfak", Start.AddHours(3), null, "Printed");
        await _database.SeedPrintJobAsync("Bar", Start.AddHours(4), Start.AddHours(4), "DeadLetter");
        await _database.SeedPrintJobAsync("Bar", Start.AddDays(-2), Start.AddMinutes(-5), "Failed");   // failed before the window
        await _database.SeedPrintJobAsync("Bar", Start.AddHours(6), End.AddMinutes(5), "Failed");       // failed after the window
        await _database.SeedPrintJobAsync("Pasta", Start.AddDays(-1), Start.AddHours(7), "Pending");     // created before, failed inside, waiting for a retry
        using var client = await OpenedDayAsync();

        var report = await ReportAsync(await CloseAsync(client));

        Assert.Equal(4, report.BusinessDay.TotalPrintFailuresCount);
        var retried = Assert.Single(report.PrintSummaries, p => p.StationName == "Pasta");
        Assert.Equal((0, 1, 0), (retried.TotalPrintJobs, retried.FailedPrintJobs, retried.RecoveredPrintJobs));
        var kitchen = Assert.Single(report.PrintSummaries, p => p.StationName == "Mutfak");
        Assert.Equal((3, 2, 1), (kitchen.TotalPrintJobs, kitchen.FailedPrintJobs, kitchen.RecoveredPrintJobs));
        var bar = Assert.Single(report.PrintSummaries, p => p.StationName == "Bar");
        Assert.Equal((2, 1, 0), (bar.TotalPrintJobs, bar.FailedPrintJobs, bar.RecoveredPrintJobs));
    }

    [Fact]
    public async Task TipsAreTheirOwnLineOutsideRevenueAndOnlyOfBillsClosedPaidInTheWindow()
    {
        await _database.SeedPaymentAsync(Start.AddHours(2), 1000m);
        var paid = await _database.SeedBillAsync("Paid", Start.AddHours(3));
        var alsoPaid = await _database.SeedBillAsync("Paid", End.AddMinutes(-1));
        var refundedAway = await _database.SeedBillAsync("Cancelled", Start.AddHours(4));
        var open = await _database.SeedBillAsync("Open");
        var yesterday = await _database.SeedBillAsync("Paid", Start.AddMinutes(-1));
        await _database.SeedTipAsync(paid, 50m);
        await _database.SeedTipAsync(paid, 25m);
        await _database.SeedTipAsync(alsoPaid, 30m);
        await _database.SeedTipAsync(refundedAway, 400m);
        await _database.SeedTipAsync(open, 500m);
        await _database.SeedTipAsync(yesterday, 600m);
        using var client = await OpenedDayAsync();

        var report = await ReportAsync(await CloseAsync(client, acknowledge: true));

        Assert.Equal(105m, report.BusinessDay.TotalTipsAmount);
        Assert.Equal(895m, report.BusinessDay.TotalRevenue);
        Assert.Equal(1, report.BusinessDay.OpenBillsCount);   // the open bill only: not the paid, the refunded-away (Cancelled) ones
    }

    [Fact]
    public async Task WaiterAndTableBreakdownsAreCountedFromTheOrdersAndVoidsOfTheWindow()
    {
        var waiterA = Guid.NewGuid();
        var waiterB = Guid.NewGuid();
        var table1 = Guid.NewGuid();
        var table2 = Guid.NewGuid();
        await _database.SeedTableAsync(table1, "A1");
        var a1 = await _database.SeedOrderAsync(Start.AddHours(1), "Completed", servingUserId: waiterA, tableId: table1, total: 100m, discount: 10m);
        await _database.SeedOrderAsync(Start.AddHours(2), "Completed", servingUserId: waiterA, tableId: table1, total: 50m, discount: 5m);
        await _database.SeedOrderAsync(Start.AddHours(3), "Completed", servingUserId: waiterB, tableId: table2, total: 70m);
        await _database.SeedOrderAsync(Start.AddHours(4), "Cancelled", servingUserId: waiterB, tableId: table2, total: 999m);
        await _database.SeedOrderAsync(End.AddMinutes(1), "Completed", servingUserId: waiterB, tableId: table2, total: 999m);
        await _database.SeedOrderAsync(Start.AddHours(5), "Draft", servingUserId: waiterB, tableId: table2, total: 500m);
        await _database.SeedOrderAsync(Start.AddHours(5), "Completed", "Online", servingUserId: waiterB, tableId: table2, total: 600m);
        await _database.SeedOrderAsync(Start.AddHours(6), "Completed", tableId: table1, total: 25m);   // nobody serving: counts for the table only
        var waiterC = Guid.NewGuid();
        var cancelledOfC = await _database.SeedOrderAsync(Start.AddHours(6), "Cancelled", servingUserId: waiterC, total: 80m);
        await _database.SeedAuditAsync("Order.ItemVoided", cancelledOfC, Start.AddHours(6));
        await _database.SeedAuditAsync("Order.ItemVoided", a1, Start.AddHours(5));
        using var client = await OpenedDayAsync();

        var report = await ReportAsync(await CloseAsync(client));

        var a = Assert.Single(report.WaiterSummaries, w => w.WaiterUserId == waiterA);
        Assert.Equal((2, 150m, 1, 15m), (a.OrdersServedCount, a.TotalSalesAmount, a.CancellationsCount, a.DiscountsAppliedAmount));
        var b = Assert.Single(report.WaiterSummaries, w => w.WaiterUserId == waiterB);
        Assert.Equal((1, 70m, 0, 0m), (b.OrdersServedCount, b.TotalSalesAmount, b.CancellationsCount, b.DiscountsAppliedAmount));
        var c = Assert.Single(report.WaiterSummaries, w => w.WaiterUserId == waiterC);
        Assert.Equal((0, 0m, 1), (c.OrdersServedCount, c.TotalSalesAmount, c.CancellationsCount));
        Assert.Equal(3, report.WaiterSummaries.Count);
        Assert.Equal(2, report.BusinessDay.TableBreakdown.Count);
        Assert.Contains(report.BusinessDay.TableBreakdown, t => t.TableId == table1 && t.TableNumber == "A1" && t.Orders == 3 && t.Sales == 175m);
        Assert.Contains(report.BusinessDay.TableBreakdown, t => t.TableId == table2 && t.TableNumber == null && t.Orders == 1 && t.Sales == 70m);
    }

    [Fact]
    public async Task CashReconciliationTotalsCoverTheSessionsClosedInTheWindow()
    {
        await _database.SeedCashSessionAsync("Closed", Start.AddHours(5), expected: 500m, actual: 490m, supervisorOverride: true);
        await _database.SeedCashSessionAsync("Reconciled", Start.AddHours(6), expected: 300m, actual: 305m, selfReconciled: true);
        await _database.SeedCashSessionAsync("Closed", End.AddMinutes(1), expected: 999m, actual: 0m);
        using var client = await OpenedDayAsync();

        var cash = (await ReportAsync(await CloseAsync(client))).BusinessDay.CashReconciliation!;

        Assert.Equal((2, 800m, 795m, -5m, 1, 1), (cash.SessionsClosed, cash.Expected, cash.Counted, cash.Difference, cash.ManagerApprovedCloses, cash.SelfReconciled));
    }

    [Fact]
    public async Task OpenItemsAreWarnedAboutInThePreflightAndAnUnacknowledgedCloseIsRefusedWithoutChangingTheDay()
    {
        await _database.SeedCashSessionAsync("Counting", null, 0m, 0m);
        await _database.SeedBillAsync("Open");
        await _database.SeedBillAsync("Reopened");
        await _database.SeedPaymentAsync(Start.AddHours(1), 10m, "Pending");
        using var client = await OpenedDayAsync();

        var preflight = (await client.GetFromJsonAsync<BusinessDayPreflightV1>($"{Base}/{Day:yyyy-MM-dd}/close-preflight"))!;
        using var refused = await CloseAsync(client);

        Assert.Equal((1, 2, 1, true), (preflight.OpenCashSessions, preflight.OpenBills, preflight.UnsettledPayments, preflight.RequiresAcknowledgement));
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        var error = (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");
        Assert.Equal("BUSINESS_DAY_OPEN_ITEMS_NOT_ACKNOWLEDGED", error.GetProperty("code").GetString());
        var day = (await client.GetFromJsonAsync<BusinessDayV1>($"{Base}/{Day:yyyy-MM-dd}"))!;
        Assert.Equal("Open", day.Status);
        Assert.Equal(0L, await _database.CountAsync("SELECT count(*) FROM audit.audit_events WHERE event_name = 'reporting.business_day_closed'"));
    }

    [Fact]
    public async Task AnAcknowledgedCloseStoresTheOpenItemCountsAndWritesTheAuditEventInTheSameTransaction()
    {
        await _database.SeedCashSessionAsync("Open", null, 0m, 0m);
        await _database.SeedBillAsync("Open");
        await _database.SeedBillAsync("Open");
        await _database.SeedPaymentAsync(Start.AddHours(1), 10m, "Unknown");
        await _database.SeedPaymentAsync(Start.AddHours(2), 20m, "ReconciliationRequired");
        await _database.SeedPaymentAsync(Start.AddHours(3), 5m, "Declined");
        using var client = await OpenedDayAsync();

        var report = await ReportAsync(await CloseAsync(client, acknowledge: true));

        var day = report.BusinessDay;
        Assert.Equal((1, 2, 2, true), (day.OpenCashSessionsCount, day.OpenBillsCount, day.UnsettledPaymentsCount, day.OpenItemsAcknowledged));
        await using var command = _database.DataSource.CreateCommand(
            "SELECT after_state_json::text, actor_id FROM audit.audit_events WHERE event_name = 'reporting.business_day_closed' AND aggregate_id = @id;");
        command.Parameters.AddWithValue("id", day.BusinessDayId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        var after = JsonDocument.Parse(reader.GetString(0)).RootElement;
        Assert.Equal((1, 2, 2, true), (
            after.GetProperty("openCashSessions").GetInt32(), after.GetProperty("openBills").GetInt32(),
            after.GetProperty("unsettledPayments").GetInt32(), after.GetProperty("openItemsAcknowledged").GetBoolean()));
        Assert.Equal(EndOfDayTestDatabase.ManagerUserId, reader.GetGuid(1));
        Assert.False(await reader.ReadAsync());
    }

    [Fact]
    public async Task ACloseWithNothingOpenNeedsNoAcknowledgementAndRecordsNone()
    {
        using var client = await OpenedDayAsync();

        var preflight = (await client.GetFromJsonAsync<BusinessDayPreflightV1>($"{Base}/{Day:yyyy-MM-dd}/close-preflight"))!;
        var day = (await ReportAsync(await CloseAsync(client))).BusinessDay;

        Assert.False(preflight.RequiresAcknowledgement);
        Assert.False(day.OpenItemsAcknowledged);
        Assert.Equal((0, 0, 0), (day.OpenCashSessionsCount, day.OpenBillsCount, day.UnsettledPaymentsCount));
    }

    [Fact]
    public async Task ThePreflightShowsTheSameFiguresTheCloseThenStores()
    {
        await _database.SeedPaymentAsync(Start.AddHours(1), 400m);
        var order = await _database.SeedOrderAsync(Start.AddHours(2), "Completed");
        await _database.SeedAuditAsync("Order.ItemVoided", order, Start.AddHours(3));
        await _database.SeedPrintJobAsync("Mutfak", Start.AddHours(1), Start.AddHours(1), "Failed");
        var bill = await _database.SeedBillAsync("Paid", Start.AddHours(4));
        await _database.SeedTipAsync(bill, 40m);
        using var client = await OpenedDayAsync();

        var preflight = (await client.GetFromJsonAsync<BusinessDayPreflightV1>($"{Base}/{Day:yyyy-MM-dd}/close-preflight"))!;
        var day = (await ReportAsync(await CloseAsync(client))).BusinessDay;

        Assert.Equal(
            (preflight.Revenue, preflight.Tips, preflight.Orders, preflight.CancelledItems, preflight.PrintFailures),
            (day.TotalRevenue, day.TotalTipsAmount, day.TotalOrdersCount, day.TotalCancelledItemsCount, day.TotalPrintFailuresCount));
        Assert.Equal((360m, 40m, 1, 1, 1), (day.TotalRevenue, day.TotalTipsAmount, day.TotalOrdersCount, day.TotalCancelledItemsCount, day.TotalPrintFailuresCount));

        await using var command = _database.DataSource.CreateCommand(
            "SELECT after_state_json::text FROM audit.audit_events WHERE event_name = 'reporting.business_day_closed' AND aggregate_id = @id;");
        command.Parameters.AddWithValue("id", day.BusinessDayId);
        var after = JsonDocument.Parse((string)(await command.ExecuteScalarAsync())!).RootElement;
        Assert.Equal("2026-02-03", after.GetProperty("businessDate").GetString());
        Assert.Equal(
            (360m, 40m, 1, 1, 1),
            (after.GetProperty("revenue").GetDecimal(), after.GetProperty("tips").GetDecimal(), after.GetProperty("orders").GetInt32(),
             after.GetProperty("cancelledItems").GetInt32(), after.GetProperty("printFailures").GetInt32()));
    }

    [Fact]
    public async Task RevenueIsPaymentsLessRefundsLessTipsWhenARefundAndATipFallInTheSameDay()
    {
        await _database.SeedPaymentAsync(Start.AddHours(1), 1000m);
        await using (var refund = _database.DataSource.CreateCommand(
            """
            INSERT INTO payments.payment_reversals
                (reversal_id, payment_id, reversal_type, amount, reason, refunded_by, idempotency_key, occurred_at)
            SELECT gen_random_uuid(), payment_id, 'PartialRefund', 200, 'test', gen_random_uuid(), 'k-' || payment_id, @at
            FROM payments.payments;
            UPDATE payments.payments SET status = 'PartiallyRefunded';
            """))
        {
            refund.Parameters.AddWithValue("at", Start.AddHours(5).UtcDateTime);
            await refund.ExecuteNonQueryAsync();
        }
        var paid = await _database.SeedBillAsync("Paid", Start.AddHours(2));
        await _database.SeedTipAsync(paid, 50m);
        using var client = await OpenedDayAsync();

        var day = (await ReportAsync(await CloseAsync(client))).BusinessDay;

        Assert.Equal((750m, 50m), (day.TotalRevenue, day.TotalTipsAmount));
    }

    [Fact]
    public async Task EveryFigureIncludesTheStartInstantAndExcludesTheEndInstant()
    {
        var atStart = await _database.SeedOrderAsync(Start, "Completed", servingUserId: Guid.NewGuid(), total: 10m);
        var atEnd = await _database.SeedOrderAsync(End, "Completed", servingUserId: Guid.NewGuid(), total: 10m);
        await _database.SeedAuditAsync("Order.ItemVoided", atStart, Start);
        await _database.SeedAuditAsync("Order.ItemVoided", atEnd, End);
        await _database.SeedPrintJobAsync("Mutfak", Start, Start, "Failed");
        await _database.SeedPrintJobAsync("Mutfak", End, End, "Failed");
        await _database.SeedPaymentAsync(Start, 100m);
        await _database.SeedPaymentAsync(End, 900m);
        await _database.SeedTipAsync(await _database.SeedBillAsync("Paid", Start), 7m);
        await _database.SeedTipAsync(await _database.SeedBillAsync("Paid", End), 70m);
        await _database.SeedCashSessionAsync("Closed", Start, 10m, 10m);
        await _database.SeedCashSessionAsync("Closed", End, 10m, 10m);
        using var client = await OpenedDayAsync();

        var day = (await ReportAsync(await CloseAsync(client))).BusinessDay;

        Assert.Equal((1, 1, 1, 7m), (day.TotalOrdersCount, day.TotalCancelledItemsCount, day.TotalPrintFailuresCount, day.TotalTipsAmount));
        Assert.Equal(93m, day.TotalRevenue);
        Assert.Equal(1, day.CashReconciliation!.SessionsClosed);
    }

    [Fact]
    public async Task OnlyOpenOrCountingOrClosingSessionsAndUnsettledPaymentsOfAnyAgeAreOpenItems()
    {
        await _database.SeedCashSessionAsync("Open", null, 0m, 0m);
        await _database.SeedCashSessionAsync("Counting", null, 0m, 0m);
        await _database.SeedCashSessionAsync("Closing", null, 0m, 0m);
        await _database.SeedCashSessionAsync("Closed", Start.AddHours(1), 5m, 5m);
        await _database.SeedCashSessionAsync("Reconciled", Start.AddHours(2), 5m, 5m);
        await _database.SeedPaymentAsync(Start.AddDays(-10), 10m, "Pending");
        await _database.SeedPaymentAsync(Start.AddDays(-10), 10m, "Approved");
        using var client = await OpenedDayAsync();

        var preflight = (await client.GetFromJsonAsync<BusinessDayPreflightV1>($"{Base}/{Day:yyyy-MM-dd}/close-preflight"))!;

        Assert.Equal((3, 0, 1), (preflight.OpenCashSessions, preflight.OpenBills, preflight.UnsettledPayments));
    }

    [Fact]
    public async Task AnItemOpenedAfterThePreflightIsStillRefusedWithoutAnAcknowledgement()
    {
        using var client = await OpenedDayAsync();
        var before = (await client.GetFromJsonAsync<BusinessDayPreflightV1>($"{Base}/{Day:yyyy-MM-dd}/close-preflight"))!;
        Assert.False(before.RequiresAcknowledgement);
        await _database.SeedBillAsync("Open");

        using var refused = await CloseAsync(client);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        var error = (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");
        Assert.Equal("BUSINESS_DAY_OPEN_ITEMS_NOT_ACKNOWLEDGED", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ThePreflightOfADayThatWasNeverOpenedIsNotFound()
    {
        using var manager = Manager();
        using var missing = await manager.GetAsync($"{Base}/{Day:yyyy-MM-dd}/close-preflight");

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task ThePreflightNeedsTheViewPermissionSoASupervisorReadsItAndAnUnpermissionedOrAnonymousCallerDoesNot()
    {
        using var manager = await OpenedDayAsync();
        using var supervisor = new HttpClient { BaseAddress = _baseAddress };
        supervisor.DefaultRequestHeaders.Add("Cookie", $"{EndOfDayEndpoints.ManagerCookieName}={EndOfDayTestDatabase.SupervisorToken}");
        using var denied = new HttpClient { BaseAddress = _baseAddress };
        denied.DefaultRequestHeaders.Add("Cookie", $"{EndOfDayEndpoints.ManagerCookieName}={EndOfDayTestDatabase.DeniedToken}");
        using var anonymous = new HttpClient { BaseAddress = _baseAddress };
        var path = $"{Base}/{Day:yyyy-MM-dd}/close-preflight";

        Assert.Equal(HttpStatusCode.OK, (await supervisor.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task ThePreflightOfAClosedDayIsRefusedInsteadOfShowingLiveFigures()
    {
        using var client = await OpenedDayAsync();
        Assert.Equal(HttpStatusCode.OK, (await CloseAsync(client)).StatusCode);

        using var preflight = await client.GetAsync($"{Base}/{Day:yyyy-MM-dd}/close-preflight");

        Assert.Equal(HttpStatusCode.Conflict, preflight.StatusCode);
        var error = (await preflight.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");
        Assert.Equal("INVALID_OPERATION", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task TwoClosesAtOnceCloseTheDayOnceAndWriteOneAuditEventAndOneSetOfSummaries()
    {
        var waiter = Guid.NewGuid();
        await _database.SeedOrderAsync(Start.AddHours(1), "Completed", servingUserId: waiter, total: 10m);
        using var client = await OpenedDayAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => CloseAsync(client)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        foreach (var loser in responses.Where(r => r.StatusCode != HttpStatusCode.OK))
        {
            Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode);
            var error = (await loser.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");
            Assert.Equal("INVALID_OPERATION", error.GetProperty("code").GetString());
        }
        Assert.Equal(1L, await _database.CountAsync("SELECT count(*) FROM audit.audit_events WHERE event_name = 'reporting.business_day_closed'"));
        Assert.Equal(1L, await _database.CountAsync("SELECT count(*) FROM reporting.waiter_performance_summaries"));
    }

    [Fact]
    public async Task ACloseWithoutABodyIsAnUnacknowledgedClose()
    {
        using var client = await OpenedDayAsync();
        using var clean = await client.PostAsync($"{Base}/{Day:yyyy-MM-dd}/close", null);
        Assert.Equal(HttpStatusCode.OK, clean.StatusCode);

        var other = new DateOnly(2026, 2, 4);
        using var opened = await client.PostAsJsonAsync($"{Base}/open", new OpenBusinessDayV1(other));
        Assert.Equal(HttpStatusCode.Created, opened.StatusCode);
        await _database.SeedBillAsync("Open");
        using var refused = await client.PostAsync($"{Base}/{other:yyyy-MM-dd}/close", null);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        var error = (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");
        Assert.Equal("BUSINESS_DAY_OPEN_ITEMS_NOT_ACKNOWLEDGED", error.GetProperty("code").GetString());
    }
}
