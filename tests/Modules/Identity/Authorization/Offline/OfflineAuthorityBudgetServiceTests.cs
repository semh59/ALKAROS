using ALKAROS.Identity.Authorization.Offline;
using ALKAROS.Identity.Authorization.Policies;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Offline;

public sealed class OfflineAuthorityBudgetServiceTests : IClassFixture<OfflineBudgetDatabase>
{
    private readonly PostgresAuthorizationPolicyRepository _policies;
    private readonly PostgresOfflineAuthorityBudgetRepository _budgets;
    private readonly DateTimeOffset _now = new(2026, 9, 4, 9, 0, 0, TimeSpan.Zero);

    public OfflineAuthorityBudgetServiceTests(OfflineBudgetDatabase db)
    {
        _policies = new PostgresAuthorizationPolicyRepository(db.DataSource);
        _budgets = new PostgresOfflineAuthorityBudgetRepository(db.DataSource);
    }

    private OfflineAuthorityBudgetService Service()
        => new(_policies, _budgets, () => _now);

    [Fact]
    public async Task IssueSnapshotsOnlyTheRoleAutoWithinPoliciesAndExpiresInFourHours()
    {
        var role = "svc-issue-waiter";
        await _policies.UpsertAsync(
            new AuthorizationPolicy(Guid.Empty, "bills.comp", role, PolicyMode.AutoWithin, 150m, 2, 28800, 1),
            null, Guid.NewGuid());
        await _policies.UpsertAsync(
            new AuthorizationPolicy(Guid.Empty, "bills.void", role, PolicyMode.AutoWithin, 90m, 1, 28800, 1),
            null, Guid.NewGuid());
        await _policies.UpsertAsync(
            new AuthorizationPolicy(Guid.Empty, "cash.drawer", role, PolicyMode.AlwaysAllow, null, null, null, 1),
            null, Guid.NewGuid());
        await _policies.UpsertAsync(
            new AuthorizationPolicy(Guid.Empty, "bills.comp", "svc-issue-other", PolicyMode.AutoWithin, 999m, 9, 28800, 1),
            null, Guid.NewGuid());

        var user = Guid.NewGuid();
        var session = Guid.NewGuid();
        var budget = await Service().IssueAsync(user, role, session);

        budget.UserId.Should().Be(user);
        budget.ExpiresAt.Should().Be(_now.AddHours(4));
        budget.Lines.Select(l => l.PermissionCode).Should().Equal("bills.comp", "bills.void");
        budget.Lines.Should().NotContain(l => l.PermissionCode == "cash.drawer");

        var reloaded = await _budgets.GetBySessionAsync(session);
        reloaded!.BudgetId.Should().Be(budget.BudgetId);
    }

    [Fact]
    public async Task IssueHonoursAnExplicitTtl()
    {
        var budget = await Service().IssueAsync(
            Guid.NewGuid(), "svc-ttl-role", Guid.NewGuid(), TimeSpan.FromHours(1));

        budget.ExpiresAt.Should().Be(_now.AddHours(1));
    }

    [Fact]
    public async Task IssueForARoleWithNoAutoWithinPolicyProducesABudgetWithNoLines()
    {
        var budget = await Service().IssueAsync(Guid.NewGuid(), "svc-empty-role", Guid.NewGuid());

        budget.Lines.Should().BeEmpty();
    }

    [Fact]
    public async Task IssueForTheSameSessionMakesTheSessionResolveToTheNewestBudget()
    {
        // A1: re-issue no longer deletes the prior budget row (it can already
        // carry offline_authority_replays with no ON DELETE CASCADE) — the
        // session lookup just moves on to the newest one (ORDER BY issued_at
        // DESC). A distinct issued_at per issue is what "newest" means; two
        // real issues are never same-instant the way a fixed test clock is.
        var session = Guid.NewGuid();
        var first = await Service().IssueAsync(Guid.NewGuid(), "svc-reissue-role", session);
        var second = await new OfflineAuthorityBudgetService(_policies, _budgets, () => _now.AddMinutes(5))
            .IssueAsync(Guid.NewGuid(), "svc-reissue-role", session);

        second.BudgetId.Should().NotBe(first.BudgetId);
        (await _budgets.GetAsync(first.BudgetId)).Should().NotBeNull("the prior budget is superseded, not deleted");
        (await _budgets.GetBySessionAsync(session))!.BudgetId.Should().Be(second.BudgetId);
    }

    [Fact]
    public async Task IssueRejectsANonPositiveTtl()
        => await FluentActions.Invoking(() => Service().IssueAsync(
                Guid.NewGuid(), "svc-bad-ttl", Guid.NewGuid(), TimeSpan.Zero))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
}
