using ALKAROS.Identity.Authorization.Delegations;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Delegations;

public sealed class AuthorizationDelegationModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 20, 0, 0, TimeSpan.Zero);

    private static AuthorizationDelegation Delegation(
        DateTimeOffset? expires = null, DateTimeOffset? revoked = null, decimal limit = 200m)
        => new(Guid.NewGuid(), "bills.comp", Guid.NewGuid(), Guid.NewGuid(),
            limit, Now.AddHours(-1), expires ?? Now.AddHours(2), revoked,
            revoked is null ? null : Guid.NewGuid());

    [Fact]
    public void IsActiveWhileNotRevokedAndBeforeExpiry()
    {
        Delegation().IsActiveAt(Now).Should().BeTrue();
        Delegation(expires: Now.AddMinutes(-1)).IsActiveAt(Now).Should().BeFalse();
        Delegation(revoked: Now.AddMinutes(-5)).IsActiveAt(Now).Should().BeFalse();
    }

    [Fact]
    public void CoversMatchesPermissionAmountAndActivity()
    {
        var d = Delegation(limit: 200m);

        d.Covers("bills.comp", 200m, Now).Should().BeTrue();
        d.Covers("bills.comp", 200.01m, Now).Should().BeFalse("amount exceeds the limit");
        d.Covers("bills.void", 10m, Now).Should().BeFalse("different permission");
        d.Covers("bills.comp", 10m, Now.AddHours(3)).Should().BeFalse("expired");
    }

    [Fact]
    public void RequestValidationRejectsBadInput()
    {
        var user = Guid.NewGuid();

        FluentActions.Invoking(() =>
            new DelegationRequest("bills.comp", user, user, 100m, Now.AddHours(1)).Validate(Now))
            .Should().Throw<ArgumentException>("grantee cannot equal delegator");

        FluentActions.Invoking(() =>
            new DelegationRequest("bills.comp", Guid.NewGuid(), Guid.NewGuid(), -1m, Now.AddHours(1)).Validate(Now))
            .Should().Throw<ArgumentException>("limit must not be negative");

        FluentActions.Invoking(() =>
            new DelegationRequest("bills.comp", Guid.NewGuid(), Guid.NewGuid(), 100m, Now).Validate(Now))
            .Should().Throw<ArgumentException>("expiry must be in the future");

        FluentActions.Invoking(() =>
            new DelegationRequest("bills.comp", Guid.NewGuid(), Guid.NewGuid(), 100m, Now.AddHours(1)).Validate(Now))
            .Should().NotThrow();
    }
}
