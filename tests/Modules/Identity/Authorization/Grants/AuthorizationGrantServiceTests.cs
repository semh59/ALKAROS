using ALKAROS.Identity.Authorization.Grants;
using ALKAROS.Identity.Authorization.Policies;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Grants;

/// <summary>
/// The grant service over the real repositories and a real database. Only the
/// clock is faked, so the auto_within window boundary is deterministic.
/// </summary>
public sealed class AuthorizationGrantServiceTests : IClassFixture<GrantDatabase>
{
    private readonly GrantDatabase _db;
    private readonly PostgresAuthorizationGrantRepository _grants;
    private readonly PostgresAuthorizationPolicyRepository _policies;
    private readonly DateTimeOffset _now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    public AuthorizationGrantServiceTests(GrantDatabase db)
    {
        _db = db;
        _grants = new PostgresAuthorizationGrantRepository(db.DataSource);
        _policies = new PostgresAuthorizationPolicyRepository(db.DataSource);
    }

    private AuthorizationGrantService Service()
        => new(_grants, _policies, nowUtc: () => _now);

    private static GrantRequest Request(
        string key, string permission = "bills.comp", string role = "waiter",
        decimal amount = 0m, Guid? requester = null, Guid? serving = null) => new(
        IdempotencyKey: key,
        PermissionCode: permission,
        RequesterUserId: requester ?? Guid.NewGuid(),
        RequesterRoleCode: role,
        ReasonCode: "CustomerChange",
        Amount: amount,
        SubjectType: serving is null ? null : "bill",
        SubjectId: serving is null ? null : Guid.NewGuid(),
        SubjectServingUserId: serving);

    [Fact]
    public async Task NoPolicyEscalatesToPendingAndWritesOneRow()
    {
        var before = await _db.GrantCountAsync();

        var result = await Service().RequestAsync(Request("svc-pending"));

        result.Outcome.Should().Be(GrantOutcome.Pending);
        result.Grant.Status.Should().Be(GrantStatus.Pending);
        result.Grant.Path.Should().BeNull();
        (await _db.GrantCountAsync()).Should().Be(before + 1);
    }

    [Fact]
    public async Task ReplayingTheSameIdempotencyKeyReturnsTheFirstGrantWithoutANewRow()
    {
        // A legitimate replay (e.g. a client retry) resends the identical
        // command instance, so every field besides the key itself matches.
        var user = Guid.NewGuid();
        var request = Request("svc-replay", requester: user, serving: user);
        var first = await Service().RequestAsync(request);
        var before = await _db.GrantCountAsync();

        var replay = await Service().RequestAsync(request);

        replay.Grant.GrantId.Should().Be(first.Grant.GrantId);
        (await _db.GrantCountAsync()).Should().Be(before);
    }

    [Fact]
    public async Task ReplayingAnIdempotencyKeyForADifferentSubjectThrows()
    {
        // Found by an independent audit (2026-09-06): a bare key lookup used
        // to trust whatever grant a prior request stored under a key with no
        // check that permission/subject/requester matched — a reused key
        // against a different command instance would silently hand back
        // that command's own resolution, skipping the policy engine for the
        // new one.
        var user = Guid.NewGuid();
        await Service().RequestAsync(Request("svc-reuse", requester: user, serving: user));

        var act = () => Service().RequestAsync(new GrantRequest(
            IdempotencyKey: "svc-reuse",
            PermissionCode: "bills.comp",
            RequesterUserId: user,
            RequesterRoleCode: "waiter",
            ReasonCode: "CustomerChange",
            Amount: 500m,
            SubjectType: "bill",
            SubjectId: Guid.NewGuid(),
            SubjectServingUserId: user));

        await act.Should().ThrowAsync<IdempotencyKeyReusedException>();
    }

