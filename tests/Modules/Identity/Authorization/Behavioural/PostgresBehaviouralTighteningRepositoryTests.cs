using ALKAROS.Identity.Authorization.Behavioural;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Identity.Authorization.Tests.Behavioural;

public sealed class PostgresBehaviouralTighteningRepositoryTests : IClassFixture<BehaviouralDatabase>
{
    private readonly PostgresBehaviouralTighteningRepository _repository;
    private readonly PostgresBehaviouralRateSource _rates;
    private readonly BehaviouralDatabase _db;
    private readonly DateTimeOffset _now = new(2026, 9, 4, 20, 0, 0, TimeSpan.Zero);

    public PostgresBehaviouralTighteningRepositoryTests(BehaviouralDatabase db)
    {
        _db = db;
        _repository = new PostgresBehaviouralTighteningRepository(db.DataSource);
        _rates = new PostgresBehaviouralRateSource(db.DataSource);
    }

    private BehaviouralTightening Draft(Guid user, string permission = "bills.void")
        => new(Guid.Empty, user, permission, 6, 1.5m, 4m, _now, null, null);

    [Fact]
    public async Task OpenThenFindActiveReturnsIt()
    {
        var user = Guid.NewGuid();
        var opened = await _repository.OpenAsync(Draft(user));

        opened.TighteningId.Should().NotBe(Guid.Empty);
        opened.IsActive.Should().BeTrue();

        var found = await _repository.FindActiveAsync(user, "bills.void");
        found!.TighteningId.Should().Be(opened.TighteningId);
        found.RecentCount.Should().Be(6);
        found.TriggerRatio.Should().Be(4m);
    }

    [Fact]
    public async Task OpeningASecondTimeForTheSameScopeReturnsTheExistingRow()
    {
        var user = Guid.NewGuid();
        var first = await _repository.OpenAsync(Draft(user));
        var second = await _repository.OpenAsync(Draft(user) with { RecentCount = 99 });

        second.TighteningId.Should().Be(first.TighteningId);
        second.RecentCount.Should().Be(6, "the first open won; the second is a no-op read");
    }

    [Fact]
    public async Task ClearStampsTheRowAndFindActiveThenReturnsNull()
    {
        var user = Guid.NewGuid();
        var manager = Guid.NewGuid();
        var opened = await _repository.OpenAsync(Draft(user));

        var cleared = await _repository.ClearAsync(opened.TighteningId, manager, _now.AddMinutes(30));

        cleared.ClearedAt.Should().Be(_now.AddMinutes(30));
        cleared.ClearedByUserId.Should().Be(manager);
        cleared.IsActive.Should().BeFalse();

        (await _repository.FindActiveAsync(user, "bills.void")).Should().BeNull();
        (await _repository.MostRecentClearAsync(user, "bills.void")).Should().Be(_now.AddMinutes(30));
    }

    [Fact]
    public async Task ClearingATwiceOrUnknownRowThrows()
    {
        var opened = await _repository.OpenAsync(Draft(Guid.NewGuid()));
        await _repository.ClearAsync(opened.TighteningId, Guid.NewGuid(), _now.AddMinutes(5));

        await FluentActions.Invoking(() =>
                _repository.ClearAsync(opened.TighteningId, Guid.NewGuid(), _now.AddMinutes(10)))
            .Should().ThrowAsync<BehaviouralTighteningAlreadyClearedException>();
        await FluentActions.Invoking(() =>
                _repository.ClearAsync(Guid.NewGuid(), Guid.NewGuid(), _now))
            .Should().ThrowAsync<BehaviouralTighteningAlreadyClearedException>();
    }

    [Fact]
    public async Task AfterAClearTheScopeCanBeTightenedAgain()
    {
        var user = Guid.NewGuid();
        var first = await _repository.OpenAsync(Draft(user));
        await _repository.ClearAsync(first.TighteningId, Guid.NewGuid(), _now.AddMinutes(5));

        var second = await _repository.OpenAsync(Draft(user) with { TriggeredAt = _now.AddHours(1) });

        second.TighteningId.Should().NotBe(first.TighteningId);
        second.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task TheTriggerRefusesToDeleteOrReopenARow()
    {
        var opened = await _repository.OpenAsync(Draft(Guid.NewGuid()));

        await using var reopen = _db.DataSource.CreateCommand(
            "UPDATE identity.behavioural_tightenings SET recent_count = 1 WHERE tightening_id = @id;");
        reopen.Parameters.AddWithValue("id", opened.TighteningId);
        await FluentActions.Invoking(() => reopen.ExecuteNonQueryAsync())
            .Should().ThrowAsync<PostgresException>("the request evidence is immutable");

        await using var delete = _db.DataSource.CreateCommand(
            "DELETE FROM identity.behavioural_tightenings WHERE tightening_id = @id;");
        delete.Parameters.AddWithValue("id", opened.TighteningId);
        await FluentActions.Invoking(() => delete.ExecuteNonQueryAsync())
            .Should().ThrowAsync<PostgresException>();
    }

    [Fact]
    public async Task RateSourceCountsOnlyGrantedRowsInTheWindowForTheScope()
    {
        var user = Guid.NewGuid();
        await _db.SeedGrantedAsync(user, "bills.void", _now.AddHours(-2), "rate-1");
        await _db.SeedGrantedAsync(user, "bills.void", _now.AddHours(-1), "rate-2");
        await _db.SeedGrantedAsync(user, "bills.void", _now.AddDays(-10), "rate-old");
        await _db.SeedGrantedAsync(user, "bills.comp", _now.AddHours(-1), "rate-other-perm");
        await _db.SeedGrantedAsync(Guid.NewGuid(), "bills.void", _now.AddHours(-1), "rate-other-user");

        (await _rates.CountGrantedSinceAsync(user, "bills.void", _now.AddHours(-24))).Should().Be(2);
        (await _rates.CountGrantedSinceAsync(user, "bills.void", _now.AddDays(-30))).Should().Be(3);
    }
}

/// <summary>Own fixture: mutates the schema with the down migration.</summary>
public sealed class BehaviouralTighteningDownMigrationTests : IClassFixture<BehaviouralDatabase>
{
    private readonly BehaviouralDatabase _db;

    public BehaviouralTighteningDownMigrationTests(BehaviouralDatabase db) => _db = db;

    [Fact]
    public async Task DownDropsTheTighteningTableAndLeavesTheGrantsTable()
    {
        await _db.ApplyDownAsync();

        (await _db.RelationExistsAsync("identity.behavioural_tightenings")).Should().BeFalse();
        (await _db.RelationExistsAsync("identity.authorization_grants")).Should().BeTrue();
    }
}
