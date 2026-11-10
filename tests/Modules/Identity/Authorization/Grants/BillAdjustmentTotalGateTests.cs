using ALKAROS.Identity.Authorization.Grants;
using ALKAROS.Identity.Authorization.Policies;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Grants;

/// <summary>
/// The per-bill total cap over the real grant service, repositories and a real database: every request alone fits its
/// role limit, but what one bill has already had taken off counts against the next request.
/// </summary>
public sealed class BillAdjustmentTotalGateTests : IClassFixture<GrantDatabase>
{
    private readonly PostgresAuthorizationGrantRepository _grants;
    private readonly PostgresAuthorizationPolicyRepository _policies;
    private readonly FixedCap _cap = new(120m);
    private readonly AppliedTotals _applied = new();

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

    /// <summary>Stands for the saved adjustments of a bill: a test adds what the request it just got approved took off.</summary>
    private sealed class AppliedTotals : IBillAdjustmentTotals
    {
        private readonly Dictionary<Guid, decimal> _byBill = [];

        public void Apply(Guid billId, decimal amount) => _byBill[billId] = _byBill.GetValueOrDefault(billId) + amount;

        public void Undo(Guid billId, decimal amount) => Apply(billId, -amount);

        public Task<decimal> GetAppliedAsync(Guid billId, CancellationToken cancellationToken = default)
            => Task.FromResult(_byBill.GetValueOrDefault(billId));
    }

    private AuthorizationGrantService Service()
        => new(_grants, _policies, null, [new BillAdjustmentTotalGate(_applied, _cap)]);

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

    private async Task<GrantOutcome> RequestAndApplyAsync(
        AuthorizationGrantService service, string permission, string role, Guid bill, decimal amount)
    {
        var result = await service.RequestAsync(Request(permission, role, bill, amount));
        if (result.Outcome == GrantOutcome.Authorized)
            _applied.Apply(bill, amount);
        return result.Outcome;
    }

    [Fact]
    public async Task CompsOfOneBillAddUpAndTheOneThatCrossesTheCapWaitsForAManager()
    {
        await AutoWithinAsync("bills.comp", "gate-cashier-a", 50m);
        var service = Service();
        var bill = Guid.NewGuid();

        var first = await RequestAndApplyAsync(service, "bills.comp", "gate-cashier-a", bill, 50m);
        var second = await RequestAndApplyAsync(service, "bills.comp", "gate-cashier-a", bill, 50m);
        var third = await RequestAndApplyAsync(service, "bills.comp", "gate-cashier-a", bill, 50m);
        var otherBill = await RequestAndApplyAsync(service, "bills.comp", "gate-cashier-a", Guid.NewGuid(), 50m);

        first.Should().Be(GrantOutcome.Authorized);
        second.Should().Be(GrantOutcome.Authorized);
        third.Should().Be(GrantOutcome.Pending);
        otherBill.Should().Be(GrantOutcome.Authorized);
    }

    [Fact]
    public async Task ARequestThatExactlyReachesTheCapIsStillApproved()
    {
        await AutoWithinAsync("bills.comp", "gate-cashier-b", 60m);
        var service = Service();
        var bill = Guid.NewGuid();

        var first = await RequestAndApplyAsync(service, "bills.comp", "gate-cashier-b", bill, 60m);
        var second = await RequestAndApplyAsync(service, "bills.comp", "gate-cashier-b", bill, 60m);
        var third = await RequestAndApplyAsync(service, "bills.comp", "gate-cashier-b", bill, 0.01m);

        first.Should().Be(GrantOutcome.Authorized);
        second.Should().Be(GrantOutcome.Authorized);
        third.Should().Be(GrantOutcome.Pending);
    }

    [Fact]
    public async Task ADiscountAndACompOnTheSameBillShareOneCap()
    {
        await AutoWithinAsync("bills.discount", "gate-waiter-c", 100m);
        await AutoWithinAsync("bills.comp", "gate-cashier-c", 50m);
        var service = Service();
        var bill = Guid.NewGuid();

        var discount = await RequestAndApplyAsync(service, "bills.discount", "gate-waiter-c", bill, 100m);
        var comp = await RequestAndApplyAsync(service, "bills.comp", "gate-cashier-c", bill, 50m);

        discount.Should().Be(GrantOutcome.Authorized);
        comp.Should().Be(GrantOutcome.Pending);
    }

    [Fact]
    public async Task APendingRequestDoesNotUseUpTheCapUntilItIsApplied()
    {
        await AutoWithinAsync("bills.comp", "gate-cashier-d", 50m);
        var service = Service();
        var bill = Guid.NewGuid();

        var unreviewed = await RequestAndApplyAsync(service, "bills.comp", "gate-unlisted-role-d", bill, 500m);
        var next = await RequestAndApplyAsync(service, "bills.comp", "gate-cashier-d", bill, 50m);

        unreviewed.Should().Be(GrantOutcome.Pending);
        next.Should().Be(GrantOutcome.Authorized);
    }

    [Fact]
    public async Task WhatAManagerAppliedDirectlyCountsTowardTheNextRequest()
    {
        await AutoWithinAsync("bills.comp", "gate-cashier-e", 50m);
        var service = Service();
        var bill = Guid.NewGuid();
        _applied.Apply(bill, 100m);

        var next = await RequestAndApplyAsync(service, "bills.comp", "gate-cashier-e", bill, 40m);

        next.Should().Be(GrantOutcome.Pending);
    }

    [Fact]
    public async Task AnUndoneAdjustmentNoLongerCountsAndTheCapOpensAgain()
    {
        await AutoWithinAsync("bills.comp", "gate-cashier-i", 50m);
        var service = Service();
        var bill = Guid.NewGuid();
        _applied.Apply(bill, 100m);
        var blocked = await RequestAndApplyAsync(service, "bills.comp", "gate-cashier-i", bill, 50m);

        _applied.Undo(bill, 100m);
        var afterUndo = await RequestAndApplyAsync(service, "bills.comp", "gate-cashier-i", bill, 50m);

        blocked.Should().Be(GrantOutcome.Pending);
        afterUndo.Should().Be(GrantOutcome.Authorized);
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
        _applied.Apply(subject, 500m);

        var first = await service.RequestAsync(Request(permission, "gate-cashier-g", subject, 500m, subjectType: subjectType));
        var second = await service.RequestAsync(Request(permission, "gate-cashier-g", subject, 500m, subjectType: subjectType));

        first.Outcome.Should().Be(GrantOutcome.Authorized);
        second.Outcome.Should().Be(GrantOutcome.Authorized);
    }
}
