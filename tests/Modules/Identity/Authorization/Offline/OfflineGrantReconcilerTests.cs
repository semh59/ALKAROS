using ALKAROS.Identity.Authorization.Behavioural;
using ALKAROS.Identity.Authorization.Grants;
using ALKAROS.Identity.Authorization.Offline;
using ALKAROS.Identity.Authorization.Policies;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Offline;

/// <summary>
/// The reconciler over the real repositories and database. Only the reconnect
/// clock is faked.
/// </summary>
public sealed class OfflineGrantReconcilerTests : IClassFixture<OfflineBudgetDatabase>
{
    private readonly OfflineBudgetDatabase _db;
    private readonly PostgresOfflineAuthorityBudgetRepository _budgets;
    private readonly PostgresAuthorizationGrantRepository _grants;
    private readonly PostgresAuthorizationPolicyRepository _policies;
    private readonly PostgresOfflineReplayLedger _replays;
    private readonly PostgresBehaviouralTighteningRepository _tightenings;
    private readonly DateTimeOffset _issuedAt = new(2026, 9, 4, 8, 0, 0, TimeSpan.Zero);
    private DateTimeOffset _reconnectAt = new(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);

    public OfflineGrantReconcilerTests(OfflineBudgetDatabase db)
    {
        _db = db;
        _budgets = new PostgresOfflineAuthorityBudgetRepository(db.DataSource);
        _grants = new PostgresAuthorizationGrantRepository(db.DataSource);
        _policies = new PostgresAuthorizationPolicyRepository(db.DataSource);
        _replays = new PostgresOfflineReplayLedger(db.DataSource);
        _tightenings = new PostgresBehaviouralTighteningRepository(db.DataSource);
    }

    private OfflineGrantReconciler Reconciler()
        => new(_budgets, _grants, _policies, _replays, _tightenings, () => _reconnectAt);

