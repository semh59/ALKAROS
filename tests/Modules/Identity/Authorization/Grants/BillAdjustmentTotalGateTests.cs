using ALKAROS.Identity.Authorization.Grants;
using ALKAROS.Identity.Authorization.Policies;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Grants;

/// <summary>
/// The per-bill total cap over the real grant service, repositories and a real database: every request alone fits its
/// role limit, but what one bill has already been granted counts against the next request.
/// </summary>
public sealed class BillAdjustmentTotalGateTests : IClassFixture<GrantDatabase>
{
    private readonly PostgresAuthorizationGrantRepository _grants;
    private readonly PostgresAuthorizationPolicyRepository _policies;
    private readonly FixedCap _cap = new(120m);

    public BillAdjustmentTotalGateTests(GrantDatabase db)
    {
        _grants = new PostgresAuthorizationGrantRepository(db.DataSource);
        _policies = new PostgresAuthorizationPolicyRepository(db.DataSource);
    }

    private sealed class FixedCap(decimal cap) : IBillAdjustmentTotalCap
    {
        public decimal Cap { get; set; } = cap;

        public Task<decimal> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Cap);
    }

    private AuthorizationGrantService Service()
        => new(_grants, _policies, null, [new BillAdjustmentTotalGate(_grants, _cap)]);

    private async Task AutoWithinAsync(string permission, string role, decimal limit)
        => await _policies.UpsertAsync(
            new AuthorizationPolicy(Guid.Empty, permission, role, PolicyMode.AutoWithin, limit, 100, 28800, 1),
            null, Guid.NewGuid());

    private static GrantRequest Request(
        string permission, string role, Guid billId, decimal amount, Guid? requester = null, string subjectType = "Bill") => new(
        IdempotencyKey: Guid.NewGuid().ToString("N"),
        PermissionCode: permission,
        RequesterUserId: requester ?? Guid.NewGuid(),
        RequesterRoleCode: role,
        ReasonCode: "CustomerChange",
        Amount: amount,
        SubjectType: subjectType,
        SubjectId: billId,
        SubjectServingUserId: null);

    [Fact]
    public async Task CompsOfOneBillAddUpAndTheOneThatCrossesTheCapWaitsForAManager()
    {
        await AutoWithinAsync("bills.comp", "gate-cashier-a", 50m);
        var service = Service();
        var bill = Guid.NewGuid();

        var first = await service.RequestAsync(Request("bills.comp", "gate-cashier-a", bill, 50m));
        var second = await service.RequestAsync(Request("bills.comp", "gate-cashier-a", bill, 50m));
        var third = await service.RequestAsync(Request("bills.comp", "gate-cashier-a", bill, 50m));
        var otherBill = await service.RequestAsync(Request("bills.comp", "gate-cashier-a", Guid.NewGuid(), 50m));

        first.Outcome.Should().Be(GrantOutcome.Authorized);
        second.Outcome.Should().Be(GrantOutcome.Authorized);
        third.Outcome.Should().Be(GrantOutcome.Pending);
        otherBill.Outcome.Should().Be(GrantOutcome.Authorized);
    }

    [Fact]
    public async Task ARequestThatExactlyReachesTheCapIsStillApproved()
    {
        await AutoWithinAsync("bills.comp", "gate-cashier-b", 60m);
        var service = Service();
        var bill = Guid.NewGuid();

        var first = await service.RequestAsync(Request("bills.comp", "gate-cashier-b", bill, 60m));
        var second = await service.RequestAsync(Request("bills.comp", "gate-cashier-b", bill, 60m));
        var third = await service.RequestAsync(Request("bills.comp", "gate-cashier-b", bill, 0.01m));

        first.Outcome.Should().Be(GrantOutcome.Authorized);
        second.Outcome.Should().Be(GrantOutcome.Authorized);
        third.Outcome.Should().Be(GrantOutcome.Pending);
    }

    [Fact]
    public async Task ADiscountAndACompOnTheSameBillShareOneCap()
    {
        await AutoWithinAsync("bills.discount", "gate-waiter-c", 100m);
        await AutoWithinAsync("bills.comp", "gate-cashier-c", 50m);
        var service = Service();
        var bill = Guid.NewGuid();

        var discount = await service.RequestAsync(Request("bills.discount", "gate-waiter-c", bill, 100m));
        var comp = await service.RequestAsync(Request("bills.comp", "gate-cashier-c", bill, 50m));

        discount.Outcome.Should().Be(GrantOutcome.Authorized);
        comp.Outcome.Should().Be(GrantOutcome.Pending);
    }

    [Fact]
    public async Task APendingRequestDoesNotUseUpTheCapUntilAManagerGrantsIt()
    {
        await AutoWithinAsync("bills.comp", "gate-cashier-d", 50m);
        var service = Service();
        var bill = Guid.NewGuid();

        var unreviewed = await service.RequestAsync(Request("bills.comp", "gate-unlisted-role-d", bill, 500m));
        var next = await service.RequestAsync(Request("bills.comp", "gate-cashier-d", bill, 50m));

        unreviewed.Outcome.Should().Be(GrantOutcome.Pending);
        next.Outcome.Should().Be(GrantOutcome.Authorized);
    }

    [Fact]
    public async Task AGrantTheManagerApprovedCountsTowardTheNextRequest()
    {
        await AutoWithinAsync("bills.comp", "gate-cashier-e", 50m);
        var service = Service();
        var bill = Guid.NewGuid();
        var pending = await service.RequestAsync(Request("bills.comp", "gate-unlisted-role-e", bill, 100m));
        await _grants.ResolveAsync(pending.Grant.GrantId, GrantStatus.Granted, PolicyPath.Manual, Guid.NewGuid());

        var next = await service.RequestAsync(Request("bills.comp", "gate-cashier-e", bill, 40m));

        next.Outcome.Should().Be(GrantOutcome.Pending);
    }

    [Fact]
    public async Task ACapOfZeroSendsEveryRequestToAManager()
    {
        await AutoWithinAsync("bills.comp", "gate-cashier-f", 50m);
        _cap.Cap = 0m;
        try
        {
            var result = await Service().RequestAsync(Request("bills.comp", "gate-cashier-f", Guid.NewGuid(), 1m));

            result.Outcome.Should().Be(GrantOutcome.Pending);
        }
        finally
        {
            _cap.Cap = 120m;
        }
    }

    [Theory]
    [InlineData("bills.void", "Bill")]
    [InlineData("bills.comp", "OrderItem")]
    public async Task OtherPermissionsAndOtherSubjectsAreNotCapped(string permission, string subjectType)
    {
        await AutoWithinAsync(permission, "gate-cashier-g", 1000m);
        var service = Service();
        var subject = Guid.NewGuid();

        var first = await service.RequestAsync(Request(permission, "gate-cashier-g", subject, 500m, subjectType: subjectType));
        var second = await service.RequestAsync(Request(permission, "gate-cashier-g", subject, 500m, subjectType: subjectType));

        first.Outcome.Should().Be(GrantOutcome.Authorized);
        second.Outcome.Should().Be(GrantOutcome.Authorized);
    }

    [Fact]
    public async Task ConcurrentRequestsFromDifferentPeopleCannotPassTheCapTogether()
    {
        await AutoWithinAsync("bills.comp", "gate-cashier-h", 50m);
        var bill = Guid.NewGuid();

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            Service().RequestAsync(Request("bills.comp", "gate-cashier-h", bill, 50m))));

        results.Count(result => result.Outcome == GrantOutcome.Authorized).Should().Be(2);
        results.Count(result => result.Outcome == GrantOutcome.Pending).Should().Be(2);
    }
}