    [Fact]
    public async Task ReplayingAnIdempotencyKeyWithADifferentAmountThrows()
    {
        // Found by an independent audit (2026-09-07): Amount/ReasonCode were
        // missing from MatchesReplay, so a resent key whose body carried a
        // different amount than the one actually reviewed (e.g. a manager
        // approves 5%, the client then resends the same key with 95%) came
        // back as the earlier Authorized outcome instead of a rejection —
        // the caller would then apply the tampered amount, not the approved one.
        var user = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        GrantRequest Discount(decimal amount) => new(
            IdempotencyKey: "svc-amount-tamper",
            PermissionCode: "bills.comp",
            RequesterUserId: user,
            RequesterRoleCode: "waiter",
            ReasonCode: "CustomerChange",
            Amount: amount,
            SubjectType: "bill",
            SubjectId: subjectId,
            SubjectServingUserId: user);
        await Service().RequestAsync(Discount(5m));

        var act = () => Service().RequestAsync(Discount(95m));

        await act.Should().ThrowAsync<IdempotencyKeyReusedException>();
    }

    [Fact]
    public async Task ReplayingAnIdempotencyKeyWithADifferentReasonCodeThrows()
    {
        var user = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        GrantRequest Discount(string reasonCode) => new(
            IdempotencyKey: "svc-reason-tamper",
            PermissionCode: "bills.comp",
            RequesterUserId: user,
            RequesterRoleCode: "waiter",
            ReasonCode: reasonCode,
            Amount: 0m,
            SubjectType: "bill",
            SubjectId: subjectId,
            SubjectServingUserId: user);
        await Service().RequestAsync(Discount("CustomerChange"));

        var act = () => Service().RequestAsync(Discount("PromotionalOffer"));

        await act.Should().ThrowAsync<IdempotencyKeyReusedException>();
    }

