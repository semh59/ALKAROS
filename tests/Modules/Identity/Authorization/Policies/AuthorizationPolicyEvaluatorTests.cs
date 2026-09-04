using ALKAROS.Identity.Authorization.Policies;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Policies;

public sealed class AuthorizationPolicyEvaluatorTests
{
    private static AuthorizationPolicy AutoWithin(decimal limit, int maxCount, int windowSeconds = 28800)
        => new(Guid.NewGuid(), "bills.comp", "waiter", PolicyMode.AutoWithin, limit, maxCount, windowSeconds, 1);

    [Fact]
    public void NoPolicyEscalates()
    {
        AuthorizationPolicyEvaluator.Evaluate(null, requestedAmount: 0m, priorAutoGrantsInWindow: 0)
            .Should().Be(PolicyOutcome.Escalate);
    }

    [Fact]
    public void AlwaysDenyDenies()
    {
        var policy = new AuthorizationPolicy(
            Guid.NewGuid(), "bills.discount", "cashier", PolicyMode.AlwaysDeny, null, null, null, 1);

        AuthorizationPolicyEvaluator.Evaluate(policy, 0m, 0).Should().Be(PolicyOutcome.Deny);
    }

    [Fact]
    public void AlwaysAllowAutoApproves()
    {
        var policy = new AuthorizationPolicy(
            Guid.NewGuid(), "tables.status", "waiter", PolicyMode.AlwaysAllow, null, null, null, 1);

        AuthorizationPolicyEvaluator.Evaluate(policy, 999_999m, 100).Should().Be(PolicyOutcome.AutoApprove);
    }

    [Theory]
    [InlineData(100, 0, PolicyOutcome.AutoApprove)]   // under the limit, no prior grants
    [InlineData(150, 1, PolicyOutcome.AutoApprove)]   // exactly the limit, one prior grant (still < 2)
    [InlineData(150.01, 0, PolicyOutcome.Escalate)]   // one kuruş over the limit
    [InlineData(100, 2, PolicyOutcome.Escalate)]      // count reached
    [InlineData(100, 3, PolicyOutcome.Escalate)]      // count exceeded
    public void AutoWithinChecksBothLimitAndCount(decimal amount, int priorGrants, PolicyOutcome expected)
    {
        var policy = AutoWithin(limit: 150m, maxCount: 2);

        AuthorizationPolicyEvaluator.Evaluate(policy, amount, priorGrants).Should().Be(expected);
    }

    [Fact]
    public void AutoWithinZeroLimitStillAutoApprovesAZeroAmountAction()
    {
        // A grant on a non-monetary action (transfer, merge) arrives with amount 0.
        var policy = AutoWithin(limit: 0m, maxCount: 5);

        AuthorizationPolicyEvaluator.Evaluate(policy, requestedAmount: 0m, priorAutoGrantsInWindow: 0)
            .Should().Be(PolicyOutcome.AutoApprove);
    }

    [Fact]
    public void AutoWithinZeroCountAlwaysEscalates()
    {
        var policy = AutoWithin(limit: 500m, maxCount: 0);

        AuthorizationPolicyEvaluator.Evaluate(policy, 1m, 0).Should().Be(PolicyOutcome.Escalate);
    }

    [Fact]
    public void NegativeInputsThrow()
    {
        var policy = AutoWithin(150m, 2);

        FluentActions.Invoking(() => AuthorizationPolicyEvaluator.Evaluate(policy, -1m, 0))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => AuthorizationPolicyEvaluator.Evaluate(policy, 1m, -1))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void PolicyValidateRejectsAutoWithinMissingBounds()
    {
        FluentActions.Invoking(() => new AuthorizationPolicy(
                Guid.NewGuid(), "bills.void", "waiter", PolicyMode.AutoWithin, 100m, null, 60, 1).Validate())
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PolicyValidateRejectsBoundsOnANonAutoWithinMode()
    {
        FluentActions.Invoking(() => new AuthorizationPolicy(
                Guid.NewGuid(), "bills.void", "waiter", PolicyMode.AlwaysDeny, 100m, null, null, 1).Validate())
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PolicyModeTextRoundTrips()
    {
        foreach (var mode in Enum.GetValues<PolicyMode>())
            PolicyModeText.FromText(PolicyModeText.ToText(mode)).Should().Be(mode);
    }
}