    private Task<OfflineAuthorityBudget> BudgetAsync(TimeSpan ttl, params OfflineAuthorityBudgetLine[] lines)
        => _budgets.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), lines, _issuedAt, _issuedAt + ttl);

    // V1-RMD-404: by default the action is on the requester's own check (the waiter serves it), so the
    // own-check rule stays out of the way of the budget/policy tests; servedBy overrides it.
    private OfflineAuthorizedAction Action(
        string key, string permission = "bills.comp", decimal amount = 100m,
        string role = "waiter", TimeSpan? takenAfter = null, Guid? requesterUserId = null,
        bool unassigned = false, Guid? servedBy = null)
    {
        var requester = requesterUserId ?? Guid.NewGuid();
        return new(key, permission, requester, role, "CustomerChange", amount,
            _issuedAt + (takenAfter ?? TimeSpan.FromHours(1)),
            SubjectType: "OrderItem", SubjectId: Guid.NewGuid(),
            SubjectServingUserId: unassigned ? null : servedBy ?? requester);
    }

    private async Task<int> ReplayCountAsync(Guid grantId)
        => (int)await _db.ScalarAsync<long>(
            $"SELECT count(*) FROM identity.offline_authority_replays WHERE grant_id = '{grantId}';");

    [Fact]
    public async Task AnActionWithinTheBudgetIsRecordedPendingWithAReplayRow()
    {
        var budget = await BudgetAsync(
            TimeSpan.FromHours(4), new OfflineAuthorityBudgetLine("bills.comp", 150m, 2));

        var results = await Reconciler().ReconcileAsync(
            budget.BudgetId, new[] { Action("recon-ok", amount: 120m) });

        results.Should().ContainSingle();
        results[0].Status.Should().Be(GrantStatus.Pending);
        results[0].Detail.Should().Contain("offline_pending_review");

        var grant = await _grants.GetAsync(results[0].GrantId);
        grant!.Status.Should().Be(GrantStatus.Pending);
        grant.Path.Should().BeNull();
        (await ReplayCountAsync(grant.GrantId)).Should().Be(1);
    }

    [Fact]
    public async Task AnActionOverTheLineAmountIsDenied()
    {
        var budget = await BudgetAsync(
            TimeSpan.FromHours(4), new OfflineAuthorityBudgetLine("bills.comp", 100m, 5));

        var results = await Reconciler().ReconcileAsync(
            budget.BudgetId, new[] { Action("recon-over-amount", amount: 100.01m) });

        results[0].Status.Should().Be(GrantStatus.Denied);
        results[0].Detail.Should().Contain("outside the offline budget");
        (await _grants.GetAsync(results[0].GrantId))!.Status.Should().Be(GrantStatus.Denied);
    }

    [Fact]
    public async Task TheSecondActionForAPermissionCappedAtOneIsDenied()
    {
        var budget = await BudgetAsync(
            TimeSpan.FromHours(4), new OfflineAuthorityBudgetLine("bills.void", null, 1));

        var results = await Reconciler().ReconcileAsync(budget.BudgetId, new[]
        {
            Action("recon-count-1", permission: "bills.void", amount: 10m),
            Action("recon-count-2", permission: "bills.void", amount: 10m),
        });

        results[0].Status.Should().Be(GrantStatus.Pending);
        results[1].Status.Should().Be(GrantStatus.Denied);
        results[1].Detail.Should().Contain("outside the offline budget");
    }

    [Fact]
    public async Task AnActionTakenAfterTheBudgetExpiredIsDenied()
    {
        // Budget lives 30 min; the action was taken an hour in, still before the
        // 2-hour reconnect, so the row's CHECK holds but the budget was expired.
        var budget = await BudgetAsync(
            TimeSpan.FromMinutes(30), new OfflineAuthorityBudgetLine("bills.comp", 150m, 5));

        var results = await Reconciler().ReconcileAsync(
            budget.BudgetId, new[] { Action("recon-expired", amount: 20m) });

        results[0].Status.Should().Be(GrantStatus.Denied);
        results[0].Detail.Should().Contain("expired");
    }

    [Fact]
    public async Task AnActionForAPermissionOutsideTheBudgetIsDenied()
    {
        var budget = await BudgetAsync(
            TimeSpan.FromHours(4), new OfflineAuthorityBudgetLine("bills.comp", 150m, 5));

        var results = await Reconciler().ReconcileAsync(
            budget.BudgetId, new[] { Action("recon-no-line", permission: "cash.drawer", amount: 0m) });

        results[0].Status.Should().Be(GrantStatus.Denied);
        results[0].Detail.Should().Contain("not in the offline authority budget");
    }

    [Fact]
    public async Task AWaitersOfflineCompOnAnotherServersCheckIsDeniedBeforeAManager()
    {
        // V1-RMD-404 (V1-RMD-399 H-06): model §3 decision #1 applies offline too.
        var budget = await BudgetAsync(
            TimeSpan.FromHours(4), new OfflineAuthorityBudgetLine("bills.comp", 150m, 5));

        var results = await Reconciler().ReconcileAsync(budget.BudgetId, new[]
        {
            Action("recon-own-check-other", amount: 20m, servedBy: Guid.NewGuid()),
            Action("recon-own-check-unassigned", amount: 20m, unassigned: true),
        });

        results.Should().OnlyContain(result => result.Status == GrantStatus.Denied);
        results.Should().OnlyContain(result => result.Detail.Contains("only a check they serve"));
    }

    [Fact]
    public async Task ACashiersOfflineCompOnAnotherServersCheckIsNotOwnCheckDenied()
    {
        // Model §3: cashier void/comp is a plain grant, not "(own check)".
        var budget = await BudgetAsync(
            TimeSpan.FromHours(4), new OfflineAuthorityBudgetLine("bills.comp", 150m, 5));

        var results = await Reconciler().ReconcileAsync(budget.BudgetId, new[]
        {
            Action("recon-cashier-other", amount: 20m, role: "cashier", servedBy: Guid.NewGuid()),
        });

        results[0].Status.Should().Be(GrantStatus.Pending);
    }

    [Fact]
    public async Task AnActionTheLivePolicyNowDeniesIsDenied()
    {
        var role = "recon-denied-role";
        await _policies.UpsertAsync(
            new AuthorizationPolicy(Guid.Empty, "bills.comp", role, PolicyMode.AlwaysDeny, null, null, null, 1),
            null, Guid.NewGuid());
        var budget = await BudgetAsync(
            TimeSpan.FromHours(4), new OfflineAuthorityBudgetLine("bills.comp", 150m, 5));

        var results = await Reconciler().ReconcileAsync(
            budget.BudgetId, new[] { Action("recon-live-deny", amount: 20m, role: role) });

        results[0].Status.Should().Be(GrantStatus.Denied);
        results[0].Detail.Should().Contain("live policy now denies");
    }

    [Fact]
    public async Task ReconcilingTheSameActionTwiceIsIdempotent()
    {
        var budget = await BudgetAsync(
            TimeSpan.FromHours(4), new OfflineAuthorityBudgetLine("bills.comp", 150m, 5));
        var action = Action("recon-idem", amount: 40m);

        var first = await Reconciler().ReconcileAsync(budget.BudgetId, new[] { action });
        var before = await _db.CountAsync("identity.authorization_grants");

        _reconnectAt = _reconnectAt.AddMinutes(30);
        var second = await Reconciler().ReconcileAsync(budget.BudgetId, new[] { action });

        second[0].GrantId.Should().Be(first[0].GrantId);
        second[0].Detail.Should().Be("already reconciled");
        second[0].IsReplay.Should().BeTrue();
        first[0].IsReplay.Should().BeFalse();
        (await _db.CountAsync("identity.authorization_grants")).Should().Be(before);
    }

    [Fact]
    public async Task AnActionFromARequesterWithAnActiveBehaviouralTighteningIsFlagged()
    {
        var requesterId = Guid.NewGuid();
        await _tightenings.OpenAsync(new BehaviouralTightening(
            Guid.Empty, requesterId, "bills.comp", RecentCount: 6, BaselinePerWindow: 2m,
            TriggerRatio: 3m, TriggeredAt: _issuedAt, ClearedAt: null, ClearedByUserId: null));
        var budget = await BudgetAsync(
            TimeSpan.FromHours(4), new OfflineAuthorityBudgetLine("bills.comp", 150m, 2));

        var results = await Reconciler().ReconcileAsync(
            budget.BudgetId, new[] { Action("recon-flagged", amount: 40m, requesterUserId: requesterId) });

        results[0].IsBehaviourallyFlagged.Should().BeTrue();
    }

    [Fact]
    public async Task AnActionFromARequesterWithNoActiveTighteningIsNotFlagged()
    {
        var budget = await BudgetAsync(
            TimeSpan.FromHours(4), new OfflineAuthorityBudgetLine("bills.comp", 150m, 2));

        var results = await Reconciler().ReconcileAsync(
            budget.BudgetId, new[] { Action("recon-not-flagged", amount: 40m) });

        results[0].IsBehaviourallyFlagged.Should().BeFalse();
    }

    [Fact]
    public async Task AnUnknownBudgetIdIsRejected()
        => await FluentActions.Invoking(() => Reconciler().ReconcileAsync(
                Guid.NewGuid(), new[] { Action("recon-no-budget") }))
            .Should().ThrowAsync<UnknownOfflineAuthorityBudgetException>();

    [Fact]
    public async Task ABudgetDeletedBetweenTheLookupAndTheReplayInsertIsRejectedNotCrashed()
    {
        // GetAsync succeeds (the caller's snapshot), but the row is gone by the
        // time the replay insert runs — the same race a same-session re-issue
        // can cause. The FK violation (23503) must surface as the typed
        // exception, not an unhandled PostgresException.
        var budget = await BudgetAsync(
            TimeSpan.FromHours(4), new OfflineAuthorityBudgetLine("bills.comp", 150m, 2));
        await using (var delete = _db.DataSource.CreateCommand(
            "DELETE FROM identity.offline_authority_budgets WHERE budget_id = @id;"))
        {
            delete.Parameters.AddWithValue("id", budget.BudgetId);
            (await delete.ExecuteNonQueryAsync()).Should().Be(1);
        }

        var reconciler = new OfflineGrantReconciler(
            new StaleSnapshotBudgetRepository(budget), _grants, _policies, _replays, _tightenings, () => _reconnectAt);

        await FluentActions
            .Invoking(() => reconciler.ReconcileAsync(budget.BudgetId, new[] { Action("recon-vanished-budget") }))
            .Should().ThrowAsync<UnknownOfflineAuthorityBudgetException>()
            .Where(exception => exception.BudgetId == budget.BudgetId);
    }

    [Fact]
    public async Task AForeignKeyFailureOtherThanTheBudgetsIsNotReportedAsAnUnknownBudget()
    {
        // V1-RMD-427: the module schema carries no user foreign keys (V1-RMD-189 adds them in the full database), so
        // a test-only constraint on the grant's subject stands in for "a referenced row does not exist".
        var budget = await BudgetAsync(
            TimeSpan.FromHours(4), new OfflineAuthorityBudgetLine("bills.comp", 150m, 2));
        await _db.ExecuteAsync(
            """
            ALTER TABLE identity.authorization_grants
                ADD CONSTRAINT fk_rmd427_test_subject FOREIGN KEY (subject_id)
                REFERENCES identity.offline_authority_budgets (budget_id) NOT VALID;
            """);
        try
        {
            var failure = await FluentActions
                .Invoking(() => Reconciler().ReconcileAsync(budget.BudgetId, new[] { Action("recon-other-fk") }))
                .Should().ThrowAsync<PostgresException>();
            failure.Which.ConstraintName.Should().Be("fk_rmd427_test_subject");
        }
        finally
        {
            await _db.ExecuteAsync("ALTER TABLE identity.authorization_grants DROP CONSTRAINT fk_rmd427_test_subject;");
        }
    }

    /// <summary>Always answers <see cref="GetAsync"/> from a fixed snapshot, regardless of DB state.</summary>
    private sealed class StaleSnapshotBudgetRepository : IOfflineAuthorityBudgetRepository
    {
        private readonly OfflineAuthorityBudget _snapshot;

        public StaleSnapshotBudgetRepository(OfflineAuthorityBudget snapshot) => _snapshot = snapshot;

        public Task<OfflineAuthorityBudget> CreateAsync(
            Guid userId, Guid sessionId, IReadOnlyList<OfflineAuthorityBudgetLine> lines,
            DateTimeOffset issuedAt, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<OfflineAuthorityBudget?> GetAsync(Guid budgetId, CancellationToken cancellationToken = default)
            => Task.FromResult<OfflineAuthorityBudget?>(_snapshot);

        public Task<OfflineAuthorityBudget?> GetBySessionAsync(
            Guid sessionId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}

/// <summary>Own fixture: mutates the schema with the down migration.</summary>
public sealed class OfflineAuthorityDownMigrationTests : IClassFixture<OfflineBudgetDatabase>
{
    private readonly OfflineBudgetDatabase _db;

    public OfflineAuthorityDownMigrationTests(OfflineBudgetDatabase db) => _db = db;

    [Fact]
    public async Task DownDropsTheOfflineTablesAndLeavesTheGrantsTable()
    {
        await _db.ApplyDownAsync();

        (await _db.RelationExistsAsync("identity.offline_authority_replays")).Should().BeFalse();
        (await _db.RelationExistsAsync("identity.offline_authority_budget_lines")).Should().BeFalse();
        (await _db.RelationExistsAsync("identity.offline_authority_budgets")).Should().BeFalse();
        (await _db.RelationExistsAsync("identity.authorization_grants")).Should().BeTrue();
    }
}
