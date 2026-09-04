using ALKAROS.Identity.Authorization.Behavioural;
using ALKAROS.Identity.Authorization.Grants;
using ALKAROS.Identity.Authorization.Policies;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Behavioural;

/// <summary>
/// The behavioural gate wired into the grant service, over the real
/// repositories and database. Only the clock is faked.
/// </summary>
public sealed class BehaviouralTighteningGateTests : IClassFixture<BehaviouralDatabase>
{
    private readonly BehaviouralDatabase _db;
    private readonly PostgresAuthorizationGrantRepository _grants;
    private readonly PostgresAuthorizationPolicyRepository _policies;
    private readonly PostgresBehaviouralTighteningRepository _tightenings;
    private readonly BehaviouralTighteningGate _gate;
    private readonly BehaviouralTighteningService _tighteningService;
    private readonly DateTimeOffset _now = new(2026, 9, 4, 20, 0, 0, TimeSpan.Zero);

    public BehaviouralTighteningGateTests(BehaviouralDatabase db)
    {
        _db = db;
        _grants = new PostgresAuthorizationGrantRepository(db.DataSource);
        _policies = new PostgresAuthorizationPolicyRepository(db.DataSource);
        _tightenings = new PostgresBehaviouralTighteningRepository(db.DataSource);
        _gate = new BehaviouralTighteningGate(
            _tightenings, new PostgresBehaviouralRateSource(db.DataSource));
        _tighteningService = new BehaviouralTighteningService(_tightenings, () => _now);
    }

    private AuthorizationGrantService Service()
        => new(_grants, _policies, prePolicyGates: new[] { _gate }, nowUtc: () => _now);

    private static GrantRequest Request(
        string key, Guid user, string role, string permission = "bills.void")
        => new(key, permission, user, role, "CustomerChange", Amount: 0m);

    private async Task AllowOutrightAsync(string permission, string role)
    {
        await _policies.UpsertAsync(
            new AuthorizationPolicy(Guid.Empty, permission, role, PolicyMode.AlwaysAllow, null, null, null, 1),
            null, Guid.NewGuid());
    }

    private async Task SpikeVoidRateAsync(Guid user)
    {
        for (var i = 0; i < 4; i++)
            await _db.SeedGrantedAsync(user, "bills.void", _now.AddHours(-2).AddMinutes(i), $"spike-{user:N}-{i}");
    }

    [Fact]
    public async Task WithoutASpikeAMonitoredActionStillAutoApproves()
    {
        var user = Guid.NewGuid();
        const string role = "gate-quiet-role";
        await AllowOutrightAsync("bills.void", role);

        var result = await Service().RequestAsync(Request("gate-quiet", user, role));

        result.Outcome.Should().Be(GrantOutcome.Authorized);
        result.Grant.Path.Should().Be(PolicyPath.Auto);
        (await _tightenings.FindActiveAsync(user, "bills.void")).Should().BeNull();
    }

    [Fact]
    public async Task ASpikeOpensATighteningAndTurnsTheAutoApprovalIntoAPendingRequest()
    {
        var user = Guid.NewGuid();
        const string role = "gate-spike-role";
        await AllowOutrightAsync("bills.void", role);
        await SpikeVoidRateAsync(user);

        var result = await Service().RequestAsync(Request("gate-spike", user, role));

        result.Outcome.Should().Be(GrantOutcome.Pending);
        result.Grant.Status.Should().Be(GrantStatus.Pending);

        var tightening = await _tightenings.FindActiveAsync(user, "bills.void");
        tightening.Should().NotBeNull("the spike left an audit row");
        tightening!.RecentCount.Should().BeGreaterThanOrEqualTo(4);
        tightening.TriggerRatio.Should().BeGreaterThanOrEqualTo(3m);
    }

    [Fact]
    public async Task AnOpenTighteningKeepsForcingEscalationOnLaterRequests()
    {
        var user = Guid.NewGuid();
        const string role = "gate-open-role";
        await AllowOutrightAsync("bills.void", role);
        await SpikeVoidRateAsync(user);

        await Service().RequestAsync(Request("gate-open-1", user, role));
        var second = await Service().RequestAsync(Request("gate-open-2", user, role));

        second.Outcome.Should().Be(GrantOutcome.Pending);
    }

    [Fact]
    public async Task AfterAManagerClearsTheFlowReturnsToAutoApproval()
    {
        var user = Guid.NewGuid();
        const string role = "gate-clear-role";
        await AllowOutrightAsync("bills.void", role);
        await SpikeVoidRateAsync(user);
        await Service().RequestAsync(Request("gate-clear-trip", user, role));

        var open = await _tightenings.FindActiveAsync(user, "bills.void");
        var cleared = await _tighteningService.ClearAsync(open!.TighteningId, Guid.NewGuid());
        cleared.IsActive.Should().BeFalse();

        var afterClear = await Service().RequestAsync(Request("gate-clear-after", user, role));

        afterClear.Outcome.Should().Be(GrantOutcome.Authorized);
        afterClear.Grant.Path.Should().Be(PolicyPath.Auto);
    }

    [Fact]
    public async Task APermissionOutsideTheMonitoredSetIsNeverGated()
    {
        var user = Guid.NewGuid();
        const string role = "gate-unmonitored-role";
        await AllowOutrightAsync("tables.reserve", role);
        // A rate that would trip the gate for a monitored permission.
        for (var i = 0; i < 6; i++)
            await _db.SeedGrantedAsync(user, "tables.reserve", _now.AddHours(-1).AddMinutes(i), $"unmon-{i}");

        var result = await Service().RequestAsync(Request("gate-unmonitored", user, role, "tables.reserve"));

        result.Outcome.Should().Be(GrantOutcome.Authorized);
        (await _tighteningService.ListActiveAsync())
            .Should().NotContain(t => t.UserId == user);
    }

    [Fact]
    public async Task AnAlwaysDenyPolicyStillWinsOverAForcedEscalation()
    {
        var user = Guid.NewGuid();
        const string role = "gate-deny-role";
        await _policies.UpsertAsync(
            new AuthorizationPolicy(Guid.Empty, "bills.comp", role, PolicyMode.AlwaysDeny, null, null, null, 1),
            null, Guid.NewGuid());
        for (var i = 0; i < 4; i++)
            await _db.SeedGrantedAsync(user, "bills.comp", _now.AddHours(-2).AddMinutes(i), $"deny-spike-{i}");

        var result = await Service().RequestAsync(Request("gate-deny", user, role, "bills.comp"));

        result.Outcome.Should().Be(GrantOutcome.Refused);
    }
}
