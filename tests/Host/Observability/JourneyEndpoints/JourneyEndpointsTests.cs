using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.Experience.Observability;
using ALKAROS.Host.Experience.Observability.Tests;
using ALKAROS.Host.Observability.TraceCorrelation;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Observability.JourneyEndpoints.Tests;

public sealed class JourneyEndpointsTests : IClassFixture<ObservabilityTestDatabase>, IAsyncLifetime, IDisposable
{
    private static readonly ActivitySource Npgsql = new("Npgsql");

    private readonly ObservabilityTestDatabase _database;
    private readonly TraceSpanBuffer _buffer = new();
    private WebApplication? _app;

    public JourneyEndpointsTests(ObservabilityTestDatabase database) => _database = database;

    public async Task InitializeAsync()
    {
        await _database.ExecuteAsync("DELETE FROM observability.raw_ui_interactions;");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddObservabilityExperience();
        builder.Services.AddSingleton(_buffer);
        builder.Services.AddSingleton(new JourneyQueries(_database.DataSource, _buffer));
        _app = builder.Build();
        _app.MapJourneyEndpoints();
        await _app.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();

    }

    public void Dispose() => _buffer.Dispose();

    private JourneyQueries Queries => new(_database.DataSource, _buffer);

    private HttpClient Client(string? token)
    {
        var client = new HttpClient { BaseAddress = new Uri(_app!.Urls.First()) };
        if (token is not null)
            client.DefaultRequestHeaders.Add("Cookie", $"{ObservabilityEndpoints.ManagerCookieName}={token}");
        return client;
    }

    private Task<int> Click(string key, int durationMs, string outcome, Guid terminal, string role = "cashier", int hoursAgo = 0) =>
        _database.ExecuteAsync(
            """
            INSERT INTO observability.raw_ui_interactions (occurred_at, terminal_id, role, key, kind, duration_ms, outcome)
            VALUES (now() - make_interval(hours => @h), @t, @r, @k, 'click', @d, @o);
            """,
            ("h", hoursAgo), ("t", terminal), ("r", role), ("k", key), ("d", durationMs), ("o", outcome));

    private static string NewTraceId() => ActivityTraceId.CreateRandom().ToString();

    private void Request(string traceId, string route, int status, double durationMs, Action<ActivityTraceId>? inside = null)
    {
        _buffer.Begin(traceId);
        inside?.Invoke(ActivityTraceId.CreateFromString(traceId));
        _buffer.Complete(traceId, "POST", route, status, DateTimeOffset.UtcNow.AddMilliseconds(-durationMs), durationMs);
    }