    [Fact]
    public async Task AlwaysAllowPolicyAutoApproves()
    {
        await _policies.UpsertAsync(
            new AuthorizationPolicy(Guid.Empty, "bills.discount", "cashier", PolicyMode.AlwaysAllow, null, null, null, 1),
            null, Guid.NewGuid());

        var result = await Service().RequestAsync(
            Request("svc-allow", permission: "bills.discount", role: "cashier", amount: 500m));

        result.Outcome.Should().Be(GrantOutcome.Authorized);
        result.Grant.Status.Should().Be(GrantStatus.Granted);
        result.Grant.Path.Should().Be(PolicyPath.Auto);
        result.Grant.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AlwaysDenyPolicyRefuses()
    {
        await _policies.UpsertAsync(
            new AuthorizationPolicy(Guid.Empty, "tables.reserve", "waiter", PolicyMode.AlwaysDeny, null, null, null, 1),
            null, Guid.NewGuid());

        var result = await Service().RequestAsync(
            Request("svc-deny", permission: "tables.reserve", role: "waiter"));

        result.Outcome.Should().Be(GrantOutcome.Refused);
        result.Grant.Status.Should().Be(GrantStatus.Denied);
        result.Grant.Path.Should().Be(PolicyPath.Auto);
    }

    [Fact]
    public async Task AutoWithinApprovesUnderTheLimitAndCountThenEscalates()
    {
        var user = Guid.NewGuid();
        await _policies.UpsertAsync(
            new AuthorizationPolicy(Guid.Empty, "bills.void", "supervisor", PolicyMode.AutoWithin, 150m, 2, 28800, 1),
            null, Guid.NewGuid());
        var service = Service();

        var g1 = await service.RequestAsync(Request("svc-aw-1", permission: "bills.void", role: "supervisor",
            amount: 100m, requester: user, serving: user));
        var g2 = await service.RequestAsync(Request("svc-aw-2", permission: "bills.void", role: "supervisor",
            amount: 150m, requester: user, serving: user));
        var g3 = await service.RequestAsync(Request("svc-aw-3", permission: "bills.void", role: "supervisor",
            amount: 10m, requester: user, serving: user));
        var over = await service.RequestAsync(Request("svc-aw-over", permission: "bills.void", role: "supervisor",
            amount: 150.01m, requester: Guid.NewGuid()));

        g1.Outcome.Should().Be(GrantOutcome.Authorized);
        g2.Outcome.Should().Be(GrantOutcome.Authorized);
        g3.Outcome.Should().Be(GrantOutcome.Pending, "the third auto grant hits max_count = 2");
        over.Outcome.Should().Be(GrantOutcome.Pending, "150.01 is over the limit");
    }

    [Fact]
    public async Task OwnCheckGuardRefusesAVoidOnAnotherServersCheckEvenWithNoPolicy()
    {
        var waiter = Guid.NewGuid();
        var otherServer = Guid.NewGuid();

        var result = await Service().RequestAsync(new GrantRequest(
            IdempotencyKey: "svc-guard",
            PermissionCode: "bills.void",
            RequesterUserId: waiter,
            RequesterRoleCode: "waiter",
            ReasonCode: "OperatorError",
            Amount: 20m,
            SubjectType: "bill",
            SubjectId: Guid.NewGuid(),
            SubjectServingUserId: otherServer));

        result.Outcome.Should().Be(GrantOutcome.Refused);
        result.Grant.Path.Should().Be(PolicyPath.Auto);
    }

    [Fact]
    public async Task OwnCheckGuardRefusesAWaiterCompOnAnUnassignedCheck()
    {
        // V1-RMD-402 (PO 2026-09-28): a check nobody serves is not the waiter's own check.
        var result = await Service().RequestAsync(new GrantRequest(
            IdempotencyKey: "svc-guard-unassigned",
            PermissionCode: "bills.comp",
            RequesterUserId: Guid.NewGuid(),
            RequesterRoleCode: "waiter",
            ReasonCode: "CustomerChange",
            Amount: 20m,
            SubjectType: "OrderItem",
            SubjectId: Guid.NewGuid(),
            SubjectServingUserId: null));

        result.Outcome.Should().Be(GrantOutcome.Refused);
        result.Grant.Path.Should().Be(PolicyPath.Auto);
    }

    [Fact]
    public async Task OwnCheckGuardDoesNotFireOnTheServersOwnCheck()
    {
        var waiter = Guid.NewGuid();

        var result = await Service().RequestAsync(new GrantRequest(
            IdempotencyKey: "svc-guard-own",
            PermissionCode: "bills.void",
            RequesterUserId: waiter,
            RequesterRoleCode: "waiter",
            ReasonCode: "OperatorError",
            Amount: 20m,
            SubjectType: "bill",
            SubjectId: Guid.NewGuid(),
            SubjectServingUserId: waiter));

        result.Outcome.Should().Be(GrantOutcome.Pending, "no policy configured -> escalate to a manager");
    }

    [Fact]
    public async Task OwnCheckGuardDoesNotApplyToACashierVoidingAnotherServersCheck()
    {
        // Model §3: cashier bills.void is a plain grant, not "(own check)".
        var result = await Service().RequestAsync(new GrantRequest(
            IdempotencyKey: "svc-guard-cashier",
            PermissionCode: "bills.void",
            RequesterUserId: Guid.NewGuid(),
            RequesterRoleCode: "cashier",
            ReasonCode: "OperatorError",
            Amount: 20m,
            SubjectType: "bill",
            SubjectId: Guid.NewGuid(),
            SubjectServingUserId: Guid.NewGuid()));

        result.Outcome.Should().Be(GrantOutcome.Pending, "a cashier's void escalates to a manager, it is not auto-denied");
    }

    [Fact]
    public async Task OwnCheckGuardDoesNotApplyToADiscountGrant()
    {
        // Model §3: bills.discount carries no "(own check)" annotation for any role.
        var waiter = Guid.NewGuid();

        var result = await Service().RequestAsync(new GrantRequest(
            IdempotencyKey: "svc-guard-discount",
            PermissionCode: "bills.discount",
            RequesterUserId: waiter,
            RequesterRoleCode: "waiter",
            ReasonCode: "CustomerChange",
            Amount: 15m,
            SubjectType: "bill",
            SubjectId: Guid.NewGuid(),
            SubjectServingUserId: Guid.NewGuid()));

        result.Outcome.Should().Be(GrantOutcome.Pending);
    }

    [Fact]
    public async Task OwnCheckGuardDoesNotApplyToNonBillPermissions()
    {
        var result = await Service().RequestAsync(new GrantRequest(
            IdempotencyKey: "svc-guard-nonbill",
            PermissionCode: "tables.reserve",
            RequesterUserId: Guid.NewGuid(),
            RequesterRoleCode: "waiter",
            ReasonCode: "CustomerChange",
            SubjectType: "table",
            SubjectId: Guid.NewGuid(),
            SubjectServingUserId: Guid.NewGuid()));

        result.Outcome.Should().Be(GrantOutcome.Pending);
    }
}
