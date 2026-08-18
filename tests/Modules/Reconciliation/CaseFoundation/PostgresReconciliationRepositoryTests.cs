using ALKAROS.Reconciliation.CaseFoundation.Tests.Fixtures;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Reconciliation.CaseFoundation.Tests;

[Collection(nameof(ReconciliationTestFixtureDefinition))]
public sealed class PostgresReconciliationRepositoryTests : IClassFixture<ReconciliationTestDatabase>
{
    private readonly ReconciliationTestDatabase _db;
    private readonly PostgresReconciliationRepository _repository;
    private readonly ReconciliationService _service;

    public PostgresReconciliationRepositoryTests(ReconciliationTestDatabase db)
    {
        _db = db;
        _repository = new PostgresReconciliationRepository(_db.DataSource);
        _service = new ReconciliationService(_repository);
    }

    [Fact]
    public async Task CreateCaseAndDeduplicateActiveOpenCaseSuccessfully()
    {
        var user = Guid.NewGuid();
        var dedupKey = "qnb:txn:dedup_test_" + Guid.NewGuid().ToString("N");

        var request1 = new CreateCaseRequest(
            DeduplicationKey: dedupKey,
            CaseType: CaseType.PaymentMismatch,
            SourceARef: "order:ORD-500",
            SourceBRef: "bank:TX-700",
            DiscrepancyAmount: 25.50m,
            Severity: CaseSeverity.High,
            PerformedBy: user,
            DetailsJson: "{\"provider\":\"QNB\",\"error\":\"Bank declined but POS charged\"}");

        // 1. First creation -> Created
        var createdCase = await _service.CreateOrDeduplicateCaseAsync(request1);
        createdCase.Should().NotBeNull();
        createdCase.Status.Should().Be(CaseStatus.Open);
        createdCase.RowVersion.Should().Be(1);

        // 2. Second creation with SAME key -> Deduplicated into single active open case (Acceptance Evidence #1)
        var request2 = request1 with
        {
            DiscrepancyAmount = 30.00m,
            DetailsJson = "{\"retry_attempt\": 2}"
        };

        var dedupCase = await _service.CreateOrDeduplicateCaseAsync(request2);
        dedupCase.CaseId.Should().Be(createdCase.CaseId); // Same ID

        // Verify actions
        var actions = await _service.GetCaseActionsAsync(createdCase.CaseId);
        actions.Should().HaveCount(2);
        actions[0].ActionType.Should().Be(ActionType.Created);
        actions[1].ActionType.Should().Be(ActionType.Deduplicated);
    }

    [Fact]
    public async Task ForbiddenStatusTransitionsThrowException()
    {
        var user = Guid.NewGuid();
        var dedupKey = "cash:var:forbidden_" + Guid.NewGuid().ToString("N");

        var created = await _service.CreateOrDeduplicateCaseAsync(new CreateCaseRequest(
            DeduplicationKey: dedupKey,
            CaseType: CaseType.CashVariance,
            SourceARef: "terminal:T-01",
            SourceBRef: "session:SES-100",
            DiscrepancyAmount: 120.00m,
            Severity: CaseSeverity.Critical,
            PerformedBy: user));

        // Transition: Open -> Resolved
        var resolved = await _service.TransitionCaseStatusAsync(new TransitionCaseStatusRequest(
            CaseId: created.CaseId,
            NewStatus: CaseStatus.Resolved,
            ExpectedVersion: 1,
            PerformedBy: user,
            ReasonOrNote: "Variance explained by supervisor override"));

        resolved.Status.Should().Be(CaseStatus.Resolved);
        resolved.RowVersion.Should().Be(2);

        // Attempt invalid transition: Resolved -> Investigating (Terminal state transition forbidden) (Acceptance Evidence #2)
        var act = () => _service.TransitionCaseStatusAsync(new TransitionCaseStatusRequest(
            CaseId: created.CaseId,
            NewStatus: CaseStatus.Investigating,
            ExpectedVersion: 2,
            PerformedBy: user));

        await act.Should().ThrowAsync<InvalidCaseStatusTransitionException>();
    }

    [Fact]
    public async Task OptimisticConcurrencyConflictDetected()
    {
        var user = Guid.NewGuid();
        var dedupKey = "fiscal:disc:concurrency_" + Guid.NewGuid().ToString("N");

        var created = await _service.CreateOrDeduplicateCaseAsync(new CreateCaseRequest(
            DeduplicationKey: dedupKey,
            CaseType: CaseType.FiscalDiscrepancy,
            SourceARef: "receipt:RC-01",
            SourceBRef: "hugin:EJ-01",
            DiscrepancyAmount: 5.00m,
            Severity: CaseSeverity.Medium,
            PerformedBy: user));

        // Wrong expected version
        var act = () => _service.TransitionCaseStatusAsync(new TransitionCaseStatusRequest(
            CaseId: created.CaseId,
            NewStatus: CaseStatus.Investigating,
            ExpectedVersion: 99,
            PerformedBy: user));

        await act.Should().ThrowAsync<ReconciliationConcurrencyException>();
    }

    [Fact]
    public async Task AddCaseNoteAppendsAuditTrail()
    {
        var user = Guid.NewGuid();
        var dedupKey = "online:note_test_" + Guid.NewGuid().ToString("N");

        var created = await _service.CreateOrDeduplicateCaseAsync(new CreateCaseRequest(
            DeduplicationKey: dedupKey,
            CaseType: CaseType.OnlineOrderMismatch,
            SourceARef: "ysp:ORD-999",
            SourceBRef: "pos:ORD-111",
            DiscrepancyAmount: 15.00m,
            Severity: CaseSeverity.Low,
            PerformedBy: user));

        await _service.AddCaseNoteAsync(new AddCaseNoteRequest(
            created.CaseId,
            "Called delivery aggregator support, waiting for response",
            user));

        var actions = await _service.GetCaseActionsAsync(created.CaseId);
        actions.Should().Contain(a => a.ActionType == ActionType.NoteAdded);
        actions[^1].DetailsJson.Should().Contain("Called delivery aggregator support");
    }
}

public sealed class PostgresReconciliationMigrationTests : IClassFixture<ReconciliationTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresReconciliationMigrationTests(ReconciliationTestDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _dataSource = database.DataSource;
    }

    [Fact]
    public async Task MigrationDownAndUpExecutesCleanly()
    {
        var downSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "030-reconciliation-cases.down.sql"));
        var upSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "030-reconciliation-cases.up.sql"));

        // 1. Run down.sql
        await using (var connection = await _dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand(downSql, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        // Verify tables dropped
        await using (var connection = await _dataSource.OpenConnectionAsync())
        {
            await using (var checkCmd = new NpgsqlCommand("SELECT to_regclass('reconciliation.cases')::text;", connection))
            {
                var result = await checkCmd.ExecuteScalarAsync();
                result.Should().Be(DBNull.Value);
            }
        }

        // 2. Run up.sql
        await using (var connection = await _dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand(upSql, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        // Verify tables recreated
        await using (var connection = await _dataSource.OpenConnectionAsync())
        {
            await using (var checkCmd = new NpgsqlCommand("SELECT to_regclass('reconciliation.cases')::text;", connection))
            {
                var result = await checkCmd.ExecuteScalarAsync();
                result.Should().Be("reconciliation.cases");
            }
        }
    }
}

[CollectionDefinition(nameof(ReconciliationTestFixtureDefinition), DisableParallelization = true)]
public sealed class ReconciliationTestFixtureDefinition : ICollectionFixture<ReconciliationTestDatabase>
{
}
