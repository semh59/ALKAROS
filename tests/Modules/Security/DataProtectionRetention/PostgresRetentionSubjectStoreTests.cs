using ALKAROS.Security.DataProtectionRetention.Tests.Fixtures;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Security.DataProtectionRetention.Tests;

public sealed class PostgresRetentionSubjectStoreTests : IAsyncLifetime
{
    private readonly DataProtectionRetentionTestDatabase _database = new();
    private readonly Secrets.SecretReference _key = RetentionCryptoFixtures.OldKey;
    private SensitiveData.SensitivePayloadProtector _protector = null!;
    private PostgresRetentionSubjectStore _store = null!;

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    private PostgresRetentionSubjectStore Store()
    {
        _protector = RetentionCryptoFixtures.CreateProtector();
        _store = new PostgresRetentionSubjectStore(_database.DataSource);
        return _store;
    }

    [Fact]
    public async Task InsertThenGetRoundTripsTheRecord()
    {
        var store = Store();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(_protector, _key);
        var createdAt = DateTimeOffset.UtcNow.AddDays(-1);

        var id = await store.InsertAsync(DataCategory.ProviderPayloads, envelope, legalHold: false, createdAt, default);
        var record = await store.GetAsync(id, default);

        record.Should().NotBeNull();
        record!.Category.Should().Be(DataCategory.ProviderPayloads);
        record.LegalHold.Should().BeFalse();
        record.IsDisposed.Should().BeFalse();
        record.RowVersion.Should().Be(1);
        record.Envelope.Ciphertext.KeyId.Should().Be(_key.Name);
    }

    [Fact]
    public async Task GetPendingExcludesDisposedSubjects()
    {
        var store = Store();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(_protector, _key);
        var pendingId = await store.InsertAsync(DataCategory.ProviderPayloads, envelope, false, DateTimeOffset.UtcNow, default);
        var disposedId = await store.InsertAsync(DataCategory.ProviderPayloads, envelope, false, DateTimeOffset.UtcNow, default);
        await store.MarkDisposedAsync(disposedId, DisposalAction.Anonymize, 1, default);

        var pending = await store.GetPendingAsync(default);

        pending.Select(r => r.Id).Should().Contain(pendingId);
        pending.Select(r => r.Id).Should().NotContain(disposedId);
    }

    [Fact]
    public async Task MarkDisposedAnonymizeOverwritesEnvelopeButKeepsTheRow()
    {
        var store = Store();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(_protector, _key);
        var id = await store.InsertAsync(DataCategory.ProviderPayloads, envelope, false, DateTimeOffset.UtcNow, default);

        await store.MarkDisposedAsync(id, DisposalAction.Anonymize, 1, default);
        var record = await store.GetAsync(id, default);

        record.Should().NotBeNull();
        record!.IsDisposed.Should().BeTrue();
        record.Action.Should().Be(DisposalAction.Anonymize);
        record.Envelope.Ciphertext.KeyId.Should().NotBe(_key.Name);
        Assert.Throws<SensitiveData.SensitiveDataException>(
            () => _protector.Unprotect(record.Envelope, _key, RetentionCryptoFixtures.Accessor));
    }

    [Fact]
    public async Task MarkDisposedDeleteKeepsTheEnvelopeUntilPurge()
    {
        var store = Store();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(_protector, _key);
        var id = await store.InsertAsync(DataCategory.UserCredentials, envelope, false, DateTimeOffset.UtcNow, default);

        await store.MarkDisposedAsync(id, DisposalAction.Delete, 1, default);
        var record = await store.GetAsync(id, default);
        var queue = await store.GetDeletionQueueAsync(default);

        record.Should().NotBeNull();
        record!.Envelope.Ciphertext.KeyId.Should().Be(_key.Name);
        queue.Should().Contain(id);
    }

    [Fact]
    public async Task PurgeIsIdempotent()
    {
        var store = Store();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(_protector, _key);
        var id = await store.InsertAsync(DataCategory.UserCredentials, envelope, false, DateTimeOffset.UtcNow, default);
        await store.MarkDisposedAsync(id, DisposalAction.Delete, 1, default);

        await store.PurgeAsync(id, default);
        await store.PurgeAsync(id, default);

        (await store.GetAsync(id, default)).Should().BeNull();
    }

    [Fact]
    public async Task MarkDisposedWithStaleRowVersionThrowsConcurrencyException()
    {
        var store = Store();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(_protector, _key);
        var id = await store.InsertAsync(DataCategory.ProviderPayloads, envelope, false, DateTimeOffset.UtcNow, default);

        await Assert.ThrowsAsync<RetentionConcurrencyException>(
            () => store.MarkDisposedAsync(id, DisposalAction.Anonymize, expectedRowVersion: 99, default));
    }

    [Fact]
    public async Task SetLegalHoldRoundTrips()
    {
        var store = Store();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(_protector, _key);
        var id = await store.InsertAsync(DataCategory.ProviderPayloads, envelope, false, DateTimeOffset.UtcNow, default);

        await store.SetLegalHoldAsync(id, true, 1, default);
        var record = await store.GetAsync(id, default);

        record!.LegalHold.Should().BeTrue();
        record.RowVersion.Should().Be(2);
    }

