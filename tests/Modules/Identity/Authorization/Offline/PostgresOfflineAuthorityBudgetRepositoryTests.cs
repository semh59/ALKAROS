using ALKAROS.Identity.Authorization.Grants;
using ALKAROS.Identity.Authorization.Offline;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Offline;

public sealed class PostgresOfflineAuthorityBudgetRepositoryTests : IClassFixture<OfflineBudgetDatabase>
{
    private readonly OfflineBudgetDatabase _db;
    private readonly PostgresOfflineAuthorityBudgetRepository _repository;
    private readonly DateTimeOffset _now = new(2026, 9, 4, 18, 0, 0, TimeSpan.Zero);

    public PostgresOfflineAuthorityBudgetRepositoryTests(OfflineBudgetDatabase db)
    {
        _db = db;
        _repository = new PostgresOfflineAuthorityBudgetRepository(db.DataSource);
    }

    private static OfflineAuthorityBudgetLine[] Lines()
        =>
        [
            new OfflineAuthorityBudgetLine("bills.comp", 150m, 2),
            new OfflineAuthorityBudgetLine("bills.void", null, 1),
        ];

    [Fact]
    public async Task CreateStoresTheBudgetWithItsLinesAndBothLookupsFindIt()
    {
        var user = Guid.NewGuid();
        var session = Guid.NewGuid();

        var created = await _repository.CreateAsync(user, session, Lines(), _now, _now.AddHours(4));

        created.BudgetId.Should().NotBe(Guid.Empty);
        created.Lines.Should().HaveCount(2);

        var byId = await _repository.GetAsync(created.BudgetId);
        var bySession = await _repository.GetBySessionAsync(session);

        byId!.SessionId.Should().Be(session);
        byId.ExpiresAt.Should().Be(_now.AddHours(4));
        byId.Lines.Should().ContainSingle(l => l.PermissionCode == "bills.comp" && l.LimitAmount == 150m);
        byId.Lines.Should().ContainSingle(l => l.PermissionCode == "bills.void" && l.LimitAmount == null);
        bySession!.BudgetId.Should().Be(created.BudgetId);
    }

    [Fact]
    public async Task ReIssuingForTheSameSessionLeavesThePriorBudgetInPlaceButSupersedesIt()
    {
        // A1: re-issue no longer deletes the prior row first — a reconciled
        // prior budget can carry offline_authority_replays rows with no
        // ON DELETE CASCADE, so deleting it would throw. GetBySessionAsync
        // (and GetAsync by budget_id) both answer from the most recently
        // issued row for the key they're asked about; the prior budget is
        // still directly reachable by its own id.
        var session = Guid.NewGuid();
        var first = await _repository.CreateAsync(
            Guid.NewGuid(), session,
            new[] { new OfflineAuthorityBudgetLine("bills.comp", 100m, 1) }, _now, _now.AddHours(4));

        var second = await _repository.CreateAsync(
            Guid.NewGuid(), session,
            new[] { new OfflineAuthorityBudgetLine("bills.void", null, 3) }, _now.AddMinutes(5), _now.AddHours(5));

        second.BudgetId.Should().NotBe(first.BudgetId);
        (await _repository.GetAsync(first.BudgetId))!.Lines
            .Should().ContainSingle(l => l.PermissionCode == "bills.comp", "the prior budget is untouched, not deleted");

        var current = await _repository.GetBySessionAsync(session);
        current!.BudgetId.Should().Be(second.BudgetId, "the session now resolves to the newest issue");
        current.Lines.Should().ContainSingle(l => l.PermissionCode == "bills.void" && l.MaxCount == 3);
    }

    [Fact]
    public async Task ReIssuingAfterThePriorBudgetHasAReconciledReplayDoesNotCrash()
    {
        // The exact A1 regression: a real IOfflineReplayLedger.RecordAsync
        // write against the prior budget must not block the next CreateAsync
        // for the same session (offline_authority_replays.budget_id has no
        // ON DELETE CASCADE, and re-issue no longer deletes the prior row).
        var session = Guid.NewGuid();
        var first = await _repository.CreateAsync(
            Guid.NewGuid(), session,
            new[] { new OfflineAuthorityBudgetLine("bills.comp", 100m, 1) }, _now, _now.AddHours(4));

        var replays = new PostgresOfflineReplayLedger(_db.DataSource);
        var draft = new AuthorizationGrant(
            GrantId: Guid.Empty, IdempotencyKey: $"reissue-{first.BudgetId}", PermissionCode: "bills.comp",
            RequesterUserId: first.UserId, RequesterRoleCode: "waiter", SubjectType: null, SubjectId: null,
            SubjectServingUserId: null, Amount: 20m, ReasonCode: "CustomerChange",
            RequestedAt: _now.AddHours(1), Status: GrantStatus.Pending,
            Path: null, ApproverUserId: null, ResolvedAt: null);
        await replays.RecordAsync(draft, first.BudgetId, _now.AddMinutes(30), _now.AddHours(1));

        await FluentActions
            .Invoking(() => _repository.CreateAsync(
                Guid.NewGuid(), session,
                new[] { new OfflineAuthorityBudgetLine("bills.void", null, 1) }, _now.AddHours(2), _now.AddHours(6)))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task CreateAcceptsABudgetWithNoLines()
    {
        var created = await _repository.CreateAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            Array.Empty<OfflineAuthorityBudgetLine>(), _now, _now.AddHours(4));

        (await _repository.GetAsync(created.BudgetId))!.Lines.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateRejectsANonPositiveWindow()
        => await FluentActions.Invoking(() => _repository.CreateAsync(
                Guid.NewGuid(), Guid.NewGuid(), Lines(), _now, _now))
            .Should().ThrowAsync<ArgumentException>();

    [Fact]
    public async Task UnknownLookupsReturnNull()
    {
        (await _repository.GetAsync(Guid.NewGuid())).Should().BeNull();
        (await _repository.GetBySessionAsync(Guid.NewGuid())).Should().BeNull();
    }
}
