using ALKAROS.Identity.Authorization.Grants;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Grants;

public sealed class GrantModelTests
{
    private static GrantRequest Valid() => new(
        IdempotencyKey: "cmd-1",
        PermissionCode: "bills.void",
        RequesterUserId: Guid.NewGuid(),
        RequesterRoleCode: "waiter",
        ReasonCode: "OperatorError",
        Amount: 42m);

    [Fact]
    public void ValidRequestPasses()
        => FluentActions.Invoking(() => Valid().Validate()).Should().NotThrow();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankIdempotencyKeyIsRejected(string key)
        => FluentActions.Invoking(() => (Valid() with { IdempotencyKey = key }).Validate())
            .Should().Throw<ArgumentException>();

    [Fact]
    public void NegativeAmountIsRejected()
        => FluentActions.Invoking(() => (Valid() with { Amount = -0.01m }).Validate())
            .Should().Throw<ArgumentException>();

    [Fact]
    public void SubjectTypeAndIdMustComeTogether()
    {
        FluentActions.Invoking(() => (Valid() with { SubjectType = "bill", SubjectId = null }).Validate())
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => (Valid() with { SubjectType = null, SubjectId = Guid.NewGuid() }).Validate())
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => (Valid() with { SubjectType = "bill", SubjectId = Guid.NewGuid() }).Validate())
            .Should().NotThrow();
    }

    [Fact]
    public void GrantTextRoundTrips()
    {
        foreach (var s in Enum.GetValues<GrantStatus>())
            GrantText.Status(GrantText.Status(s)).Should().Be(s);
        foreach (var p in Enum.GetValues<PolicyPath>())
            GrantText.Path(GrantText.Path(p)).Should().Be(p);
    }
}