    [Fact]
    public async Task GetPendingRespectsAnExplicitLimit()
    {
        var store = Store();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(_protector, _key);
        for (var i = 0; i < 5; i++)
            await store.InsertAsync(DataCategory.ProviderPayloads, envelope, false, DateTimeOffset.UtcNow.AddMinutes(-i), default);

        var pending = await store.GetPendingAsync(default, limit: 2);

        pending.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetDeletionQueueRespectsAnExplicitLimit()
    {
        var store = Store();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(_protector, _key);
        for (var i = 0; i < 5; i++)
        {
            var id = await store.InsertAsync(DataCategory.UserCredentials, envelope, false, DateTimeOffset.UtcNow.AddMinutes(-i), default);
            await store.MarkDisposedAsync(id, DisposalAction.Delete, 1, default);
        }

        var queue = await store.GetDeletionQueueAsync(default, limit: 2);

        queue.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPendingUsesTheIndexInsteadOfASequentialScan()
    {
        // Regression for the audit finding: ix_retention_subjects_pending's
        // predicate previously included "AND legal_hold = FALSE", which does
        // not match GetPendingAsync's actual "WHERE disposed_at IS NULL"
        // query, so the planner could never pick the index. Migration 137
        // narrowed the predicate to match. Verify via EXPLAIN that a real
        // Postgres planner now chooses the index for this exact query shape.
        var store = Store();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(_protector, _key);
        for (var i = 0; i < 50; i++)
        {
            await store.InsertAsync(
                i % 2 == 0 ? DataCategory.ProviderPayloads : DataCategory.OrderNotes,
                envelope,
                legalHold: i % 3 == 0,
                DateTimeOffset.UtcNow.AddMinutes(-i),
                default);
        }
        // Force the planner to trust real statistics rather than defaults.
        await _database.ExecuteAsync("ANALYZE security.retention_subjects;");

        // With only 50 rows the cost-based planner prefers a Seq Scan
        // regardless of which indexes exist (the table simply fits in one
        // page) - that is a volume artifact, not evidence the index is
        // usable. Disabling seqscan on this session forces the planner to
        // pick the best available index instead, which proves the narrowed
        // predicate now actually matches this query shape; a mismatched
        // predicate would leave the planner no choice but Seq Scan even
        // with seqscan "disabled" (it is a strong cost penalty, not a hard
        // block), so the assertion is still a meaningful proof of usability.
        await using var connection = await _database.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var setCommand = connection.CreateCommand())
        {
            setCommand.Transaction = transaction;
            setCommand.CommandText = "SET LOCAL enable_seqscan = off;";
            await setCommand.ExecuteNonQueryAsync();
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            EXPLAIN (FORMAT TEXT)
            SELECT id, data_category, envelope_bytes, created_at, legal_hold,
                   disposed_at, disposal_action, row_version
            FROM security.retention_subjects
            WHERE disposed_at IS NULL
            ORDER BY created_at ASC;
            """;
        await using var reader = await command.ExecuteReaderAsync();
        var plan = new List<string>();
        while (await reader.ReadAsync())
            plan.Add(reader.GetString(0));

        string.Join("\n", plan).Should().Contain("ix_retention_subjects_pending",
            because: "the plan should use the narrowed partial index rather than a Seq Scan; actual plan:\n" + string.Join("\n", plan));
    }

    [Fact]
    public async Task InsertingAnInvalidDataCategoryIsRejectedByTheCheckConstraint()
    {
        var store = Store();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(_protector, _key);

        var act = () => _database.ExecuteAsync(
            """
            INSERT INTO security.retention_subjects
                (id, data_category, envelope_bytes, created_at, legal_hold, row_version)
            VALUES (@id, @category, @envelope, @created_at, FALSE, 1);
            """,
            ("id", Guid.NewGuid()),
            ("category", "BogusCategory"),
            ("envelope", envelope.ToPersistenceBytes()),
            ("created_at", DateTimeOffset.UtcNow));

        var exception = await act.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be("23514"); // check_violation
        exception.Which.ConstraintName.Should().Be("retention_subjects_data_category_check");
    }

    [Fact]
    public async Task ReadingARowWithACorruptDataCategoryThrowsACorruptDataException()
    {
        // Simulates a row written by an older/different code path that
        // bypassed the CHECK constraint (e.g. a direct DBA fix, or data from
        // before the constraint existed) - the constraint is dropped here
        // specifically to prove the code-level fail-safe in ReadRecord
        // still catches it independently of the database guard.
        var store = Store();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(_protector, _key);
        var id = await store.InsertAsync(DataCategory.ProviderPayloads, envelope, false, DateTimeOffset.UtcNow, default);

        await _database.ExecuteAsync(
            "ALTER TABLE security.retention_subjects DROP CONSTRAINT retention_subjects_data_category_check;");
        await _database.ExecuteAsync(
            "UPDATE security.retention_subjects SET data_category = 'BogusCategory' WHERE id = @id;",
            ("id", id));

        var getAct = () => store.GetAsync(id, default);
        var exception = await getAct.Should().ThrowAsync<RetentionSubjectCorruptDataException>();
        exception.Which.SubjectId.Should().Be(id);
        exception.Which.RawValue.Should().Be("BogusCategory");

        var pendingAct = () => store.GetPendingAsync(default);
        await pendingAct.Should().ThrowAsync<RetentionSubjectCorruptDataException>();
    }
}