    private static void DatabaseSpan(ActivityTraceId trace, string statement, bool fail = false)
    {
        var parent = new ActivityContext(trace, ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
        using var activity = Npgsql.StartActivity("query", ActivityKind.Client, parent)!;
        activity.SetTag("db.statement", statement);
        if (fail)
            activity.SetStatus(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task ActionsAreRankedBySlowestAndByErrorsAndFilteredByRoleAndTerminal()
    {
        var terminal = Guid.NewGuid();
        var other = Guid.NewGuid();
        await Click("b.pay.slow", 900, "ok", terminal);
        await Click("b.pay.slow", 700, "ok", terminal);
        await Click("b.pay.fail", 50, "error", terminal);
        await Click("b.pay.fail", 60, "error", terminal);
        await Click("b.waiter.only", 2000, "ok", other, role: "waiter");
        await Click("b.old", 5000, "ok", terminal, hoursAgo: 30);

        var slowest = await Queries.ActionsAsync("slowest", null, null, 24, CancellationToken.None);
        var failing = await Queries.ActionsAsync("failing", null, null, 24, CancellationToken.None);
        var byTerminal = await Queries.ActionsAsync("slowest", null, terminal, 24, CancellationToken.None);
        var byRole = await Queries.ActionsAsync("slowest", "waiter", null, 24, CancellationToken.None);
        var longWindow = await Queries.ActionsAsync("slowest", null, null, 48, CancellationToken.None);

        slowest.Select(a => a.Key).Should().Equal("b.waiter.only", "b.pay.slow", "b.pay.fail");
        slowest.Single(a => a.Key == "b.pay.slow").Should().Match<ActionSummary>(a => a.Count == 2 && a.AverageMilliseconds == 800 && a.MaxMilliseconds == 900);
        failing[0].Should().Match<ActionSummary>(a => a.Key == "b.pay.fail" && a.ErrorCount == 2);
        byTerminal.Select(a => a.Key).Should().NotContain("b.waiter.only");
        byRole.Should().ContainSingle().Which.Key.Should().Be("b.waiter.only");
        longWindow.Select(a => a.Key).Should().StartWith("b.old");
    }

    [Fact]
    public void RequestsAreRankedFilteredAndNeverShowTheScreensOwnCalls()
    {
        Request(NewTraceId(), "/api/orders", 200, 40);
        var slow = NewTraceId();
        Request(slow, "/api/bills/pay", 200, 900);
        var failed = NewTraceId();
        Request(failed, "/api/kitchen", 500, 20);
        Request(NewTraceId(), JourneyQueries.OwnRoute + "/actions", 200, 5000);

        Queries.Requests("slowest", 0)[0].TraceId.Should().Be(slow);
        Queries.Requests("failing", 0)[0].TraceId.Should().Be(failed);
        Queries.Requests("slowest", 100).Should().ContainSingle().Which.TraceId.Should().Be(slow);
        Queries.Requests("slowest", 0).Should().NotContain(r => r.Route.StartsWith(JourneyQueries.OwnRoute, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TimelineOrdersTheRequestItsDatabaseCallsAndItsEventsUnderOneTraceId()
    {
        var trace = NewTraceId();
        Request(trace, "/api/bills/pay", 200, 120, id =>
        {
            DatabaseSpan(id, "select 1 from bills where id = @id");
            DatabaseSpan(id, "update bills set paid = true where id = @id", fail: true);
        });
        await _database.ExecuteAsync(
            """
            INSERT INTO audit.audit_events (id, event_name, aggregate_type, aggregate_id, actor_type, correlation_id, occurred_at)
            VALUES (gen_random_uuid(), 'billing.bill.paid', 'Bill', gen_random_uuid(), 'User', @c, now() - interval '100 milliseconds'),
                   (gen_random_uuid(), 'billing.other', 'Bill', gen_random_uuid(), 'User', 'someone-else', now());
            """,
            ("c", trace));

        var journey = (await Queries.TimelineAsync(trace, CancellationToken.None))!;

        journey.TraceId.Should().Be(trace);
        journey.RequestCaptured.Should().BeTrue();
        journey.Steps.Select(s => s.Kind).Should().Equal("request", "event", "database", "database");
        journey.Steps.Select(s => s.OffsetMilliseconds).Should().BeInAscendingOrder();
        journey.Steps[3].Failed.Should().BeTrue();
        journey.Steps.Should().NotContain(s => s.Name == "billing.other");
        journey.Steps[1].Name.Should().Be("billing.bill.paid");
    }

    [Fact]
    public async Task TimelineStillShowsEventsAfterTheRequestLeftTheBufferAndIsEmptyForUnknownIds()
    {
        var trace = NewTraceId();
        await _database.ExecuteAsync(
            """
            INSERT INTO audit.audit_events (id, event_name, aggregate_type, aggregate_id, actor_type, correlation_id)
            VALUES (gen_random_uuid(), 'orders.order.created', 'Order', gen_random_uuid(), 'User', @c);
            """,
            ("c", trace));

        var journey = (await Queries.TimelineAsync(trace, CancellationToken.None))!;

        journey.RequestCaptured.Should().BeFalse();
        journey.Steps.Should().ContainSingle().Which.Kind.Should().Be("event");
        (await Queries.TimelineAsync(NewTraceId(), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task StaffMovementsListActorsWithTheirNamesAndHonourTheActorFilter()
    {
        var name = ObservabilityTestDatabase.SupervisorUserId;
        await _database.ExecuteAsync(
            """
            INSERT INTO audit.audit_events (id, event_name, aggregate_type, aggregate_id, actor_id, actor_type, correlation_id)
            VALUES (gen_random_uuid(), 'staff.test.moved', 'Staff', gen_random_uuid(), @a, 'User', 'x'),
                   (gen_random_uuid(), 'staff.test.system', 'Staff', gen_random_uuid(), NULL, 'System', 'x');
            """,
            ("a", name));

        var rows = await Queries.StaffMovementsAsync(name, 24, CancellationToken.None);

        rows.Should().Contain(r => r.EventName == "staff.test.moved" && r.ActorName == "Observability Supervisor");
        rows.Should().NotContain(r => r.EventName == "staff.test.system");
        (await Queries.StaffMovementsAsync(Guid.NewGuid(), 24, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task CallsWithoutASessionAreUnauthorizedAndWithoutPermissionAreForbidden()
    {
        using var anonymous = Client(null);
        using var denied = Client(ObservabilityTestDatabase.DeniedToken);

        (await anonymous.GetAsync("/api/v1/management/observability/journeys/actions")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await denied.GetAsync("/api/v1/management/observability/journeys/actions")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await denied.GetAsync("/api/v1/management/observability/journeys/requests")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await denied.GetAsync("/api/v1/management/observability/journeys/staff-movements")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ViewOnlySupervisorSeesJourneysButNotStaffMovements()
    {
        var trace = NewTraceId();
        Request(trace, "/api/bills/pay", 200, 80);
        using var viewOnly = Client(ObservabilityTestDatabase.ViewOnlyToken);
        using var manager = Client(ObservabilityTestDatabase.SupervisorToken);

        (await viewOnly.GetAsync("/api/v1/management/observability/journeys/actions")).StatusCode.Should().Be(HttpStatusCode.OK);
        var journey = await viewOnly.GetFromJsonAsync<Journey>($"/api/v1/management/observability/journeys/requests/{trace}");
        journey!.TraceId.Should().Be(trace);
        (await viewOnly.GetAsync("/api/v1/management/observability/journeys/staff-movements")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await manager.GetAsync("/api/v1/management/observability/journeys/staff-movements")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MalformedTraceIdsAreNotFoundEvenWhenAnEventCarriesThem()
    {
        const string traceId = "not-a-trace-id";
        await _database.ExecuteAsync(
            """
            INSERT INTO audit.audit_events (id, event_name, aggregate_type, aggregate_id, actor_type, correlation_id)
            VALUES (gen_random_uuid(), 'x.y.z', 'X', gen_random_uuid(), 'User', @c);
            """,
            ("c", traceId));
        using var manager = Client(ObservabilityTestDatabase.SupervisorToken);

        (await manager.GetAsync($"/api/v1/management/observability/journeys/requests/{traceId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
