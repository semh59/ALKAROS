using ALKAROS.Identity.Authorization.Delegations;
using ALKAROS.Identity.Authorization.Grants;
using ALKAROS.Identity.Authorization.Policies;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Delegations;

/// <summary>
/// The delegation resolver, and the grant service with it wired, over the real
/// repositories and database. Only the clock is faked.
/// </summary>
public sealed class DelegationEscalationResolverTests : IClassFixture<DelegationDatabase>
{
    private readonly DelegationDatabase _db;
    private readonly PostgresAuthorizationDelegationRepository _delegations;
    private readonly PostgresAuthorizationGrantRepository _grants;
    private readonly PostgresAuthorizationPolicyRepository _policies;
    private readonly DateTimeOffset _now = new(2026, 9, 4, 20, 0, 0, TimeSpan.Zero);

    public DelegationEscalationResolverTests(DelegationDatabase db)
    {
        _db = db;
        _delegations = new PostgresAuthorizationDelegationRepository(db.DataSource);
        _grants = new PostgresAuthorizationGrantRepository(db.DataSource);
        _policies = new PostgresAuthorizationPolicyRepository(db.DataSource);
    }

    private AuthorizationGrantService ServiceWithDelegation()
        => new(_grants, _policies,
            escalationResolvers: new[] { new DelegationEscalationResolver(_delegations) },
            nowUtc: () => _now);

    private static GrantRequest Request(string key, Guid requester, decimal amount)
        => new(key, "bills.comp", requester, "waiter", "CustomerChange", amount,
            SubjectType: "bill", SubjectId: Guid.NewGuid(), SubjectServingUserId: requester);

    [Fact]
    public async Task ResolverReturnsDelegationPathWhenAnActiveDelegationCovers()
    {
        var waiter = Guid.NewGuid();
        await _delegations.CreateAsync(
            new DelegationRequest("bills.comp", waiter, Guid.NewGuid(), 200m, _now.AddHours(2)), _now);

        var path = await new DelegationEscalationResolver(_delegations)
            .TryResolveAsync(Request("r-1", waiter, 150m), _now.AddMinutes(30));

        path.Should().Be(PolicyPath.Delegation);
    }

    [Fact]
    public async Task ResolverReturnsNullWhenNoDelegationApplies()
    {
        var path = await new DelegationEscalationResolver(_delegations)
            .TryResolveAsync(Request("r-2", Guid.NewGuid(), 10m), _now);

        path.Should().BeNull();
    }

    [Fact]
    public async Task GrantServiceAuthorizesAnEscalatedRequestViaTheDelegationInsteadOfAManager()
    {
        var waiter = Guid.NewGuid();
        await _delegations.CreateAsync(
            new DelegationRequest("bills.comp", waiter, Guid.NewGuid(), 200m, _now.AddHours(2)), _now);

        var result = await ServiceWithDelegation().RequestAsync(Request("svc-deleg", waiter, 120m));

        result.Outcome.Should().Be(GrantOutcome.Authorized);
        result.Grant.Status.Should().Be(GrantStatus.Granted);
        result.Grant.Path.Should().Be(PolicyPath.Delegation);
        result.Grant.ApproverUserId.Should().BeNull("a delegation, not a person, authorized it");
    }

    [Fact]
    public async Task GrantServiceStillEscalatesToPendingWhenTheDelegationDoesNotCoverTheAmount()
    {
        var waiter = Guid.NewGuid();
        await _delegations.CreateAsync(
            new DelegationRequest("bills.comp", waiter, Guid.NewGuid(), 100m, _now.AddHours(2)), _now);

        var result = await ServiceWithDelegation().RequestAsync(Request("svc-deleg-over", waiter, 150m));

        result.Outcome.Should().Be(GrantOutcome.Pending);
        result.Grant.Path.Should().BeNull();
    }

    [Fact]
    public async Task ARevokedDelegationNoLongerAuthorizes()
    {
        var waiter = Guid.NewGuid();
        var delegation = await _delegations.CreateAsync(
            new DelegationRequest("bills.comp", waiter, Guid.NewGuid(), 200m, _now.AddHours(2)), _now);
        await _delegations.RevokeAsync(delegation.DelegationId, _now.AddMinutes(10), Guid.NewGuid());

        var result = await ServiceWithDelegation().RequestAsync(Request("svc-deleg-revoked", waiter, 50m));

        result.Outcome.Should().Be(GrantOutcome.Pending);
    }
}
