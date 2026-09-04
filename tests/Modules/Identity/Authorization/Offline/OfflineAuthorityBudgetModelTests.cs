using ALKAROS.Identity.Authorization.Offline;
using ALKAROS.Identity.Authorization.Policies;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Offline;

public sealed class OfflineAuthorityBudgetModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 20, 0, 0, TimeSpan.Zero);

    private static OfflineAuthorityBudget Budget(params OfflineAuthorityBudgetLine[] lines)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now, Now.AddHours(4), lines);

    [Fact]
    public void IsExpiredAtTheExpiryInstantAndAfter()
    {
        var budget = Budget();

        budget.IsExpiredAt(Now.AddHours(3)).Should().BeFalse();
        budget.IsExpiredAt(Now.AddHours(4)).Should().BeTrue("the boundary is inclusive");
        budget.IsExpiredAt(Now.AddHours(5)).Should().BeTrue();
    }

    [Fact]
    public void LineForReturnsTheMatchingPermissionOrNull()
    {
        var budget = Budget(
            new OfflineAuthorityBudgetLine("bills.comp", 150m, 2),
            new OfflineAuthorityBudgetLine("bills.void", null, 1));

        budget.LineFor("bills.comp")!.LimitAmount.Should().Be(150m);
        budget.LineFor("bills.void")!.MaxCount.Should().Be(1);
        budget.LineFor("cash.drawer").Should().BeNull();
    }

    [Fact]
    public void AdmitsChecksCountThenAmount()
    {
        var capped = new OfflineAuthorityBudgetLine("bills.comp", 150m, 2);

        capped.Admits(150m, 0).Should().BeTrue();
        capped.Admits(150m, 1).Should().BeTrue();
        capped.Admits(150m, 2).Should().BeFalse("the count is spent");
        capped.Admits(150.01m, 0).Should().BeFalse("over the monetary limit");
        capped.Admits(-1m, 0).Should().BeFalse("a negative delta is never admitted");

        var countOnly = new OfflineAuthorityBudgetLine("bills.void", null, 1);
        countOnly.Admits(9_999m, 0).Should().BeTrue("no monetary cap");
        countOnly.Admits(1m, 1).Should().BeFalse("count is spent");
    }

    [Fact]
    public void LinesForKeepsOnlyAutoWithinPoliciesOfTheRole()
    {
        var policies = new[]
        {
            new AuthorizationPolicy(Guid.NewGuid(), "bills.comp", "waiter", PolicyMode.AutoWithin, 150m, 2, 28800, 1),
            new AuthorizationPolicy(Guid.NewGuid(), "bills.void", "waiter", PolicyMode.AutoWithin, 80m, 1, 28800, 1),
            new AuthorizationPolicy(Guid.NewGuid(), "cash.drawer", "waiter", PolicyMode.AlwaysAllow, null, null, null, 1),
            new AuthorizationPolicy(Guid.NewGuid(), "bills.discount", "waiter", PolicyMode.AlwaysDeny, null, null, null, 1),
            new AuthorizationPolicy(Guid.NewGuid(), "bills.comp", "cashier", PolicyMode.AutoWithin, 500m, 5, 28800, 1),
        };

        var lines = OfflineAuthorityBudgetPolicy.LinesFor("waiter", policies);

        lines.Select(l => l.PermissionCode).Should().Equal("bills.comp", "bills.void");
        lines[0].LimitAmount.Should().Be(150m);
        lines[0].MaxCount.Should().Be(2);
    }

    [Fact]
    public void LinesForIsEmptyWhenTheRoleHasNoAutoWithinPolicy()
        => OfflineAuthorityBudgetPolicy.LinesFor("runner", new[]
        {
            new AuthorizationPolicy(Guid.NewGuid(), "bills.comp", "waiter", PolicyMode.AutoWithin, 150m, 2, 28800, 1),
        }).Should().BeEmpty();
}
